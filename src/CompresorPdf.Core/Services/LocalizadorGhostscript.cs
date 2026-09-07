using System.Runtime.InteropServices;

namespace CompresorPdf.Core.Services;

/// <summary>
/// Busca Ghostscript primero en la ruta configurada por el usuario, luego en el PATH
/// y por último en las ubicaciones habituales de cada sistema operativo.
/// </summary>
public sealed class LocalizadorGhostscript : ILocalizadorGhostscript
{
    private readonly string? _rutaConfigurada;
    private string? _cache;

    public LocalizadorGhostscript(string? rutaConfigurada = null) => _rutaConfigurada = rutaConfigurada;

    /// <summary>Nombres del ejecutable según plataforma (gswin64c en Windows).</summary>
    private static string[] Nombres => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? ["gswin64c.exe", "gswin32c.exe", "gs.exe"]
        : ["gs"];

    private static string[] CarpetasHabituales => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ?
        [
            @"C:\Program Files\gs",
            @"C:\Program Files (x86)\gs"
        ]
        :
        [
            "/opt/homebrew/bin",
            "/usr/local/bin",
            "/usr/bin",
            "/opt/local/bin"
        ];

    public string? Localizar()
    {
        if (_cache is not null) return _cache;

        if (!string.IsNullOrWhiteSpace(_rutaConfigurada) && File.Exists(_rutaConfigurada))
            return _cache = _rutaConfigurada;

        return _cache = BuscarEnPath() ?? BuscarEnCarpetasHabituales();
    }

    private static string? BuscarEnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;

        foreach (var carpeta in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(carpeta)) continue;
            var encontrado = BuscarEn(carpeta);
            if (encontrado is not null) return encontrado;
        }
        return null;
    }

    private static string? BuscarEnCarpetasHabituales()
    {
        foreach (var carpeta in CarpetasHabituales)
        {
            var directo = BuscarEn(carpeta);
            if (directo is not null) return directo;

            // En Windows Ghostscript se instala en C:\Program Files\gs\gs10.03.1\bin
            if (!Directory.Exists(carpeta)) continue;
            foreach (var version in SubcarpetasSeguras(carpeta))
            {
                var enBin = BuscarEn(Path.Combine(version, "bin"));
                if (enBin is not null) return enBin;
            }
        }
        return null;
    }

    private static IEnumerable<string> SubcarpetasSeguras(string carpeta)
    {
        try { return Directory.EnumerateDirectories(carpeta); }
        catch { return []; }
    }

    private static string? BuscarEn(string carpeta)
    {
        foreach (var nombre in Nombres)
        {
            var candidato = Path.Combine(carpeta, nombre);
            if (File.Exists(candidato)) return candidato;
        }
        return null;
    }
}
