using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CompresorPdf.App.ViewModels;

namespace CompresorPdf.App.Views;

/// <summary>
/// Sólo contiene lo que necesita hablar con el sistema operativo: la barra de título
/// propia (plan de diseño, sección 5.1), drag &amp; drop y diálogos de archivo. Toda la
/// lógica de negocio vive en <see cref="MainWindowViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private Border? _zonaPrincipal;
    private Avalonia.Controls.Shapes.Path? _iconoMaximizar;

    public MainWindow()
    {
        InitializeComponent();

        _zonaPrincipal = this.FindControl<Border>("ZonaPrincipal");
        _iconoMaximizar = this.FindControl<Avalonia.Controls.Shapes.Path>("IconoMaximizarVentana");

        AddHandler(DragDrop.DragEnterEvent, AlEntrarArrastre);
        AddHandler(DragDrop.DragLeaveEvent, AlSalirArrastre);
        AddHandler(DragDrop.DropEvent, AlSoltar);

        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty) ActualizarIconoMaximizar();
        };
        ActualizarIconoMaximizar();

        Closing += (_, _) => Modelo?.GuardarPreferencias();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private MainWindowViewModel? Modelo => DataContext as MainWindowViewModel;

    // ---- Barra de título propia (sección 5.1) --------------------------------------

    private void AlPresionarBarraTitulo(object? origen, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            AlternarMaximizado();
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void AlMinimizar(object? origen, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void AlMaximizar(object? origen, RoutedEventArgs e) => AlternarMaximizado();

    private void AlCerrar(object? origen, RoutedEventArgs e) => Close();

    private void AlternarMaximizado() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>El glifo del botón central cambia entre "maximizar" y "restaurar" según
    /// el estado actual — el mismo lenguaje visual que cualquier barra de título nativa.</summary>
    private void ActualizarIconoMaximizar()
    {
        if (_iconoMaximizar is null) return;
        var restaurando = WindowState == WindowState.Maximized;
        _iconoMaximizar.Data = (Avalonia.Media.Geometry?)
            this.FindResource(restaurando ? "IconoRestaurar" : "IconoMaximizar");
    }

    // ---- Drag & drop (RF-01), con el feedback visual de la sección 5.2 -------------

    private void AlEntrarArrastre(object? origen, DragEventArgs e)
    {
        if (e.DataTransfer?.Contains(DataFormat.File) != true || Modelo?.PuedeAnadirArchivos != true)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = DragDropEffects.Copy;
        _zonaPrincipal?.Classes.Add("arrastrando");
    }

    private void AlSalirArrastre(object? origen, DragEventArgs e) =>
        _zonaPrincipal?.Classes.Remove("arrastrando");

    private async void AlSoltar(object? origen, DragEventArgs e)
    {
        _zonaPrincipal?.Classes.Remove("arrastrando");
        if (Modelo is null || !Modelo.PuedeAnadirArchivos) return;

        var archivos = e.DataTransfer?.TryGetFiles();
        if (archivos is null) return;

        var rutas = RutasLocalesDe(archivos);
        if (rutas.Count > 0) await Modelo.AgregarRutasAsync(rutas);
    }

    // ---- Selectores de archivo/carpeta (RF-01, RF-05) ------------------------------

    private async void AlAnadirArchivos(object? origen, RoutedEventArgs e)
    {
        if (Modelo is null || !Modelo.PuedeAnadirArchivos) return;

        var seleccion = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecciona los PDFs a comprimir",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }]
        });

        var rutas = RutasLocalesDe(seleccion);
        if (rutas.Count > 0) await Modelo.AgregarRutasAsync(rutas);
    }

    private async void AlElegirCarpeta(object? origen, RoutedEventArgs e)
    {
        if (Modelo is null) return;

        var carpetas = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Carpeta de salida",
            AllowMultiple = false
        });

        var ruta = carpetas.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(ruta)) Modelo.CarpetaSalida = ruta;
    }

    /// <summary>
    /// Los IStorageItem pueden venir de orígenes sin ruta local (iCloud, sandbox).
    /// El Core trabaja con rutas, así que descartamos los que no la tengan.
    /// </summary>
    private static List<string> RutasLocalesDe(IEnumerable<IStorageItem> elementos) =>
    [
        .. elementos
            .Select(f => f.TryGetLocalPath())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!)
    ];
}
