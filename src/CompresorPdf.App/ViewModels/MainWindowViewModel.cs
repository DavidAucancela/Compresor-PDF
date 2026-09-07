using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompresorPdf.Core.Config;
using CompresorPdf.Core.Diagnostico;
using CompresorPdf.Core.Models;
using CompresorPdf.Core.Services;

namespace CompresorPdf.App.ViewModels;

/// <summary>
/// Estado de la ventana principal. No conoce Avalonia: toda interacción con el sistema
/// (diálogos de archivo, abrir carpetas) llega desde la vista a través de estos métodos.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ServicioCompresionLote _servicio;
    private readonly CompresorGhostscript _compresor;
    private readonly IRepositorioPreferencias _repositorio;
    private readonly IRegistro _registro;

    private CancellationTokenSource? _cancelacion;
    private string? _carpetaResultados;   // RF-24: carpeta de salida del último lote

    public MainWindowViewModel(
        ServicioCompresionLote servicio,
        CompresorGhostscript compresor,
        IRepositorioPreferencias repositorio,
        PreferenciasUsuario preferencias,
        IRegistro registro)
    {
        _servicio = servicio;
        _compresor = compresor;
        _repositorio = repositorio;
        _registro = registro;

        Preferencias = preferencias;
        _umbralMb = preferencias.UmbralMb;
        _unidadUmbral = preferencias.UnidadUmbral;
        _nivel = preferencias.Nivel;
        _salidaJuntoAlOriginal = preferencias.SalidaJuntoAlOriginal;
        _carpetaSalida = preferencias.CarpetaSalida ?? "";
        _crearRespaldo = preferencias.CrearRespaldo;

        // RF-22: notificar cuando se añaden/quitan filas
        Filas.CollectionChanged += AlCambiarFilas;
    }

    public PreferenciasUsuario Preferencias { get; }

    public ObservableCollection<FilaResultadoViewModel> Filas { get; } = [];

    private void AlCambiarFilas(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HaySeleccionados));
        OnPropertyChanged(nameof(TodosSeleccionados));
        QuitarSeleccionadosCommand.NotifyCanExecuteChanged();
    }

    // ---- Ajustes enlazados a la UI (panel colapsable, sección 5.4) -----------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumenAjustes))]
    [NotifyPropertyChangedFor(nameof(UmbralValor))]
    private double _umbralMb;

    /// <summary>Unidad en la que se muestra y edita el umbral (RF-25: KB o MB). Cambiarla no
    /// cambia el umbral efectivo, sólo cómo se ve — el valor canónico sigue siendo
    /// <see cref="UmbralMb"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumenAjustes))]
    [NotifyPropertyChangedFor(nameof(UmbralValor))]
    [NotifyPropertyChangedFor(nameof(UmbralMaximo))]
    [NotifyPropertyChangedFor(nameof(UmbralIncremento))]
    [NotifyPropertyChangedFor(nameof(UmbralFormato))]
    private UnidadTamano _unidadUmbral;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumenAjustes))]
    private NivelCompresion _nivel;

    [ObservableProperty] private bool _salidaJuntoAlOriginal;
    [ObservableProperty] private string _carpetaSalida;
    [ObservableProperty] private bool _crearRespaldo;

    /// <summary>El número que edita el control numérico, en la unidad elegida. Escribir aquí
    /// convierte hacia <see cref="UmbralMb"/> (el valor real que usa el Core); cambiar de
    /// unidad no toca el umbral, sólo cómo se muestra (RF-25).</summary>
    public double UmbralValor
    {
        get => UnidadUmbral == UnidadTamano.KB ? Math.Round(UmbralMb * 1024, 2) : UmbralMb;
        set => UmbralMb = Math.Max(0, UnidadUmbral == UnidadTamano.KB ? value / 1024.0 : value);
    }

    /// <summary>500 MB expresado en la unidad activa — el mismo techo, distinta vara.</summary>
    public double UmbralMaximo => UnidadUmbral == UnidadTamano.KB ? 500 * 1024 : 500;

    public double UmbralIncremento => UnidadUmbral == UnidadTamano.KB ? 50 : 0.5;

    public string UmbralFormato => UnidadUmbral == UnidadTamano.KB ? "0" : "0.##";

    /// <summary>
    /// Resumen de una línea que se ve en la barra de título cuando el panel de ajustes
    /// está cerrado — para que quede claro qué umbral y nivel están activos sin tener que
    /// abrirlo (sección 5.4 del plan de diseño).
    /// </summary>
    public string ResumenAjustes => string.Create(
        CultureInfo.InvariantCulture,
        $"{UmbralValor:0.##} {(UnidadUmbral == UnidadTamano.KB ? "KB" : "MB")} · {NombreNivel(Nivel)}");

    private static string NombreNivel(NivelCompresion nivel) => nivel switch
    {
        NivelCompresion.Bajo => "Bajo",
        NivelCompresion.Medio => "Medio",
        NivelCompresion.Alto => "Alto",
        _ => nivel.ToString()
    };

    // ---- Estado del proceso --------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayArchivos))]
    [NotifyPropertyChangedFor(nameof(PuedeAnadirArchivos))]
    [NotifyPropertyChangedFor(nameof(EstaOcupado))]
    private bool _procesando;

    /// <summary>True mientras se analiza un lote recién cargado (RF-19). Separado de
    /// <see cref="Procesando"/> porque son etapas distintas: analizar es rápido pero, con
    /// ~200 archivos, deja de ser instantáneo — y antes corría en el hilo de UI.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayArchivos))]
    [NotifyPropertyChangedFor(nameof(PuedeAnadirArchivos))]
    [NotifyPropertyChangedFor(nameof(EstaOcupado))]
    private bool _analizando;

    /// <summary>Comprimiendo o analizando: en cualquiera de los dos casos hay una barra de
    /// progreso y un mensaje de estado que mostrar.</summary>
    public bool EstaOcupado => Procesando || Analizando;

    [ObservableProperty] private double _progreso;
    [ObservableProperty] private string _mensajeEstado = "Arrastra PDFs aquí para empezar.";

    // ---- Resumen final destacado (sección 5.6): el ahorro total en grande -----------

    [ObservableProperty] private bool _hayResumen;
    [ObservableProperty] private string _ahorroDestacado = "";
    [ObservableProperty] private string _ahorroPorcentaje = "";
    [ObservableProperty] private string _detalleResumen = "";
    [ObservableProperty] private bool _resumenTieneProblemas;

    // ---- Totales en vivo durante la compresión (RF-21) ----------------------------

    [ObservableProperty] private bool _hayTotalesEnVivo;
    [ObservableProperty] private string _totalOriginalEnVivo = "";
    [ObservableProperty] private string _totalFinalEnVivo = "";
    [ObservableProperty] private string _totalAhorroEnVivo = "";
    [ObservableProperty] private string _totalAhorroPorcentajeEnVivo = "";

    // ---- Computed ------------------------------------------------------------------

    public bool HayArchivos => Filas.Count > 0 && !Procesando && !Analizando;

    /// <summary>Habilita "Añadir archivos" y el drag&amp;drop: ni comprimiendo ni analizando
    /// un lote anterior (RF-19).</summary>
    public bool PuedeAnadirArchivos => !Procesando && !Analizando;

    public bool GhostscriptDisponible => _compresor.EstaDisponible;

    public string AvisoMotor => GhostscriptDisponible
        ? $"Ghostscript ({_compresor.RutaBinario})"
        : "Ghostscript no encontrado. macOS: brew install ghostscript · Windows: instala Ghostscript y reinicia la app.";

    // ---- Selección (RF-22) ---------------------------------------------------------

    /// <summary>True si al menos una fila está marcada.</summary>
    public bool HaySeleccionados => Filas.Any(f => f.Seleccionada);

    /// <summary>True si todas las filas están marcadas. El setter marca/desmarca todas a la vez
    /// (usado por el checkbox de cabecera de la lista).</summary>
    public bool TodosSeleccionados
    {
        get => Filas.Count > 0 && Filas.All(f => f.Seleccionada);
        set
        {
            foreach (var f in Filas) f.Seleccionada = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HaySeleccionados));
            QuitarSeleccionadosCommand.NotifyCanExecuteChanged();
        }
    }

    // ---- Acciones ------------------------------------------------------------------

    /// <summary>
    /// Añade rutas soltadas o elegidas en el selector (RF-01 / RF-02), analizándolas fuera
    /// del hilo de UI (RF-19). Con lotes grandes (~200 PDFs) el análisis puede tardar varios
    /// segundos — antes de esto, corría síncrono en el hilo que dibuja la ventana y la
    /// congelaba mientras duraba.
    /// </summary>
    public async Task AgregarRutasAsync(IEnumerable<string> rutas)
    {
        Analizando = true;
        Progreso = 0;
        MensajeEstado = "Analizando archivos…";
        NotificarComandos();

        try
        {
            var progreso = new Progress<ProgresoAnalisis>(p =>
            {
                Progreso = p.PorcentajeGlobal;
                MensajeEstado = $"Analizando {p.Analizados}/{p.Total} · {p.ArchivoActual}";
            });

            var nuevos = await _servicio.PrepararAsync(rutas, progreso);

            var yaPresentes = Filas
                .Select(f => f.Archivo.RutaCompleta)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var agregados = 0;
            foreach (var archivo in nuevos)
            {
                if (!yaPresentes.Add(archivo.RutaCompleta)) continue;

                var fila = new FilaResultadoViewModel(archivo);
                // RF-22: propagar cambios de selección al padre para que HaySeleccionados
                // y TodosSeleccionados se actualicen cuando el usuario marca un checkbox.
                fila.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(FilaResultadoViewModel.Seleccionada)) return;
                    OnPropertyChanged(nameof(HaySeleccionados));
                    OnPropertyChanged(nameof(TodosSeleccionados));
                    QuitarSeleccionadosCommand.NotifyCanExecuteChanged();
                };
                Filas.Add(fila);
                agregados++;
            }

            MensajeEstado = agregados == 0
                ? "No se añadió ningún PDF nuevo."
                : $"{Filas.Count} archivo(s) en la lista · {agregados} añadido(s).";
        }
        catch (Exception ex)
        {
            _registro.Error("Fallo analizando archivos nuevos", ex);
            MensajeEstado = $"Error al analizar los archivos: {ex.Message}";
        }
        finally
        {
            Analizando = false;
            OnPropertyChanged(nameof(HayArchivos));
            NotificarComandos();
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeComprimir))]
    private async Task ComprimirAsync()
    {
        GuardarPreferencias();

        Procesando = true;
        Progreso = 0;
        HayResumen = false;
        HayTotalesEnVivo = false;
        _carpetaResultados = null;
        _cancelacion = new CancellationTokenSource();
        NotificarComandos();

        foreach (var fila in Filas)
        {
            if (fila.Estado is not EstadoCompresion.Corrupto)
                fila.Estado = EstadoCompresion.Pendiente;
        }

        var archivos = Filas.Select(f => f.Archivo).ToList();
        var porRuta = Filas.ToDictionary(f => f.Archivo.RutaCompleta, StringComparer.OrdinalIgnoreCase);

        // RF-24b: orígenes mixtos → usar una sola carpeta de destino para todo el lote
        var preferenciasLote = Preferencias;
        if (_servicio.TieneOrigenesMixtos(archivos))
        {
            preferenciasLote = new PreferenciasUsuario
            {
                UmbralMb          = Preferencias.UmbralMb,
                UnidadUmbral      = Preferencias.UnidadUmbral,
                Nivel             = Preferencias.Nivel,
                SalidaJuntoAlOriginal = false,
                CarpetaSalida     = string.IsNullOrWhiteSpace(Preferencias.CarpetaSalida)
                                        ? RutasApp.CarpetaSalidaPorDefecto
                                        : Preferencias.CarpetaSalida,
                CrearRespaldo     = Preferencias.CrearRespaldo,
                SufijoSalida      = Preferencias.SufijoSalida,
                RutaGhostscript   = Preferencias.RutaGhostscript,
                GradoParalelismo  = Preferencias.GradoParalelismo
            };
        }

        // Acumulador de resultados parciales para RF-21 (totales en vivo).
        // Progress<T> despacha en el hilo de UI, así que la lista no necesita ser thread-safe.
        var resultadosParciales = new List<ResultadoCompresion>();

        var progreso = new Progress<ProgresoLote>(p =>
        {
            Progreso = p.PorcentajeGlobal;
            MensajeEstado = $"Procesando {p.Procesados}/{p.Total} · {p.ArchivoActual}";

            if (p.UltimoResultado is { } r)
            {
                if (porRuta.TryGetValue(r.Origen.RutaCompleta, out var fila))
                    fila.Aplicar(r);

                // RF-24: primera carpeta de salida real → botón "Abrir carpeta"
                if (_carpetaResultados is null && r.RutaSalida is { } ruta)
                    _carpetaResultados = Path.GetDirectoryName(ruta);

                // RF-21: acumular y recalcular totales en vivo
                resultadosParciales.Add(r);
                var parcial = ResumenLote.De(resultadosParciales);
                if (parcial.Comprimidos > 0)
                {
                    HayTotalesEnVivo = true;
                    TotalOriginalEnVivo = ArchivoPdf.FormatearTamano(parcial.BytesOriginales);
                    TotalFinalEnVivo    = ArchivoPdf.FormatearTamano(parcial.BytesFinales);
                    TotalAhorroEnVivo   = parcial.AhorroLegible;
                    TotalAhorroPorcentajeEnVivo = string.Create(
                        CultureInfo.InvariantCulture, $"-{parcial.PorcentajeAhorro:0.#}%");
                }
            }
        });

        try
        {
            var resultados = await _servicio.ProcesarAsync(
                archivos, preferenciasLote, progreso, _cancelacion.Token);

            // El último Progress puede llegar tarde: reconciliamos con los resultados finales.
            foreach (var r in resultados)
            {
                if (porRuta.TryGetValue(r.Origen.RutaCompleta, out var fila))
                    fila.Aplicar(r);
            }

            MostrarResumen(ResumenLote.De(resultados));
        }
        catch (Exception ex)
        {
            _registro.Error("Fallo no controlado en el lote", ex);
            MensajeEstado = $"Error inesperado: {ex.Message}";
        }
        finally
        {
            _cancelacion.Dispose();
            _cancelacion = null;
            Procesando = false;
            HayTotalesEnVivo = false;
            Progreso = 100;
            NotificarComandos();
        }
    }

    private bool PuedeComprimir() => Filas.Count > 0 && !Procesando && !Analizando && GhostscriptDisponible;

    [RelayCommand(CanExecute = nameof(PuedeCancelar))]
    private void Cancelar()
    {
        _cancelacion?.Cancel();
        MensajeEstado = "Cancelando…";
    }

    private bool PuedeCancelar() => Procesando;

    [RelayCommand(CanExecute = nameof(PuedeLimpiar))]
    private void Limpiar()
    {
        Filas.Clear();
        Progreso = 0;
        HayResumen = false;
        HayTotalesEnVivo = false;
        _carpetaResultados = null;
        MensajeEstado = "Lista vacía. Arrastra PDFs aquí.";
        NotificarComandos();
        OnPropertyChanged(nameof(HayArchivos));
    }

    private bool PuedeLimpiar() => Filas.Count > 0 && !Procesando && !Analizando;

    // RF-23: quitar solo las filas seleccionadas sin vaciar el lote
    [RelayCommand(CanExecute = nameof(PuedeQuitar))]
    private void QuitarSeleccionados()
    {
        foreach (var f in Filas.Where(f => f.Seleccionada).ToList())
            Filas.Remove(f);
        OnPropertyChanged(nameof(HayArchivos));
        OnPropertyChanged(nameof(HaySeleccionados));
        OnPropertyChanged(nameof(TodosSeleccionados));
        NotificarComandos();
    }

    private bool PuedeQuitar() => HaySeleccionados && !EstaOcupado;

    // RF-24: abrir en el explorador del SO la carpeta donde quedaron los comprimidos
    [RelayCommand(CanExecute = nameof(PuedeAbrirCarpeta))]
    private void AbrirCarpetaResultados()
    {
        if (_carpetaResultados is null) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = _carpetaResultados,
            UseShellExecute = true  // abre Finder en macOS, Explorador en Windows
        });
    }

    private bool PuedeAbrirCarpeta() => HayResumen && _carpetaResultados != null;

    private void MostrarResumen(ResumenLote resumen)
    {
        MensajeEstado = "Proceso terminado.";
        HayResumen = true;
        ResumenTieneProblemas = resumen.Fallidos > 0;
        AbrirCarpetaResultadosCommand.NotifyCanExecuteChanged();

        if (resumen.Comprimidos == 0)
        {
            AhorroDestacado = "Sin cambios";
            AhorroPorcentaje = "";
            DetalleResumen = $"{resumen.Total} archivo(s): ninguno necesitó compresión" +
                              (resumen.Fallidos > 0 ? $" · {resumen.Fallidos} con problemas." : ".");
            return;
        }

        AhorroDestacado = resumen.AhorroLegible;
        AhorroPorcentaje = string.Create(CultureInfo.InvariantCulture, $"-{resumen.PorcentajeAhorro:0.#}%");
        DetalleResumen = $"{resumen.Comprimidos} comprimido(s) · {resumen.Omitidos} omitido(s)" +
                          (resumen.Fallidos > 0 ? $" · {resumen.Fallidos} con problemas" : "");
    }

    /// <summary>Vuelca los ajustes de la UI al modelo y los persiste (RNF-07).</summary>
    public void GuardarPreferencias()
    {
        Preferencias.UmbralMb = Math.Max(0, UmbralMb);
        Preferencias.UnidadUmbral = UnidadUmbral;
        Preferencias.Nivel = Nivel;
        Preferencias.SalidaJuntoAlOriginal = SalidaJuntoAlOriginal;
        Preferencias.CarpetaSalida = string.IsNullOrWhiteSpace(CarpetaSalida) ? null : CarpetaSalida;
        Preferencias.CrearRespaldo = CrearRespaldo;
        _repositorio.Guardar(Preferencias);
    }

    private void NotificarComandos()
    {
        ComprimirCommand.NotifyCanExecuteChanged();
        CancelarCommand.NotifyCanExecuteChanged();
        LimpiarCommand.NotifyCanExecuteChanged();
        QuitarSeleccionadosCommand.NotifyCanExecuteChanged();
        AbrirCarpetaResultadosCommand.NotifyCanExecuteChanged();
    }
}
