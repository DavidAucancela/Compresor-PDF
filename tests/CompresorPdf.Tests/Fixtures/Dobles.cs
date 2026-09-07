using CompresorPdf.Core.Models;
using CompresorPdf.Core.Services;

namespace CompresorPdf.Tests.Fixtures;

/// <summary>Localizador que devuelve siempre la ruta que se le indique.</summary>
public sealed class LocalizadorFijo(string? ruta) : ILocalizadorGhostscript
{
    public string? Localizar() => ruta;
}

/// <summary>
/// Motor falso: escribe un archivo de salida del tamaño que decida <paramref name="factorTamano"/>
/// respecto al original, sin necesitar Ghostscript.
/// </summary>
public sealed class CompresorFalso(double factorTamano = 0.5) : ICompresorPdf
{
    public string Nombre => "Falso";
    public bool EstaDisponible { get; set; } = true;
    public int Invocaciones { get; private set; }
    public Func<string, Task>? AlComprimir { get; set; }

    public async Task<ResultadoMotor> ComprimirAsync(
        string rutaEntrada, string rutaSalida, PerfilCompresion perfil, CancellationToken ct = default)
    {
        Invocaciones++;
        if (AlComprimir is not null) await AlComprimir(rutaEntrada);
        ct.ThrowIfCancellationRequested();

        var bytesOrigen = new FileInfo(rutaEntrada).Length;
        var destino = (int)Math.Max(1, bytesOrigen * factorTamano);
        File.WriteAllText(rutaSalida, new string('c', destino));
        return ResultadoMotor.Ok();
    }
}

/// <summary>Motor que siempre falla, para verificar RNF-02.</summary>
public sealed class CompresorQueFalla(string mensaje = "boom") : ICompresorPdf
{
    public string Nombre => "Fallo";
    public bool EstaDisponible => true;

    public Task<ResultadoMotor> ComprimirAsync(
        string rutaEntrada, string rutaSalida, PerfilCompresion perfil, CancellationToken ct = default) =>
        Task.FromResult(ResultadoMotor.Fallo(mensaje));
}

/// <summary>Ejecutor que captura los argumentos en lugar de lanzar un proceso real.</summary>
public sealed class EjecutorEspia(int codigoSalida = 0, string salidaError = "") : IEjecutorProceso
{
    public string? Ejecutable { get; private set; }
    public IReadOnlyList<string> Argumentos { get; private set; } = [];

    /// <summary>Si es true, crea el archivo indicado en -sOutputFile (simula éxito real).</summary>
    public bool CrearSalida { get; set; } = true;

    public Task<ResultadoProceso> EjecutarAsync(
        string ejecutable, IReadOnlyList<string> argumentos, CancellationToken ct = default)
    {
        Ejecutable = ejecutable;
        Argumentos = argumentos;

        if (CrearSalida && codigoSalida == 0)
        {
            var salida = argumentos.FirstOrDefault(a => a.StartsWith("-sOutputFile=", StringComparison.Ordinal));
            if (salida is not null)
                File.WriteAllText(salida["-sOutputFile=".Length..], "%PDF-1.7\nfake");
        }

        return Task.FromResult(new ResultadoProceso(codigoSalida, "", salidaError));
    }
}
