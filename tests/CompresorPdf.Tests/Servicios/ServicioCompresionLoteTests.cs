using CompresorPdf.Core.Config;
using CompresorPdf.Core.Models;
using CompresorPdf.Core.Services;
using CompresorPdf.Tests.Fixtures;
using Xunit;

namespace CompresorPdf.Tests.Servicios;

public class ServicioCompresionLoteTests
{
    private static PreferenciasUsuario Prefs(double umbralMb = 2.0) => new()
    {
        UmbralMb = umbralMb,
        SalidaJuntoAlOriginal = true,
        GradoParalelismo = 2
    };

    [Fact]
    public async Task PrepararAsync_analiza_lo_mismo_que_Preparar()
    {
        using var tmp = new CarpetaTemporal();
        var servicio = new ServicioCompresionLote(new CompresorFalso());
        var rutas = new[]
        {
            tmp.CrearPdf("a.pdf", bytes: 3 * 1024 * 1024),
            tmp.CrearPdf("b.pdf", bytes: 1024),
            tmp.CrearArchivoNoPdf("roto.pdf")
        };

        var sincrono = servicio.Preparar(rutas);
        var asincrono = await servicio.PrepararAsync(rutas);

        Assert.Equal(sincrono.Count, asincrono.Count);
        Assert.Equal(
            sincrono.Select(a => (a.RutaCompleta, a.TamanoBytes, a.EsCorrupto)),
            asincrono.Select(a => (a.RutaCompleta, a.TamanoBytes, a.EsCorrupto)));
    }

    [Fact]
    public async Task PrepararAsync_reporta_progreso_una_vez_por_archivo()
    {
        using var tmp = new CarpetaTemporal();
        var servicio = new ServicioCompresionLote(new CompresorFalso());
        var rutas = Enumerable.Range(0, 5).Select(i => tmp.CrearPdf($"f{i}.pdf")).ToArray();

        var eventos = new List<ProgresoAnalisis>();
        await servicio.PrepararAsync(rutas, new Progress<ProgresoAnalisis>(p =>
        {
            lock (eventos) eventos.Add(p);
        }));

        await Task.Delay(150); // Progress<T> despacha de forma asíncrona.
        lock (eventos)
        {
            Assert.Equal(5, eventos.Count);
            Assert.Equal(5, eventos[^1].Total);
            Assert.Equal(100, eventos[^1].PorcentajeGlobal);
        }
    }

    [Fact]
    public async Task PrepararAsync_respeta_la_cancelacion()
    {
        using var tmp = new CarpetaTemporal();
        var servicio = new ServicioCompresionLote(new CompresorFalso());
        var rutas = Enumerable.Range(0, 20).Select(i => tmp.CrearPdf($"f{i}.pdf")).ToArray();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Task.Run con un token ya cancelado produce TaskCanceledException (subtipo concreto
        // de OperationCanceledException) sin llegar a ejecutar el cuerpo.
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => servicio.PrepararAsync(rutas, ct: cts.Token));
    }

    [Fact]
    public async Task Un_pdf_por_debajo_del_umbral_se_omite_sin_llamar_al_motor()
    {
        using var tmp = new CarpetaTemporal();
        var motor = new CompresorFalso();
        var servicio = new ServicioCompresionLote(motor);
        var archivos = servicio.Preparar([tmp.CrearPdf("pequeno.pdf", bytes: 1024)]);

        var resultados = await servicio.ProcesarAsync(archivos, Prefs());

        Assert.Equal(EstadoCompresion.Omitido, resultados[0].Estado);
        Assert.Equal(0, motor.Invocaciones);
    }

    [Fact]
    public async Task Un_pdf_por_encima_del_umbral_se_comprime_y_reporta_reduccion()
    {
        using var tmp = new CarpetaTemporal();
        var servicio = new ServicioCompresionLote(new CompresorFalso(factorTamano: 0.25));
        var archivos = servicio.Preparar([tmp.CrearPdf("grande.pdf", bytes: 3 * 1024 * 1024)]);

        var resultados = await servicio.ProcesarAsync(archivos, Prefs());
        var r = resultados[0];

        Assert.Equal(EstadoCompresion.Comprimido, r.Estado);
        Assert.True(r.TamanoFinal < r.TamanoOriginal);
        Assert.InRange(r.PorcentajeReduccion, 70, 80);
        Assert.True(File.Exists(r.RutaSalida));
    }

    [Fact]
    public async Task El_archivo_original_nunca_se_modifica()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.CrearPdf("grande.pdf", bytes: 3 * 1024 * 1024);
        var tamanoAntes = new FileInfo(ruta).Length;
        var servicio = new ServicioCompresionLote(new CompresorFalso());

        await servicio.ProcesarAsync(servicio.Preparar([ruta]), Prefs());

        Assert.True(File.Exists(ruta));
        Assert.Equal(tamanoAntes, new FileInfo(ruta).Length);
    }

    [Fact]
    public async Task Si_el_resultado_no_es_mas_pequeno_se_descarta_y_se_marca_SinGanancia()
    {
        using var tmp = new CarpetaTemporal();
        var servicio = new ServicioCompresionLote(new CompresorFalso(factorTamano: 1.5));
        var archivos = servicio.Preparar([tmp.CrearPdf("ya-optimizado.pdf", bytes: 3 * 1024 * 1024)]);

        var r = (await servicio.ProcesarAsync(archivos, Prefs()))[0];

        Assert.Equal(EstadoCompresion.SinGanancia, r.Estado);
        Assert.Null(r.RutaSalida);
        Assert.False(Directory.Exists(tmp.Combinar("comprimidos"))
                     && Directory.EnumerateFiles(tmp.Combinar("comprimidos")).Any());
    }

    [Fact]
    public async Task Un_pdf_protegido_se_reporta_sin_intentar_comprimirlo()
    {
        using var tmp = new CarpetaTemporal();
        var motor = new CompresorFalso();
        var servicio = new ServicioCompresionLote(motor);
        var archivos = servicio.Preparar([tmp.CrearPdf("cerrado.pdf", bytes: 3 * 1024 * 1024, protegido: true)]);

        var r = (await servicio.ProcesarAsync(archivos, Prefs()))[0];

        Assert.Equal(EstadoCompresion.Protegido, r.Estado);
        Assert.Equal(0, motor.Invocaciones);
    }

    [Fact]
    public async Task Un_archivo_corrupto_no_detiene_al_resto_del_lote()
    {
        using var tmp = new CarpetaTemporal();
        var servicio = new ServicioCompresionLote(new CompresorFalso());
        var archivos = servicio.Preparar(
        [
            tmp.CrearArchivoNoPdf("roto.pdf", bytes: 3 * 1024 * 1024),
            tmp.CrearPdf("sano.pdf", bytes: 3 * 1024 * 1024)
        ]);

        var resultados = await servicio.ProcesarAsync(archivos, Prefs());

        Assert.Equal(EstadoCompresion.Corrupto, resultados[0].Estado);
        Assert.Equal(EstadoCompresion.Comprimido, resultados[1].Estado);
    }

    [Fact]
    public async Task Un_fallo_del_motor_se_reporta_como_Error_y_el_lote_continua()
    {
        using var tmp = new CarpetaTemporal();
        var servicio = new ServicioCompresionLote(new CompresorQueFalla("gs murió"));
        var archivos = servicio.Preparar([tmp.CrearPdf("a.pdf", bytes: 3 * 1024 * 1024)]);

        var r = (await servicio.ProcesarAsync(archivos, Prefs()))[0];

        Assert.Equal(EstadoCompresion.Error, r.Estado);
        Assert.Contains("gs murió", r.Mensaje);
    }

    [Fact]
    public async Task El_progreso_se_reporta_una_vez_por_archivo()
    {
        using var tmp = new CarpetaTemporal();
        var servicio = new ServicioCompresionLote(new CompresorFalso());
        var archivos = servicio.Preparar(
        [
            tmp.CrearPdf("a.pdf", bytes: 3 * 1024 * 1024),
            tmp.CrearPdf("b.pdf", bytes: 3 * 1024 * 1024),
            tmp.CrearPdf("c.pdf", bytes: 1024)
        ]);

        var eventos = new List<ProgresoLote>();
        await servicio.ProcesarAsync(archivos, Prefs(), new Progress<ProgresoLote>(p =>
        {
            lock (eventos) eventos.Add(p);
        }));

        // Progress<T> despacha de forma asíncrona; damos margen antes de contar.
        await Task.Delay(150);
        lock (eventos) Assert.Equal(3, eventos.Count);
    }

    [Fact]
    public async Task Cancelar_deja_los_pendientes_marcados_como_Cancelado()
    {
        using var tmp = new CarpetaTemporal();
        using var cts = new CancellationTokenSource();

        var motor = new CompresorFalso { AlComprimir = async _ => { await cts.CancelAsync(); } };
        var servicio = new ServicioCompresionLote(motor);
        var archivos = servicio.Preparar(Enumerable.Range(0, 6)
            .Select(i => tmp.CrearPdf($"f{i}.pdf", bytes: 3 * 1024 * 1024)));

        var prefs = Prefs();
        prefs.GradoParalelismo = 1;

        var resultados = await servicio.ProcesarAsync(archivos, prefs, null, cts.Token);

        Assert.Contains(resultados, r => r.Estado == EstadoCompresion.Cancelado);
        Assert.DoesNotContain(resultados, r => r.Estado == EstadoCompresion.Pendiente);
    }

    [Fact]
    public async Task Dos_pdf_con_el_mismo_nombre_no_se_pisan_al_ir_a_una_carpeta_unica()
    {
        // Reproduce el caso de orígenes mixtos: varios PDF con idéntico nombre redirigidos a
        // una sola carpeta. Antes, con paralelismo, ResolverRutaSalida devolvía la misma ruta
        // a dos hilos y uno terminaba con "Ghostscript terminó sin generar el archivo".
        using var tmp = new CarpetaTemporal();
        var a = tmp.CrearPdf(Path.Combine("uno", "informe.pdf"), bytes: 3 * 1024 * 1024);
        var b = tmp.CrearPdf(Path.Combine("dos", "informe.pdf"), bytes: 3 * 1024 * 1024);
        var salida = tmp.Combinar("salida");

        var servicio = new ServicioCompresionLote(new CompresorFalso(factorTamano: 0.25));
        var prefs = new PreferenciasUsuario
        {
            UmbralMb = 2.0,
            SalidaJuntoAlOriginal = false,
            CarpetaSalida = salida,
            GradoParalelismo = 2
        };

        var resultados = await servicio.ProcesarAsync(servicio.Preparar([a, b]), prefs);

        Assert.All(resultados, r => Assert.Equal(EstadoCompresion.Comprimido, r.Estado));
        Assert.Equal(2, resultados.Select(r => r.RutaSalida).Distinct().Count());
        Assert.Equal(2, Directory.EnumerateFiles(salida).Count());
    }

    [Fact]
    public async Task Si_el_motor_dice_ok_pero_no_escribe_nada_se_marca_Error()
    {
        using var tmp = new CarpetaTemporal();
        var servicio = new ServicioCompresionLote(new CompresorSinSalida());
        var archivos = servicio.Preparar([tmp.CrearPdf("a.pdf", bytes: 3 * 1024 * 1024)]);

        var r = (await servicio.ProcesarAsync(archivos, Prefs()))[0];

        Assert.Equal(EstadoCompresion.Error, r.Estado);
        Assert.Null(r.RutaSalida);
        Assert.False(Directory.Exists(tmp.Combinar("comprimidos"))
                     && Directory.EnumerateFiles(tmp.Combinar("comprimidos")).Any());
    }

    [Fact]
    public async Task El_resumen_agrega_solo_lo_realmente_comprimido()
    {
        using var tmp = new CarpetaTemporal();
        var servicio = new ServicioCompresionLote(new CompresorFalso(factorTamano: 0.5));
        var archivos = servicio.Preparar(
        [
            tmp.CrearPdf("grande1.pdf", bytes: 4 * 1024 * 1024),
            tmp.CrearPdf("grande2.pdf", bytes: 4 * 1024 * 1024),
            tmp.CrearPdf("pequeno.pdf", bytes: 1024),
            tmp.CrearArchivoNoPdf("roto.pdf", bytes: 3 * 1024 * 1024)
        ]);

        var resumen = ResumenLote.De(await servicio.ProcesarAsync(archivos, Prefs()));

        Assert.Equal(4, resumen.Total);
        Assert.Equal(2, resumen.Comprimidos);
        Assert.Equal(1, resumen.Omitidos);
        Assert.Equal(1, resumen.Fallidos);
        Assert.InRange(resumen.PorcentajeAhorro, 45, 55);
    }
}
