using System.Text;
using CompresorPdf.Core.Models;

namespace CompresorPdf.Core.Services;

/// <summary>
/// Inspección ligera del PDF sin dependencias externas (RF-12).
///
/// Es deliberadamente heurística: no parsea el formato, sólo busca marcadores en los bytes.
/// Sirve para decidir *cómo tratar* el archivo, no para procesarlo. Si en el futuro se
/// necesita precisión (p. ej. contar imágenes reales), se sustituye la implementación
/// sin tocar a los consumidores, que dependen de <see cref="IAnalizadorPdf"/>.
/// </summary>
public sealed class AnalizadorPdf : IAnalizadorPdf
{
    /// <summary>Cuánto leemos para las heurísticas. 4 MB cubre la práctica totalidad de casos.</summary>
    private const int BytesMaximosInspeccion = 4 * 1024 * 1024;

    private static readonly byte[] Cabecera = "%PDF-"u8.ToArray();

    public ArchivoPdf Analizar(string rutaArchivo)
    {
        var info = new FileInfo(rutaArchivo);

        if (!info.Exists)
        {
            return new ArchivoPdf
            {
                RutaCompleta = rutaArchivo,
                TamanoBytes = 0,
                EsCorrupto = true,
                DetalleAnalisis = "El archivo no existe."
            };
        }

        try
        {
            var muestra = LeerMuestra(info);

            if (!ComienzaCon(muestra, Cabecera))
            {
                return new ArchivoPdf
                {
                    RutaCompleta = rutaArchivo,
                    TamanoBytes = info.Length,
                    EsCorrupto = true,
                    DetalleAnalisis = "No tiene cabecera %PDF-, no es un PDF válido."
                };
            }

            var texto = Encoding.Latin1.GetString(muestra);

            return new ArchivoPdf
            {
                RutaCompleta = rutaArchivo,
                TamanoBytes = info.Length,
                EstaProtegido = texto.Contains("/Encrypt", StringComparison.Ordinal),
                PareceEscaneado = ParecesEscaneado(texto),
                Paginas = ContarPaginas(texto)
            };
        }
        catch (Exception ex)
        {
            return new ArchivoPdf
            {
                RutaCompleta = rutaArchivo,
                TamanoBytes = info.Length,
                EsCorrupto = true,
                DetalleAnalisis = $"No se pudo leer: {ex.Message}"
            };
        }
    }

    private static byte[] LeerMuestra(FileInfo info)
    {
        var aLeer = (int)Math.Min(info.Length, BytesMaximosInspeccion);
        var buffer = new byte[aLeer];
        using var fs = info.OpenRead();
        fs.ReadExactly(buffer, 0, aLeer);
        return buffer;
    }

    private static bool ComienzaCon(byte[] datos, byte[] prefijo)
    {
        if (datos.Length < prefijo.Length) return false;
        return datos.AsSpan(0, prefijo.Length).SequenceEqual(prefijo);
    }

    /// <summary>
    /// Heurística: un PDF con capa de texto declara fuentes. Si hay imágenes y ninguna
    /// fuente, casi con seguridad es un escaneo. Comprimirlo agresivamente degrada la
    /// legibilidad, así que la UI lo avisa.
    /// </summary>
    private static bool ParecesEscaneado(string texto)
    {
        var tieneFuentes = texto.Contains("/Font", StringComparison.Ordinal);
        var tieneImagenes = texto.Contains("/Image", StringComparison.Ordinal);
        return tieneImagenes && !tieneFuentes;
    }

    /// <summary>Cuenta objetos "/Type /Page" descartando "/Pages" (el nodo raíz del árbol).</summary>
    private static int ContarPaginas(string texto)
    {
        var total = 0;
        var i = 0;
        while ((i = texto.IndexOf("/Page", i, StringComparison.Ordinal)) >= 0)
        {
            var siguiente = i + 5 < texto.Length ? texto[i + 5] : ' ';
            if (siguiente is not 's')
                total++;
            i += 5;
        }
        return total;
    }
}
