using CompresorPdf.Core.Diagnostico;
using CompresorPdf.Core.Models;

namespace CompresorPdf.Core.Services;

/// <summary>
/// Motor basado en invocar Ghostscript como proceso externo.
///
/// Ghostscript re-escribe el PDF con el dispositivo <c>pdfwrite</c>: recomprime y baja el DPI
/// de las imágenes y conserva el texto como vectores, así que el PDF sigue siendo buscable
/// (requisito de calidad nº 4 del plan original).
/// </summary>
public sealed class CompresorGhostscript : ICompresorPdf
{
    private readonly ILocalizadorGhostscript _localizador;
    private readonly IEjecutorProceso _ejecutor;
    private readonly IRegistro _registro;

    public CompresorGhostscript(
        ILocalizadorGhostscript localizador,
        IEjecutorProceso? ejecutor = null,
        IRegistro? registro = null)
    {
        _localizador = localizador;
        _ejecutor = ejecutor ?? new EjecutorProceso();
        _registro = registro ?? RegistroNulo.Instancia;
    }

    public string Nombre => "Ghostscript";

    public bool EstaDisponible => _localizador.Localizar() is not null;

    /// <summary>Ruta resuelta del binario, para mostrarla en la pantalla de diagnóstico.</summary>
    public string? RutaBinario => _localizador.Localizar();

    public async Task<ResultadoMotor> ComprimirAsync(
        string rutaEntrada,
        string rutaSalida,
        PerfilCompresion perfil,
        CancellationToken ct = default)
    {
        var binario = _localizador.Localizar();
        if (binario is null)
            return ResultadoMotor.Fallo(
                "No se encontró Ghostscript. Instálalo (macOS: brew install ghostscript) " +
                "o indica su ruta en Ajustes.");

        var argumentos = ConstruirArgumentos(rutaEntrada, rutaSalida, perfil);
        _registro.Info($"Ghostscript {perfil.Nivel} → {Path.GetFileName(rutaEntrada)}");

        try
        {
            var resultado = await _ejecutor.EjecutarAsync(binario, argumentos, ct).ConfigureAwait(false);

            if (!resultado.Exitoso)
            {
                var detalle = PrimeraLineaUtil(resultado.SalidaError) ?? $"código {resultado.CodigoSalida}";
                _registro.Advertencia($"Ghostscript falló en {rutaEntrada}: {detalle}");
                LimpiarSalidaParcial(rutaSalida);
                return ResultadoMotor.Fallo($"Ghostscript devolvió un error: {detalle}");
            }

            // Ghostscript puede terminar con código 0 y aun así no escribir nada útil (o dejar
            // un archivo de 0 bytes si la ruta se había reservado antes). Ambos casos son fallo.
            if (!File.Exists(rutaSalida) || new FileInfo(rutaSalida).Length == 0L)
            {
                LimpiarSalidaParcial(rutaSalida);
                return ResultadoMotor.Fallo("Ghostscript terminó sin generar el archivo de salida.");
            }

            return ResultadoMotor.Ok();
        }
        catch (OperationCanceledException)
        {
            LimpiarSalidaParcial(rutaSalida);
            throw;
        }
        catch (Exception ex)
        {
            _registro.Error($"Error inesperado comprimiendo {rutaEntrada}", ex);
            LimpiarSalidaParcial(rutaSalida);
            return ResultadoMotor.Fallo(ex.Message);
        }
    }

    /// <summary>
    /// Traduce el perfil a los conmutadores de Ghostscript. Es internal para poder
    /// verificar en las pruebas que cada nivel produce el PDFSETTINGS correcto.
    /// </summary>
    internal static List<string> ConstruirArgumentos(
        string rutaEntrada,
        string rutaSalida,
        PerfilCompresion perfil)
    {
        var args = new List<string>
        {
            "-sDEVICE=pdfwrite",
            $"-dCompatibilityLevel={perfil.NivelCompatibilidad}",
            $"-dPDFSETTINGS={PdfSettingsDe(perfil.Nivel)}",
            "-dNOPAUSE",
            "-dQUIET",
            "-dBATCH",
            "-dSAFER",
            // Sin esto Ghostscript puede sustituir fuentes y romper el aspecto del documento.
            "-dSubsetFonts=true",
            "-dEmbedAllFonts=true"
        };

        if (perfil.DpiImagenes is { } dpi)
        {
            args.AddRange(
            [
                "-dDownsampleColorImages=true",
                "-dDownsampleGrayImages=true",
                "-dDownsampleMonoImages=true",
                $"-dColorImageResolution={dpi}",
                $"-dGrayImageResolution={dpi}",
                $"-dMonoImageResolution={dpi}"
            ]);
        }

        if (perfil.EscalaDeGrises)
        {
            args.AddRange(
            [
                "-sColorConversionStrategy=Gray",
                "-dProcessColorModel=/DeviceGray"
            ]);
        }

        args.Add($"-sOutputFile={rutaSalida}");
        args.Add(rutaEntrada);
        return args;
    }

    /// <summary>Mapeo del RF-10 documentado en la sección 6 del plan.</summary>
    internal static string PdfSettingsDe(NivelCompresion nivel) => nivel switch
    {
        NivelCompresion.Alto => "/screen",
        NivelCompresion.Medio => "/ebook",
        NivelCompresion.Bajo => "/printer",
        _ => "/ebook"
    };

    private static string? PrimeraLineaUtil(string salida) =>
        salida.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
              .FirstOrDefault();

    /// <summary>Un PDF a medio escribir es peor que ninguno: se borra.</summary>
    private static void LimpiarSalidaParcial(string ruta)
    {
        try
        {
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch
        {
            // Si no se puede borrar, el reporte ya marca el archivo como fallido.
        }
    }
}
