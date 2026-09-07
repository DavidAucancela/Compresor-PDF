using CompresorPdf.Core.Config;
using CompresorPdf.Core.Models;

namespace CompresorPdf.Core.Services;

public interface IGestorArchivos
{
    /// <summary>Calcula la ruta de salida sin pisar nada existente.</summary>
    string ResolverRutaSalida(ArchivoPdf archivo, PreferenciasUsuario preferencias);

    /// <summary>Copia el original a una carpeta de respaldo antes de cualquier operación (RF-06).</summary>
    string? CrearRespaldo(ArchivoPdf archivo, PreferenciasUsuario preferencias);

    /// <summary>Filtra una lista de rutas dejando sólo PDFs existentes, sin duplicados.</summary>
    IReadOnlyList<string> FiltrarPdfs(IEnumerable<string> rutas);

    /// <summary>True si los archivos provienen de más de una carpeta padre distinta (RF-24b).</summary>
    bool TieneOrigenesMixtos(IReadOnlyList<ArchivoPdf> archivos);
}
