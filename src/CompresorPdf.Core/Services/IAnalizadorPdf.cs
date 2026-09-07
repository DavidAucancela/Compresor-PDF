using CompresorPdf.Core.Models;

namespace CompresorPdf.Core.Services;

public interface IAnalizadorPdf
{
    /// <summary>Lee el archivo y deduce tamaño, protección, validez y si parece escaneado.</summary>
    ArchivoPdf Analizar(string rutaArchivo);
}
