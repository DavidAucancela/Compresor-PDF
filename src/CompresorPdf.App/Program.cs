using Avalonia;

namespace CompresorPdf.App;

internal static class Program
{
    // Punto de entrada. No usar APIs de Avalonia antes de AppMain: el toolkit aún no está listo.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>Usado también por los diseñadores visuales de IDE.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Aplicacion>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
