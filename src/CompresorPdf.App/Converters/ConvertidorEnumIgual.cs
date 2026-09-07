using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace CompresorPdf.App.Converters;

/// <summary>
/// Compara un valor de enum contra el nombre pasado como <c>ConverterParameter</c>. Es lo que
/// permite implementar el selector segmentado de nivel (sección 5.4) con tres
/// <c>ToggleButton</c> normales en vez de un control de terceros: cada uno se marca cuando
/// <c>Nivel</c> coincide con su parámetro, y al pulsarlo pone ese valor en el ViewModel.
/// </summary>
public sealed class ConvertidorEnumIgual : IValueConverter
{
    public static readonly ConvertidorEnumIgual Instancia = new();

    public object Convert(object? valor, Type tipoDestino, object? parametro, CultureInfo cultura) =>
        valor is not null && parametro is string nombre &&
        string.Equals(valor.ToString(), nombre, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? valor, Type tipoDestino, object? parametro, CultureInfo cultura)
    {
        // Sólo el ToggleButton que pasa a marcado dispara el cambio; el que se desmarca
        // (el que estaba seleccionado antes) no debe pisar el valor con "false".
        if (valor is true && parametro is string nombre && tipoDestino.IsEnum)
            return Enum.Parse(tipoDestino, nombre, ignoreCase: true);

        return BindingOperations.DoNothing;
    }
}
