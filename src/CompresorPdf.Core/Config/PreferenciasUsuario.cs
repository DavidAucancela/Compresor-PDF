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

    /// <summary>DPI forzado para las imágenes (RF-27). Si es null se usa el que trae cada
    /// nivel (300 / 150 / 72). El motor lo traduce a los conmutadores de downsampling.</summary>
    public int? DpiImagenes { get; set; }

    /// <summary>Convierte las imágenes a escala de grises (RF-28). Reduce mucho, elimina el
    /// color.</summary>
    public bool EscalaDeGrises { get; set; }

    /// <summary>Nivel de compatibilidad PDF que se le pide a Ghostscript (RF-32).</summary>
    public string NivelCompatibilidad { get; set; } = "1.7";

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

    /// <summary>Número máximo de archivos comprimidos en paralelo (RF-31).</summary>
    public int GradoParalelismo { get; set; } = 2;

    /// <summary>Estado del panel de configuración lateral (RF-26 / ADR-008): se recuerda
    /// entre sesiones para no reabrirlo siempre.</summary>
    public bool PanelConfiguracionVisible { get; set; } = true;

    /// <summary>Si true, la interfaz se muestra en tema oscuro; si false, en claro.</summary>
    public bool TemaOscuro { get; set; }

    public long UmbralBytes => (long)(UmbralMb * 1024 * 1024);

    /// <summary>
    /// Traduce las preferencias al perfil que consume el motor. Propaga TODO lo que el motor
    /// sabe usar: sin esto, <see cref="DpiImagenes"/>, <see cref="EscalaDeGrises"/> y
    /// <see cref="NivelCompatibilidad"/> nunca llegarían a Ghostscript (RF-27/28/32).
    /// </summary>
    public PerfilCompresion APerfil() => new()
    {
        Nombre = "Actual",
        Nivel = Nivel,
        DpiImagenes = DpiImagenes,
        EscalaDeGrises = EscalaDeGrises,
        NivelCompatibilidad = NivelCompatibilidad
    };

    public static PreferenciasUsuario PorDefecto() => new()
    {
        CarpetaSalida = RutasApp.CarpetaSalidaPorDefecto
    };
}
