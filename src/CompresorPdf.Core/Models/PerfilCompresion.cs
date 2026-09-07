namespace CompresorPdf.Core.Models;

/// <summary>
/// Conjunto de ajustes guardable (RF-14). En el MVP se usa uno solo, el balanceado.
/// </summary>
public sealed class PerfilCompresion
{
    public required string Nombre { get; set; }

    public NivelCompresion Nivel { get; set; } = NivelCompresion.Medio;

    /// <summary>
    /// DPI forzado para las imágenes. Si es null se usa el que trae el perfil de Ghostscript.
    /// </summary>
    public int? DpiImagenes { get; set; }

    /// <summary>Convierte imágenes a escala de grises. Reduce mucho, degrada color.</summary>
    public bool EscalaDeGrises { get; set; }

    /// <summary>Nivel de compatibilidad PDF que se le pide a Ghostscript.</summary>
    public string NivelCompatibilidad { get; set; } = "1.7";

    public static PerfilCompresion Email() => new()
    {
        Nombre = "Email",
        Nivel = NivelCompresion.Alto,
        DpiImagenes = 96
    };

    public static PerfilCompresion Balanceado() => new()
    {
        Nombre = "Balanceado",
        Nivel = NivelCompresion.Medio
    };

    public static PerfilCompresion Impresion() => new()
    {
        Nombre = "Impresión",
        Nivel = NivelCompresion.Bajo,
        NivelCompatibilidad = "1.6"
    };
}
