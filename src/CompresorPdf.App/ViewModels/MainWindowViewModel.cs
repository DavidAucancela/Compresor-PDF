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
    /// <summary>Reconstruye motor y orquestador para una ruta de Ghostscript dada (RF-30).
    /// El orquestador guarda una referencia al compresor en su constructor, así que para que
    /// "Volver a comprobar" tenga efecto hay que rehacer los dos, no sólo el compresor.</summary>
    private readonly Func<string?, (ServicioCompresionLote Servicio, CompresorGhostscript Compresor)> _fabricaMotor;

    private ServicioCompresionLote _servicio;
    private CompresorGhostscript _compresor;
    private readonly IRepositorioPreferencias _repositorio;
    private readonly IRegistro _registro;

    private CancellationTokenSource? _cancelacion;
    private string? _carpetaResultados;   // RF-24: carpeta de salida del último lote

    public MainWindowViewModel(
        ServicioCompresionLote servicio,
        CompresorGhostscript compresor,
        Func<string?, (ServicioCompresionLote, CompresorGhostscript)> fabricaMotor,
        IRepositorioPreferencias repositorio,
        PreferenciasUsuario preferencias,
        IRegistro registro)
    {
        _servicio = servicio;
        _compresor = compresor;
        _fabricaMotor = fabricaMotor;
        _repositorio = repositorio;
        _registro = registro;

        Preferencias = preferencias;
        _umbralMb = preferencias.UmbralMb;
        _unidadUmbral = preferencias.UnidadUmbral;
        _nivel = preferencias.Nivel;
        _salidaJuntoAlOriginal = preferencias.SalidaJuntoAlOriginal;
        _carpetaSalida = preferencias.CarpetaSalida ?? "";
        _sufijoSalida = preferencias.SufijoSalida;
        _escalaDeGrises = preferencias.EscalaDeGrises;
        _gradoParalelismo = preferencias.GradoParalelismo;
        _rutaGhostscript = preferencias.RutaGhostscript ?? "";
        _panelConfigVisible = preferencias.PanelConfiguracionVisible;
        _temaOscuro = preferencias.TemaOscuro;

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

    /// <summary>Sufijo que se añade al nombre del archivo de salida (RF-29). El campo ya se
    /// persistía; hasta la Fase 6 no había forma de editarlo desde la UI.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EjemploNombreSalida))]
    private string _sufijoSalida;

    [ObservableProperty] private bool _escalaDeGrises;            // RF-28

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParalelismoValor))]
    private int _gradoParalelismo;                                // RF-31

    [ObservableProperty] private string _rutaGhostscript;        // RF-30

    /// <summary>RF-26 / ADR-008: el panel lateral de configuración está desplegado o plegado.</summary>
    [ObservableProperty] private bool _panelConfigVisible;

    /// <summary>Tema oscuro activo. El code-behind reacciona al cambio para llamar a
    /// Application.RequestedThemeVariant. Se persiste al instante igual que el panel.</summary>
    [ObservableProperty] private bool _temaOscuro;

    /// <summary>Tope del grado de paralelismo: no tiene sentido pasar del número de núcleos,
    /// que es donde el propio orquestador lo recorta.</summary>
    public int MaxParalelismo => Environment.ProcessorCount;

    /// <summary>Fachada nullable para el NumericUpDown (su <c>Value</c> es <c>decimal?</c>):
    /// vaciar el campo manda null y lo tratamos como el mínimo (RF-31).</summary>
    public decimal? ParalelismoValor
    {
        get => GradoParalelismo;
        set => GradoParalelismo = value is null
            ? 1
            : Math.Clamp((int)value.Value, 1, MaxParalelismo);
    }

    /// <summary>Vista previa del nombre resultante con el sufijo actual (RF-29).</summary>
    public string EjemploNombreSalida => $"documento{SufijoSalida}.pdf";

    /// <summary>El número que edita el control numérico, en la unidad elegida. Es
    /// <c>decimal?</c> porque el <c>Value</c> del <c>NumericUpDown</c> lo es: al vaciar el
    /// campo manda null y convertirlo a un tipo no anulable reventaba con
    /// <c>InvalidCastException</c>. Escribir aquí convierte hacia <see cref="UmbralMb"/> (el
    /// valor real que usa el Core); cambiar de unidad no toca el umbral, sólo cómo se
    /// muestra (RF-25).</summary>
    public decimal? UmbralValor
    {
        get => (decimal)(UnidadUmbral == UnidadTamano.KB ? Math.Round(UmbralMb * 1024, 2) : UmbralMb);
        set
        {
            var v = (double)(value ?? 0m);
            UmbralMb = Math.Max(0, UnidadUmbral == UnidadTamano.KB ? v / 1024.0 : v);
        }
    }

    /// <summary>500 MB expresado en la unidad activa — el mismo techo, distinta vara.</summary>
    public decimal UmbralMaximo => UnidadUmbral == UnidadTamano.KB ? 512000m : 500m;

    public decimal UmbralIncremento => UnidadUmbral == UnidadTamano.KB ? 50m : 0.5m;

    public string UmbralFormato => UnidadUmbral == UnidadTamano.KB ? "0" : "0.##";

    /// <summary>
    /// Resumen de una línea que se ve en la barra de título cuando el panel de ajustes
    /// está cerrado — para que quede claro qué umbral y nivel están activos sin tener que
    /// abrirlo (sección 5.4 del plan de diseño).
    /// </summary>
    public string ResumenAjustes => string.Create(
        CultureInfo.InvariantCulture,
        $"{UmbralValor ?? 0m:0.##} {(UnidadUmbral == UnidadTamano.KB ? "KB" : "MB")} · {NombreNivel(Nivel)}");

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

    // ---- Resultados del último lote (RF-33: reunión automática de omitidos) -------

    private IReadOnlyList<ResultadoCompresion> _ultimosResultados = [];

    // ---- Mensaje informativo persistente (visible tras cada operación clave) ------

    /// <summary>Resumen que persiste después de que termina la animación de progreso:
    /// cuántos archivos se cargaron, dónde quedaron los comprimidos, qué copió "Descargar"…</summary>
    [ObservableProperty] private string _mensajeInformativo = "";

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

            if (agregados == 0)
            {
                MensajeEstado = "No se añadió ningún PDF nuevo.";
            }
            else
            {
                MensajeEstado = $"{Filas.Count} archivo(s) en la lista · {agregados} añadido(s).";
                var umbral = Preferencias.UmbralBytes;
                var aComprimir = Filas.Count(f => !f.Archivo.EsCorrupto && !f.Archivo.EstaProtegido
                                                  && f.Archivo.TamanoBytes > umbral);
                var conProblemas = Filas.Count(f => f.Archivo.EsCorrupto || f.Archivo.EstaProtegido);
                var aOmitir = Filas.Count - aComprimir - conProblemas;
                MensajeInformativo =
                    $"{Filas.Count} archivo(s) cargados · {aComprimir} superan el umbral" +
                    (aOmitir > 0 ? $" · {aOmitir} se omitirán (bajo el umbral)" : "") +
                    (conProblemas > 0 ? $" · {conProblemas} con problemas" : "") + ".";
            }
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
    private Task ComprimirAsync() => EjecutarLoteAsync([.. Filas], esReintento: false);

    /// <summary>Reintenta sólo las filas que terminaron en <see cref="EstadoCompresion.Error"/>
    /// (el motor falló, pero el archivo puede estar bien). Fusiona los nuevos resultados con
    /// los del lote anterior para que el resumen siga reflejando todo.</summary>
    [RelayCommand(CanExecute = nameof(PuedeReintentar))]
    private Task ReintentarFallidosAsync() =>
        EjecutarLoteAsync(
            [.. Filas.Where(f => f.Estado == EstadoCompresion.Error)], esReintento: true);

    private bool PuedeReintentar() =>
        HayResumen && !Procesando && !Analizando && GhostscriptDisponible
        && Filas.Any(f => f.Estado == EstadoCompresion.Error);

    /// <summary>True si el último lote dejó algún archivo en Error (reintentable).</summary>
    public bool HayFallidos =>
        HayResumen && Filas.Any(f => f.Estado == EstadoCompresion.Error);

    private async Task EjecutarLoteAsync(IReadOnlyList<FilaResultadoViewModel> filas, bool esReintento)
    {
        if (filas.Count == 0) return;

        GuardarPreferencias();

        Procesando = true;
        Progreso = 0;
        HayTotalesEnVivo = false;
        if (!esReintento)
        {
            HayResumen = false;
            _ultimosResultados = [];
            _carpetaResultados = null;
            MensajeInformativo = "";
        }
        _cancelacion = new CancellationTokenSource();
        NotificarComandos();

        foreach (var fila in filas)
        {
            if (fila.Estado is not EstadoCompresion.Corrupto)
                fila.Estado = EstadoCompresion.Pendiente;
        }

        var archivos = filas.Select(f => f.Archivo).ToList();
        var porRuta = filas.ToDictionary(f => f.Archivo.RutaCompleta, StringComparer.OrdinalIgnoreCase);

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
                GradoParalelismo  = Preferencias.GradoParalelismo,
                EscalaDeGrises    = Preferencias.EscalaDeGrises,
                DpiImagenes       = Preferencias.DpiImagenes,
                NivelCompatibilidad = Preferencias.NivelCompatibilidad
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

            // RF-33 / reintento: fusionamos con lo que ya había para no perder el resto del lote.
            _ultimosResultados = FusionarResultados(_ultimosResultados, resultados);
            MostrarResumen(ResumenLote.De(_ultimosResultados));

            // Con carpeta de salida explícita, los omitidos/sin-ganancia no llegan solos
            // a la carpeta de salida durante la compresión. Los copiamos aquí para que el
            // usuario encuentre todos sus archivos en un solo sitio sin tener que pulsar
            // "Descargar" manualmente.
            ReunirOmitidosAutomaticamente(preferenciasLote);
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

    /// <summary>Reemplaza en <paramref name="previos"/> las entradas cuyo origen aparece en
    /// <paramref name="nuevos"/> (un reintento), conservando el orden y el resto.</summary>
    private static IReadOnlyList<ResultadoCompresion> FusionarResultados(
        IReadOnlyList<ResultadoCompresion> previos, IReadOnlyList<ResultadoCompresion> nuevos)
    {
        if (previos.Count == 0) return nuevos;

        var porRuta = nuevos.ToDictionary(r => r.Origen.RutaCompleta, StringComparer.OrdinalIgnoreCase);
        return [.. previos.Select(p =>
            porRuta.TryGetValue(p.Origen.RutaCompleta, out var reintentado) ? reintentado : p)];
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
        _ultimosResultados = [];
        _carpetaResultados = null;
        MensajeEstado = "Lista vacía. Arrastra PDFs aquí.";
        MensajeInformativo = "";
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

    /// <summary>
    /// Cuando hay carpeta de salida explícita (SalidaJuntoAlOriginal=false), los archivos
    /// omitidos/sin-ganancia no llegan ahí solos durante la compresión: Ghostscript sólo
    /// actúa sobre los que superan el umbral. Este método los copia automáticamente al
    /// terminar el lote para que el usuario encuentre todos sus archivos en un solo sitio.
    /// </summary>
    private void ReunirOmitidosAutomaticamente(PreferenciasUsuario prefs)
    {
        if (prefs.SalidaJuntoAlOriginal || string.IsNullOrWhiteSpace(prefs.CarpetaSalida))
            return;

        var hayOmitidos = _ultimosResultados.Any(
            r => r.Estado is EstadoCompresion.Omitido or EstadoCompresion.SinGanancia);
        if (!hayOmitidos) return;

        try
        {
            var copiados = _servicio.CopiarComprimidos(_ultimosResultados, prefs.CarpetaSalida);
            _carpetaResultados ??= prefs.CarpetaSalida;
            AbrirCarpetaResultadosCommand.NotifyCanExecuteChanged();

            var resumen = ResumenLote.De(_ultimosResultados);
            var totalDesc = resumen.Comprimidos + resumen.Omitidos;
            MensajeInformativo = copiados.Count == 0
                ? $"Proceso terminado: {totalDesc} archivo(s) en {prefs.CarpetaSalida} " +
                  $"({resumen.Comprimidos} comprimidos + {resumen.Omitidos} sin cambios ya presentes)" +
                  (resumen.Fallidos > 0 ? $" · {resumen.Fallidos} con problemas" : "") + "."
                : $"Proceso terminado: {totalDesc} archivo(s) reunidos en {prefs.CarpetaSalida} " +
                  $"({resumen.Comprimidos} comprimidos + {copiados.Count} originales copiados)" +
                  (resumen.Fallidos > 0 ? $" · {resumen.Fallidos} con problemas" : "") + ".";
        }
        catch (Exception ex)
        {
            _registro.Error("Error al reunir los omitidos automáticamente", ex);
        }
    }

    private void MostrarResumen(ResumenLote resumen)
    {
        MensajeEstado = "Proceso terminado.";
        HayResumen = true;
        ResumenTieneProblemas = resumen.Fallidos > 0;
        OnPropertyChanged(nameof(HayFallidos));
        ReintentarFallidosCommand.NotifyCanExecuteChanged();

        AbrirCarpetaResultadosCommand.NotifyCanExecuteChanged();

        if (resumen.Comprimidos == 0)
        {
            AhorroDestacado = "Sin cambios";
            AhorroPorcentaje = "";
            var todosConProblemas = resumen.Fallidos > 0 && resumen.Omitidos == 0;
            DetalleResumen = todosConProblemas
                ? $"{resumen.Total} archivo(s) con problemas."
                : $"{resumen.Total} archivo(s): ninguno superó el umbral" +
                  (resumen.Fallidos > 0 ? $" · {resumen.Fallidos} con problemas." : ".");
            MensajeInformativo = todosConProblemas
                ? $"Sin resultados: {resumen.Fallidos} archivo(s) presentaron problemas."
                : $"Ninguno superó el umbral · {resumen.Omitidos} omitido(s)" +
                  (resumen.Fallidos > 0 ? $" · {resumen.Fallidos} con problemas" : "") + ".";
            return;
        }

        AhorroDestacado = resumen.AhorroLegible;
        AhorroPorcentaje = string.Create(CultureInfo.InvariantCulture, $"-{resumen.PorcentajeAhorro:0.#}%");
        DetalleResumen = $"{resumen.Comprimidos} comprimido(s) · {resumen.Omitidos} omitido(s)" +
                          (resumen.Fallidos > 0 ? $" · {resumen.Fallidos} con problemas" : "");

        var rutaMsg = _carpetaResultados is not null ? $" guardados en {_carpetaResultados}" : "";
        MensajeInformativo = $"Proceso terminado: {resumen.Comprimidos} comprimido(s){rutaMsg}" +
                             $" · {resumen.Omitidos} omitido(s)" +
                             (resumen.Fallidos > 0 ? $" · {resumen.Fallidos} con problemas" : "") + ".";
    }

    /// <summary>Vuelca los ajustes de la UI al modelo y los persiste (RNF-07).</summary>
    public void GuardarPreferencias()
    {
        Preferencias.UmbralMb = Math.Max(0, UmbralMb);
        Preferencias.UnidadUmbral = UnidadUmbral;
        Preferencias.Nivel = Nivel;
        Preferencias.SalidaJuntoAlOriginal = SalidaJuntoAlOriginal;
        Preferencias.CarpetaSalida = string.IsNullOrWhiteSpace(CarpetaSalida) ? null : CarpetaSalida;
        // La opción "Respaldar el original" se retiró de la UI: se fuerza desactivada.
        Preferencias.CrearRespaldo = false;
        Preferencias.SufijoSalida = SufijoSalida?.Trim() ?? "";
        Preferencias.EscalaDeGrises = EscalaDeGrises;
        Preferencias.GradoParalelismo = Math.Clamp(GradoParalelismo, 1, MaxParalelismo);
        Preferencias.RutaGhostscript = string.IsNullOrWhiteSpace(RutaGhostscript) ? null : RutaGhostscript.Trim();
        Preferencias.PanelConfiguracionVisible = PanelConfigVisible;
        Preferencias.TemaOscuro = TemaOscuro;
        _repositorio.Guardar(Preferencias);
    }

    /// <summary>RF-26 / ADR-008: pliega o despliega el panel lateral. Se persiste al instante
    /// para que el estado sobreviva aunque la app se cierre sin comprimir nada.</summary>
    [RelayCommand]
    private void AlternarPanelConfig()
    {
        PanelConfigVisible = !PanelConfigVisible;
        Preferencias.PanelConfiguracionVisible = PanelConfigVisible;
        _repositorio.Guardar(Preferencias);
    }

    /// <summary>Alterna entre tema oscuro y claro. Se persiste al instante; el code-behind
    /// reacciona al cambio de <see cref="TemaOscuro"/> para aplicarlo a Avalonia.</summary>
    [RelayCommand]
    private void AlternarTema()
    {
        TemaOscuro = !TemaOscuro;
        Preferencias.TemaOscuro = TemaOscuro;
        _repositorio.Guardar(Preferencias);
    }

    /// <summary>
    /// RF-30: aplica la ruta manual de Ghostscript sin reiniciar. Reconstruye motor y
    /// orquestador (ver <see cref="_fabricaMotor"/>) y refresca el estado del motor en la UI.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeVolverAComprobarMotor))]
    private void VolverAComprobarMotor()
    {
        Preferencias.RutaGhostscript = string.IsNullOrWhiteSpace(RutaGhostscript) ? null : RutaGhostscript.Trim();
        _repositorio.Guardar(Preferencias);

        (_servicio, _compresor) = _fabricaMotor(Preferencias.RutaGhostscript);

        OnPropertyChanged(nameof(GhostscriptDisponible));
        OnPropertyChanged(nameof(AvisoMotor));
        NotificarComandos();

        MensajeEstado = GhostscriptDisponible
            ? $"Ghostscript detectado: {_compresor.RutaBinario}"
            : "Sigue sin encontrarse Ghostscript. Revisa la ruta indicada.";
    }

    private bool PuedeVolverAComprobarMotor() => !Procesando && !Analizando;

    private void NotificarComandos()
    {
        ComprimirCommand.NotifyCanExecuteChanged();
        CancelarCommand.NotifyCanExecuteChanged();
        LimpiarCommand.NotifyCanExecuteChanged();
        QuitarSeleccionadosCommand.NotifyCanExecuteChanged();
        AbrirCarpetaResultadosCommand.NotifyCanExecuteChanged();
        VolverAComprobarMotorCommand.NotifyCanExecuteChanged();
        ReintentarFallidosCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HayFallidos));
    }
}
