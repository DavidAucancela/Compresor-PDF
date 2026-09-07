using System.Globalization;

namespace CompresorPdf.Core.Diagnostico;

/// <summary>
/// Log en texto plano dentro de la carpeta de datos de la aplicación (RNF-08).
/// Serializa las escrituras: el lote corre en paralelo.
/// </summary>
public sealed class RegistroArchivo : IRegistro
{
    private readonly string _ruta;
    private readonly Lock _candado = new();

    public RegistroArchivo(string? rutaArchivo = null)
    {
        _ruta = rutaArchivo ?? Path.Combine(RutasApp.CarpetaDatos, "compresor.log");
        Directory.CreateDirectory(Path.GetDirectoryName(_ruta)!);
    }

    public string Ruta => _ruta;

    public void Info(string mensaje) => Escribir("INFO", mensaje);
    public void Advertencia(string mensaje) => Escribir("WARN", mensaje);

    public void Error(string mensaje, Exception? ex = null) =>
        Escribir("ERROR", ex is null ? mensaje : $"{mensaje} :: {ex.GetType().Name}: {ex.Message}");

    private void Escribir(string nivel, string mensaje)
    {
        var linea = string.Create(CultureInfo.InvariantCulture,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{nivel}] {mensaje}");
        try
        {
            lock (_candado)
            {
                File.AppendAllText(_ruta, linea + Environment.NewLine);
            }
        }
        catch
        {
            // El logging nunca debe tumbar la aplicación.
        }
    }
}
