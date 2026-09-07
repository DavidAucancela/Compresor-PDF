namespace CompresorPdf.Core.Diagnostico;

/// <summary>
/// Abstracción mínima de logging (RNF-08). Evita acoplar el Core a un framework de logs.
/// </summary>
public interface IRegistro
{
    void Info(string mensaje);
    void Advertencia(string mensaje);
    void Error(string mensaje, Exception? ex = null);
}

/// <summary>Implementación nula, útil en pruebas.</summary>
public sealed class RegistroNulo : IRegistro
{
    public static readonly RegistroNulo Instancia = new();
    public void Info(string mensaje) { }
    public void Advertencia(string mensaje) { }
    public void Error(string mensaje, Exception? ex = null) { }
}
