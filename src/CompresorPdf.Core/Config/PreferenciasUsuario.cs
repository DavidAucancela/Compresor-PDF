using CompresorPdf.Core.Diagnostico;
using CompresorPdf.Core.Models;

namespace CompresorPdf.Core.Config;

/// <summary>Preferencias persistidas entre sesiones (RNF-07).</summary>
public sealed class PreferenciasUsuario
{
    /// <summary>Umbral en MB: los PDF por debajo se omiten (RF-03). Por defecto 2 MB.
    /// Es siempre el valor canónico, sin importar en qué unidad lo edite el usuario.</summary>
    public double UmbralMb { get; set; } = 2.0;

    /// <summary>Unidad en la que la UI muestra y edita <see cref="UmbralMb"/> (RF-25).
    /// Puramente de presentación: no afecta el umbral efectivo.</summary>
    public UnidadTamano UnidadUmbral { get; set; } = UnidadTamano.MB;

    public NivelCompresion Nivel { get; set; } = NivelCompresion.Medio;

    /// <summary>Carpeta de salida (RF-05). Si es null se usa <c>&lt;origen&gt;/comprimidos</c>.</summary>
    public string? CarpetaSalida { get; set; }

    /// <summary>Si es true, la salida va junto al original en una subcarpeta <c>comprimidos/</c>.</summary>
    public bool SalidaJuntoAlOriginal { get; set; } = true;

    /// <summary>Nunca sobrescribir el original sin respaldo (RF-06 / RNF-03).</summary>
    public bool CrearRespaldo { get; set; }

    /// <summary>Sufijo añadido al nombre de salida. Vacío = mismo nombre en otra carpeta.</summary>
    public string SufijoSalida { get; set; } = "";

    /// <summary>Ruta manual al binario de Ghostscript, si no está en el PATH.</summary>
    public string? RutaGhostscript { get; set; }

    /// <summary>Número máximo de archivos comprimidos en paralelo.</summary>
    public int GradoParalelismo { get; set; } = 2;

    public long UmbralBytes => (long)(UmbralMb * 1024 * 1024);

    public PerfilCompresion APerfil() => new()
    {
        Nombre = "Actual",
        Nivel = Nivel
    };

    public static PreferenciasUsuario PorDefecto() => new()
    {
        CarpetaSalida = RutasApp.CarpetaSalidaPorDefecto
    };
}
