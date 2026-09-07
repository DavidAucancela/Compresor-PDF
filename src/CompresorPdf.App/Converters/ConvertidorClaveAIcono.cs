using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CompresorPdf.App.Converters;

/// <summary>
/// Resuelve la clave de ícono de una fila (p. ej. "IconoCheck") contra las geometrías
/// definidas en Styles/Iconos.axaml. Al ser recursos estáticos (no dependen del tema
/// claro/oscuro), no hace falta re-evaluar nada cuando cambia el tema — a diferencia del
/// color de estado, que se resuelve con Classes + DynamicResource (ver
/// FilaResultadoViewModel y Styles/Controles.axaml) precisamente para que sí reaccione.
/// </summary>
public sealed class ConvertidorClaveAIcono : IValueConverter
{
    public static readonly ConvertidorClaveAIcono Instancia = new();

    public object? Convert(object? valor, Type tipoDestino, object? parametro, CultureInfo cultura)
    {
        if (valor is not string clave || string.IsNullOrEmpty(clave)) return null;

        return Avalonia.Application.Current?.TryFindResource(clave, out var recurso) == true
            ? recurso as Geometry
            : null;
    }

    public object ConvertBack(object? valor, Type tipoDestino, object? parametro, CultureInfo cultura) =>
        throw new NotSupportedException();
}
