using CommunityToolkit.Mvvm.ComponentModel;
using CompresorPdf.Core.Models;

namespace CompresorPdf.App.ViewModels;

/// <summary>Una fila de la lista de resultados (RF-07).</summary>
public sealed partial class FilaResultadoViewModel : ObservableObject
{
    public FilaResultadoViewModel(ArchivoPdf archivo)
    {
        Archivo = archivo;
        Estado = archivo.EsCorrupto ? EstadoCompresion.Corrupto : EstadoCompresion.Pendiente;
        Mensaje = archivo.DetalleAnalisis ?? "";
    }

    public ArchivoPdf Archivo { get; }

    public string Nombre => Archivo.Nombre;
    public string Carpeta => Path.GetDirectoryName(Archivo.RutaCompleta) ?? "";
    public string TamanoOriginal => ArchivoPdf.FormatearTamano(Archivo.TamanoBytes);

    [ObservableProperty] private bool _seleccionada;       // RF-22
    [ObservableProperty] private EstadoCompresion _estado;
    [ObservableProperty] private string _tamanoFinal = "—";
    [ObservableProperty] private string _reduccion = "";
    [ObservableProperty] private string _mensaje = "";

    public string EstadoTexto => Estado switch
    {
        EstadoCompresion.Pendiente => "Pendiente",
        EstadoCompresion.Procesando => "Procesando…",
        EstadoCompresion.Comprimido => "Comprimido",
        EstadoCompresion.Omitido => "Omitido",
        EstadoCompresion.SinGanancia => "Ya optimizado",
        EstadoCompresion.Protegido => "Protegido",
        EstadoCompresion.Corrupto => "No válido",
        EstadoCompresion.Cancelado => "Cancelado",
        EstadoCompresion.Error => "Error",
        _ => Estado.ToString()
    };

    /// <summary>
    /// Clave de ícono (Styles/Iconos.axaml), resuelta por <see cref="Converters.ConvertidorClaveAIcono"/>.
    /// Vacía para "Pendiente": no hay nada útil que mostrar todavía.
    /// </summary>
    public string IconoClave => Estado switch
    {
        EstadoCompresion.Procesando => "IconoEspiral",
        EstadoCompresion.Comprimido => "IconoCheck",
        EstadoCompresion.Omitido or EstadoCompresion.SinGanancia => "IconoMinus",
        EstadoCompresion.Protegido => "IconoCandado",
        EstadoCompresion.Corrupto or EstadoCompresion.Error => "IconoAdvertencia",
        EstadoCompresion.Cancelado => "IconoCancelado",
        _ => ""
    };

    public bool TieneIcono => IconoClave.Length > 0;

    // Clases de estado semánticas (sección 4.1 del plan de diseño): la vista las conmuta vía
    // Classes.<nombre>="{Binding EsX}" y Styles/Controles.axaml + los tokens de Semi.Avalonia
    // resuelven el color real — así el ViewModel no conoce un solo valor hex (ADR-007).
    public bool EsExito => Estado is EstadoCompresion.Comprimido;
    public bool EsNeutral => Estado is EstadoCompresion.Omitido or EstadoCompresion.SinGanancia;
    public bool EsAviso => Estado is EstadoCompresion.Cancelado;
    public bool EsPeligro => Estado is EstadoCompresion.Corrupto or EstadoCompresion.Error
                                     or EstadoCompresion.Protegido;
    public bool EsAcento => Estado is EstadoCompresion.Procesando;

    partial void OnEstadoChanged(EstadoCompresion value)
    {
        OnPropertyChanged(nameof(EstadoTexto));
        OnPropertyChanged(nameof(IconoClave));
        OnPropertyChanged(nameof(TieneIcono));
        OnPropertyChanged(nameof(EsExito));
        OnPropertyChanged(nameof(EsNeutral));
        OnPropertyChanged(nameof(EsAviso));
        OnPropertyChanged(nameof(EsPeligro));
        OnPropertyChanged(nameof(EsAcento));
    }

    public void Aplicar(ResultadoCompresion resultado)
    {
        Estado = resultado.Estado;
        // Sólo mostramos un tamaño final si de verdad se entregó un archivo. En "Omitido" /
        // "Ya optimizado" / "Error" el usuario se queda con el original, y un "→ 149 KB" ahí
        // confunde (parece que el archivo creció o que hubo un fallo silencioso).
        TamanoFinal = resultado.RutaSalida is not null ? resultado.TamanoFinalLegible : "—";
        Reduccion = resultado.ReduccionLegible == "—" ? "" : resultado.ReduccionLegible;
        Mensaje = resultado.Mensaje ?? "";
    }
}
