using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CompresorPdf.App.ViewModels;
using CompresorPdf.App.Views;
using CompresorPdf.Core.Config;
using CompresorPdf.Core.Diagnostico;
using CompresorPdf.Core.Services;

namespace CompresorPdf.App;

/// <summary>
/// Arranque de la aplicación. Aquí se compone el grafo de dependencias a mano:
/// el proyecto es pequeño y un contenedor de DI no aportaría nada todavía.
/// </summary>
public partial class Aplicacion : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime escritorio)
        {
            escritorio.MainWindow = new MainWindow { DataContext = CrearViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MainWindowViewModel CrearViewModel()
    {
        var registro = new RegistroArchivo();
        var repositorio = new RepositorioPreferenciasJson(registro: registro);
        var preferencias = repositorio.Cargar();

        var compresor = new CompresorGhostscript(
            new LocalizadorGhostscript(preferencias.RutaGhostscript),
            registro: registro);

        var servicio = new ServicioCompresionLote(compresor, registro: registro);

        return new MainWindowViewModel(servicio, compresor, repositorio, preferencias, registro);
    }
}
