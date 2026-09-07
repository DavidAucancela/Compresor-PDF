using System.Globalization;

namespace CompresorPdf.Core.Models;

/// <summary>Reporte por archivo exigido por RF-07.</summary>
public sealed class ResultadoCompresion
{
    public required ArchivoPdf Origen { get; init; }

    public EstadoCompresion Estado { get; set; } = EstadoCompresion.Pendiente;

    public long TamanoOriginal => Origen.TamanoBytes;

    /// <summary>Tamaño del archivo generado. 0 mientras no exista salida.</summary>
    public long TamanoFinal { get; set; }

    /// <summary>Ruta del archivo producido (null si se omitió o falló).</summary>
    public string? RutaSalida { get; set; }

    /// <summary>Mensaje legible para el usuario: causa del error o nota informativa.</summary>
    public string? Mensaje { get; set; }

    public TimeSpan Duracion { get; set; }

    /// <summary>Porcentaje de reducción (0-100). 0 si no hubo compresión efectiva.</summary>
    public double PorcentajeReduccion =>
        TamanoOriginal > 0 && TamanoFinal > 0 && TamanoFinal < TamanoOriginal
            ? (1d - (double)TamanoFinal / TamanoOriginal) * 100d
            : 0d;

    public long BytesAhorrados =>
        TamanoFinal > 0 && TamanoFinal < TamanoOriginal ? TamanoOriginal - TamanoFinal : 0;

    public string TamanoOriginalLegible => ArchivoPdf.FormatearTamano(TamanoOriginal);

    public string TamanoFinalLegible =>
        TamanoFinal > 0 ? ArchivoPdf.FormatearTamano(TamanoFinal) : "—";

    public string ReduccionLegible =>
        PorcentajeReduccion > 0
            ? string.Create(CultureInfo.InvariantCulture, $"-{PorcentajeReduccion:0.#} %")
            : "—";
}
