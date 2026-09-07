namespace CompresorPdf.Core.Diagnostico;

/// <summary>Ubicaciones estándar por sistema operativo para datos de la app.</summary>
public static class RutasApp
{
    private const string NombreApp = "CompresorPdf";

    /// <summary>
    /// macOS: ~/Library/Application Support/CompresorPdf
    /// Windows: %APPDATA%\CompresorPdf
    /// </summary>
    public static string CarpetaDatos { get; } = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create),
        NombreApp);

    public static string ArchivoPreferencias => Path.Combine(CarpetaDatos, "preferencias.json");

    public static string CarpetaSalidaPorDefecto => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "PDFs comprimidos");
}
