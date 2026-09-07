using CompresorPdf.Core.Models;

namespace CompresorPdf.Core.Services;

/// <summary>
/// Contrato del motor de compresión. La UI y el orquestador sólo conocen esta interfaz,
/// de modo que cambiar Ghostscript por otra librería no obliga a tocar nada más
/// (ver docs/02-DECISIONES-ADR.md, ADR-002).
/// </summary>
public interface ICompresorPdf
{
    /// <summary>Nombre del motor, para mostrarlo en la UI y en los logs.</summary>
    string Nombre { get; }

    /// <summary>True si el motor está disponible en esta máquina.</summary>
    bool EstaDisponible { get; }

    /// <summary>
    /// Comprime <paramref name="rutaEntrada"/> escribiendo en <paramref name="rutaSalida"/>.
    /// No modifica la entrada bajo ninguna circunstancia (RNF-03).
    /// </summary>
    Task<ResultadoMotor> ComprimirAsync(
        string rutaEntrada,
        string rutaSalida,
        PerfilCompresion perfil,
        CancellationToken ct = default);
}

/// <summary>Resultado crudo del motor, antes de convertirlo en <see cref="ResultadoCompresion"/>.</summary>
public sealed record ResultadoMotor(bool Exitoso, string? Mensaje)
{
    public static ResultadoMotor Ok() => new(true, null);
    public static ResultadoMotor Fallo(string mensaje) => new(false, mensaje);
}
