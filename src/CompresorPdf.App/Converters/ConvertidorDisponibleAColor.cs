using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace CompresorPdf.App.Converters;

/// <summary>El punto de estado del motor en el panel de ajustes: verde si Ghostscript
/// está disponible, ámbar si no. Resuelve los tokens de Semi.Avalonia en vez de un hex
/// suelto, para que respete el tema claro/oscuro.</summary>
public sealed class ConvertidorDisponibleAColor : IValueConverter
{
    public static readonly ConvertidorDisponibleAColor Instancia = new();

    public object? Convert(object? valor, Type tipoDestino, object? parametro, CultureInfo cultura)
    {
        var clave = valor is true ? "SemiColorSuccess" : "SemiColorWarning";
        return Avalonia.Application.Current?.TryFindResource(clave, out var recurso) == true
            ? recurso
            : null;
    }

    public object ConvertBack(object? valor, Type tipoDestino, object? parametro, CultureInfo cultura) =>
        throw new NotSupportedException();
}
