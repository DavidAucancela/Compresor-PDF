using System.Collections.Concurrent;
using System.Diagnostics;
using CompresorPdf.Core.Config;
using CompresorPdf.Core.Diagnostico;
using CompresorPdf.Core.Models;

namespace CompresorPdf.Core.Services;

/// <summary>
/// Orquesta el flujo completo de la sección 7 del plan: analizar, filtrar por umbral,
/// comprimir en paralelo y reportar.
///
/// Reglas que garantiza:
///  - RNF-02: el fallo de un archivo no detiene el lote.
///  - RNF-03: el original nunca se modifica.
///  - RF-13: la cancelación deja el lote consistente (los pendientes quedan "Cancelado").
/// </summary>
public sealed class ServicioCompresionLote
{
    private readonly ICompresorPdf _compresor;
    private readonly IAnalizadorPdf _analizador;
    private readonly IGestorArchivos _gestor;
    private readonly IRegistro _registro;

    /// <summary>Serializa la resolución de rutas de salida: con orígenes mixtos, varios PDF
    /// con el mismo nombre se redirigen a una única carpeta y <c>ResolverRutaSalida</c> (que
    /// sólo mira <c>File.Exists</c>) podría devolver la misma ruta a dos hilos a la vez.</summary>
    private readonly object _bloqueoRutaSalida = new();

    public ServicioCompresionLote(
        ICompresorPdf compresor,
        IAnalizadorPdf? analizador = null,
        IGestorArchivos? gestor = null,
        IRegistro? registro = null)
    {
        _compresor = compresor;
        _analizador = analizador ?? new AnalizadorPdf();
        _gestor = gestor ?? new GestorArchivos();
        _registro = registro ?? RegistroNulo.Instancia;
    }

    /// <summary>True si los archivos proceden de más de una carpeta padre (RF-24b).</summary>
    public bool TieneOrigenesMixtos(IReadOnlyList<ArchivoPdf> archivos) =>
        _gestor.TieneOrigenesMixtos(archivos);

    /// <summary>
    /// RF-33: reúne en una sola carpeta todos los PDF del lote que tienen resultado descargable:
    /// los <see cref="EstadoCompresion.Comprimido"/> se copian desde su ruta de salida; los
    /// <see cref="EstadoCompresion.Omitido"/> y <see cref="EstadoCompresion.SinGanancia"/> se
    /// copian desde el original (no superaron el umbral o ya estaban optimizados, pero el
    /// usuario los quiere reunidos igualmente). Omite los que ya estaban en la carpeta destino.
    /// Devuelve las rutas efectivamente copiadas.
    /// </summary>
    public IReadOnlyList<string> CopiarComprimidos(
        IEnumerable<ResultadoCompresion> resultados, string carpetaDestino)
    {
        var lista = resultados as IList<ResultadoCompresion> ?? [.. resultados];

        var comprimidos = lista
            .Where(r => r.Estado == EstadoCompresion.Comprimido && r.RutaSalida is not null)
            .Select(r => r.RutaSalida!);

        var originalesSinCambio = lista
            .Where(r => r.Estado is EstadoCompresion.Omitido or EstadoCompresion.SinGanancia)
            .Select(r => r.Origen.RutaCompleta);

        return _gestor.CopiarA(comprimidos.Concat(originalesSinCambio), carpetaDestino);
    }

    /// <summary>Analiza rutas de entrada y devuelve los archivos listos para encolar.</summary>
    public IReadOnlyList<ArchivoPdf> Preparar(IEnumerable<string> rutas) =>
        [.. _gestor.FiltrarPdfs(rutas).Select(_analizador.Analizar)];

    /// <summary>
    /// Igual que <see cref="Preparar"/> pero fuera del hilo de llamada y con progreso (RF-19).
    /// Con lotes grandes (~200 PDFs), leer hasta 4 MB de cada archivo para el análisis
    /// heurístico puede tardar varios segundos; sin esto, ese trabajo cae en el hilo de UI y
    /// congela la ventana mientras dura. No hace falta paralelizar el análisis en sí — leer
    /// secuencialmente 200 archivos ya es rápido comparado con comprimirlos — lo único que
    /// importa es que corra fuera del hilo que dibuja la interfaz.
    /// </summary>
    public async Task<IReadOnlyList<ArchivoPdf>> PrepararAsync(
        IEnumerable<string> rutas,
        IProgress<ProgresoAnalisis>? progreso = null,
        CancellationToken ct = default)
    {
        // Expandir carpetas es E/S de metadatos (listar directorios), no de contenido: rápido
        // incluso con miles de archivos. Lo costoso es leer cada PDF, que sí va a Task.Run.
        var candidatos = _gestor.FiltrarPdfs(rutas);

        return await Task.Run(() =>
        {
            var resultado = new List<ArchivoPdf>(candidatos.Count);
            for (var i = 0; i < candidatos.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var archivo = _analizador.Analizar(candidatos[i]);
                resultado.Add(archivo);
                progreso?.Report(new ProgresoAnalisis(i + 1, candidatos.Count, archivo.Nombre));
            }
            return (IReadOnlyList<ArchivoPdf>)resultado;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Procesa el lote completo. Devuelve un resultado por archivo, en el mismo orden de entrada.
    /// </summary>
    public async Task<IReadOnlyList<ResultadoCompresion>> ProcesarAsync(
        IReadOnlyList<ArchivoPdf> archivos,
        PreferenciasUsuario preferencias,
        IProgress<ProgresoLote>? progreso = null,
        CancellationToken ct = default)
    {
        var resultados = archivos
            .Select(a => new ResultadoCompresion { Origen = a })
            .ToArray();

        var procesados = 0;
        var paralelismo = Math.Clamp(preferencias.GradoParalelismo, 1, Environment.ProcessorCount);

        try
        {
            await Parallel.ForAsync(0, resultados.Length,
                new ParallelOptions { MaxDegreeOfParallelism = paralelismo, CancellationToken = ct },
                async (i, tokenElemento) =>
                {
                    var resultado = resultados[i];
                    await ProcesarUnoAsync(resultado, preferencias, tokenElemento).ConfigureAwait(false);

                    var hechos = Interlocked.Increment(ref procesados);
                    progreso?.Report(new ProgresoLote(
                        hechos, resultados.Length, resultado.Origen.Nombre, resultado));
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            foreach (var r in resultados)
            {
                if (r.Estado is EstadoCompresion.Pendiente or EstadoCompresion.Procesando)
                {
                    r.Estado = EstadoCompresion.Cancelado;
                    r.Mensaje = "Cancelado por el usuario.";
                }
            }
            _registro.Advertencia("Lote cancelado por el usuario.");
        }

        return resultados;
    }

    private async Task ProcesarUnoAsync(
        ResultadoCompresion resultado,
        PreferenciasUsuario preferencias,
        CancellationToken ct)
    {
        var archivo = resultado.Origen;
        var cronometro = Stopwatch.StartNew();
        resultado.Estado = EstadoCompresion.Procesando;

        try
        {
            if (archivo.EsCorrupto)
            {
                Marcar(resultado, EstadoCompresion.Corrupto,
                    archivo.DetalleAnalisis ?? "El archivo no es un PDF legible.");
                return;
            }

            if (archivo.EstaProtegido)
            {
                Marcar(resultado, EstadoCompresion.Protegido,
                    "PDF protegido con contraseña: no se puede recomprimir.");
                return;
            }

            // RF-03: la regla de omisión es lo primero que se evalúa sobre un PDF sano.
            if (archivo.TamanoBytes <= preferencias.UmbralBytes)
            {
                Marcar(resultado, EstadoCompresion.Omitido,
                    $"No supera el umbral de {preferencias.UmbralMb:0.##} MB.");
                return;
            }

            _gestor.CrearRespaldo(archivo, preferencias);

            // Reservamos la ruta de salida bajo cerrojo y creamos el archivo vacío: así el
            // siguiente hilo que resuelva un nombre igual (orígenes mixtos, mismo nombre de
            // fichero) lo esquiva en vez de escribir sobre el mismo PDF.
            string rutaSalida;
            lock (_bloqueoRutaSalida)
            {
                rutaSalida = _gestor.ResolverRutaSalida(archivo, preferencias);
                File.Create(rutaSalida).Dispose();
            }

            var motor = await _compresor
                .ComprimirAsync(archivo.RutaCompleta, rutaSalida, preferencias.APerfil(), ct)
                .ConfigureAwait(false);

            if (!motor.Exitoso)
            {
                BorrarSilenciosamente(rutaSalida);   // quita el placeholder vacío
                Marcar(resultado, EstadoCompresion.Error, motor.Mensaje ?? "Error del motor.");
                return;
            }

            var tamanoFinal = new FileInfo(rutaSalida).Length;

            // El motor dijo "ok" pero no escribió nada (queda el placeholder de 0 bytes): es
            // un fallo, no una compresión. Sin esto la fila quedaría como "comprimido a 0 KB".
            if (tamanoFinal == 0)
            {
                BorrarSilenciosamente(rutaSalida);
                Marcar(resultado, EstadoCompresion.Error,
                    "El motor terminó sin escribir el PDF de salida.");
                return;
            }

            resultado.TamanoFinal = tamanoFinal;
            resultado.RutaSalida = rutaSalida;

            if (tamanoFinal >= archivo.TamanoBytes)
            {
                // Comprimir lo dejó igual o peor: no tiene sentido entregar ese archivo.
                BorrarSilenciosamente(rutaSalida);
                resultado.RutaSalida = null;
                resultado.TamanoFinal = tamanoFinal;
                Marcar(resultado, EstadoCompresion.SinGanancia,
                    "El PDF ya estaba optimizado: se conserva el original.");
                return;
            }

            resultado.Estado = EstadoCompresion.Comprimido;
            if (archivo.PareceEscaneado)
                resultado.Mensaje = "Documento escaneado: revisa la legibilidad del resultado.";

            _registro.Info(
                $"{archivo.Nombre}: {archivo.TamanoBytes} → {tamanoFinal} bytes " +
                $"({resultado.PorcentajeReduccion:0.#} %)");
        }
        catch (OperationCanceledException)
        {
            resultado.Estado = EstadoCompresion.Cancelado;
            resultado.Mensaje = "Cancelado por el usuario.";
            throw;
        }
        catch (Exception ex)
        {
            // RNF-02: cualquier excepción se convierte en un resultado, nunca tumba el lote.
            _registro.Error($"Fallo procesando {archivo.RutaCompleta}", ex);
            Marcar(resultado, EstadoCompresion.Error, ex.Message);
        }
        finally
        {
            cronometro.Stop();
            resultado.Duracion = cronometro.Elapsed;
        }
    }

    private static void Marcar(ResultadoCompresion resultado, EstadoCompresion estado, string mensaje)
    {
        resultado.Estado = estado;
        resultado.Mensaje = mensaje;
    }

    private static void BorrarSilenciosamente(string ruta)
    {
        try { if (File.Exists(ruta)) File.Delete(ruta); } catch { /* sin consecuencias */ }
    }
}
