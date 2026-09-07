namespace CompresorPdf.Core.Services;

/// <summary>Salida de un proceso externo ya finalizado.</summary>
public sealed record ResultadoProceso(int CodigoSalida, string SalidaEstandar, string SalidaError)
{
    public bool Exitoso => CodigoSalida == 0;
}

/// <summary>
/// Ejecuta binarios externos. Existe como interfaz para poder probar
/// <see cref="CompresorGhostscript"/> sin tener Ghostscript instalado.
/// </summary>
public interface IEjecutorProceso
{
    Task<ResultadoProceso> EjecutarAsync(
        string ejecutable,
        IReadOnlyList<string> argumentos,
        CancellationToken ct = default);
}
