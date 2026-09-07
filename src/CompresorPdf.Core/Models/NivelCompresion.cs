namespace CompresorPdf.Core.Models;

/// <summary>
/// Nivel de agresividad de la compresión. Mapea a los perfiles PDFSETTINGS de Ghostscript.
/// </summary>
public enum NivelCompresion
{
    /// <summary>300 dpi. Calidad de impresión, reducción moderada. → /printer</summary>
    Bajo,

    /// <summary>150 dpi. Balanceado, valor por defecto. → /ebook</summary>
    Medio,

    /// <summary>72 dpi. Máxima reducción, pensado para pantalla/email. → /screen</summary>
    Alto
}
