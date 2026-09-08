using CompresorPdf.Core.Config;
using CompresorPdf.Core.Models;

namespace CompresorPdf.Core.Services;

/// <summary>
/// Todo lo que toca el sistema de archivos: rutas de salida, respaldos y validación de entrada.
/// La regla que gobierna esta clase es RNF-03: el archivo original jamás se modifica.
/// </summary>
public sealed class GestorArchivos : IGestorArchivos
{
    public const string NombreSubcarpetaSalida = "comprimidos";
    public const string NombreSubcarpetaRespaldo = "originales";

    public string ResolverRutaSalida(ArchivoPdf archivo, PreferenciasUsuario preferencias)
    {
        var carpeta = CarpetaDestino(archivo, preferencias);
        Directory.CreateDirectory(carpeta);

        var nombre = Path.GetFileNameWithoutExtension(archivo.Nombre) + preferencias.SufijoSalida;
        var candidato = Path.Combine(carpeta, nombre + ".pdf");

        // Si la salida coincidiese con el original, forzamos un sufijo: nunca lo sobrescribimos.
        if (string.Equals(candidato, archivo.RutaCompleta, StringComparison.OrdinalIgnoreCase))
            candidato = Path.Combine(carpeta, nombre + "-comprimido.pdf");

        return RutaLibre(candidato);
    }

    public string? CrearRespaldo(ArchivoPdf archivo, PreferenciasUsuario preferencias)
    {
        if (!preferencias.CrearRespaldo) return null;

        var carpeta = Path.Combine(
            Path.GetDirectoryName(archivo.RutaCompleta) ?? ".",
            NombreSubcarpetaRespaldo);
        Directory.CreateDirectory(carpeta);

        var destino = RutaLibre(Path.Combine(carpeta, archivo.Nombre));
        File.Copy(archivo.RutaCompleta, destino);
        return destino;
    }

    public IReadOnlyList<string> FiltrarPdfs(IEnumerable<string> rutas)
    {
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resultado = new List<string>();

        foreach (var ruta in rutas)
        {
            foreach (var candidato in ExpandirCarpetas(ruta))
            {
                if (!EsPdf(candidato) || !File.Exists(candidato)) continue;
                var completa = Path.GetFullPath(candidato);
                if (vistos.Add(completa)) resultado.Add(completa);
            }
        }

        return resultado;
    }

    public IReadOnlyList<string> CopiarA(IEnumerable<string> rutasOrigen, string carpetaDestino)
    {
        if (string.IsNullOrWhiteSpace(carpetaDestino))
            throw new ArgumentException("La carpeta de destino no puede estar vacía.", nameof(carpetaDestino));

        Directory.CreateDirectory(carpetaDestino);
        var destinoCompleto = Path.GetFullPath(carpetaDestino);

        var copiados = new List<string>();
        foreach (var origen in rutasOrigen)
        {
            if (string.IsNullOrWhiteSpace(origen) || !File.Exists(origen)) continue;

            // Si el archivo ya vive en la carpeta destino no hay nada que copiar.
            var carpetaOrigen = Path.GetFullPath(Path.GetDirectoryName(origen) ?? ".");
            if (string.Equals(carpetaOrigen, destinoCompleto, StringComparison.OrdinalIgnoreCase))
                continue;

            var destino = RutaLibre(Path.Combine(carpetaDestino, Path.GetFileName(origen)));
            File.Copy(origen, destino);
            copiados.Add(destino);
        }

        return copiados;
    }

    public bool TieneOrigenesMixtos(IReadOnlyList<ArchivoPdf> archivos) =>
        archivos
            .Select(a => Path.GetDirectoryName(a.RutaCompleta))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() > 1;

    /// <summary>Soltar una carpeta en la ventana equivale a soltar los PDFs que contiene.</summary>
    private static IEnumerable<string> ExpandirCarpetas(string ruta)
    {
        if (Directory.Exists(ruta))
        {
            IEnumerable<string> hijos;
            try
            {
                hijos = Directory.EnumerateFiles(ruta, "*.pdf", SearchOption.AllDirectories);
            }
            catch
            {
                yield break;
            }

            foreach (var hijo in hijos) yield return hijo;
        }
        else
        {
            yield return ruta;
        }
    }

    private static bool EsPdf(string ruta) =>
        Path.GetExtension(ruta).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string CarpetaDestino(ArchivoPdf archivo, PreferenciasUsuario preferencias)
    {
        if (preferencias.SalidaJuntoAlOriginal || string.IsNullOrWhiteSpace(preferencias.CarpetaSalida))
        {
            var origen = Path.GetDirectoryName(archivo.RutaCompleta) ?? ".";
            return Path.Combine(origen, NombreSubcarpetaSalida);
        }

        return preferencias.CarpetaSalida;
    }

    /// <summary>Añade " (2)", " (3)"… hasta encontrar un nombre libre.</summary>
    private static string RutaLibre(string ruta)
    {
        if (!File.Exists(ruta)) return ruta;

        var carpeta = Path.GetDirectoryName(ruta)!;
        var nombre = Path.GetFileNameWithoutExtension(ruta);
        var extension = Path.GetExtension(ruta);

        for (var i = 2; i < 1000; i++)
        {
            var candidato = Path.Combine(carpeta, $"{nombre} ({i}){extension}");
            if (!File.Exists(candidato)) return candidato;
        }

        return Path.Combine(carpeta, $"{nombre}-{Guid.NewGuid():N}{extension}");
    }
}
