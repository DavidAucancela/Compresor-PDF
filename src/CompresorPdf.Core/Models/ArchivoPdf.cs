using System.Globalization;

namespace CompresorPdf.Core.Models;

/// <summary>
/// Un PDF de entrada tal y como lo ve el sistema de archivos, más los datos que
/// el <see cref="Services.AnalizadorPdf"/> logra deducir de él.
/// </summary>
public sealed class ArchivoPdf
{
    public required string RutaCompleta { get; init; }

    public string Nombre => Path.GetFileName(RutaCompleta);

    /// <summary>Tamaño en bytes del archivo original.</summary>
    public required long TamanoBytes { get; init; }

    /// <summary>True si el PDF declara un diccionario /Encrypt.</summary>
    public bool EstaProtegido { get; init; }

    /// <summary>True si la cabecera %PDF- falta o el archivo no se puede leer.</summary>
    public bool EsCorrupto { get; init; }

    /// <summary>Heurística: no se encontraron fuentes → probablemente es un escaneo sin capa de texto.</summary>
    public bool PareceEscaneado { get; init; }

    /// <summary>Número de páginas detectado (0 si no se pudo determinar).</summary>
    public int Paginas { get; init; }

    /// <summary>Motivo del fallo de análisis, si lo hubo.</summary>
    public string? DetalleAnalisis { get; init; }

    public static string FormatearTamano(long bytes)
    {
        string[] unidades = ["B", "KB", "MB", "GB"];
        double valor = bytes;
        int i = 0;
        while (valor >= 1024 && i < unidades.Length - 1)
        {
            valor /= 1024;
            i++;
        }
        return string.Create(CultureInfo.InvariantCulture, $"{valor:0.##} {unidades[i]}");
    }
}
