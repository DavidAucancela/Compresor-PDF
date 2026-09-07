using System.Diagnostics;
using System.Text;

namespace CompresorPdf.Core.Services;

/// <summary>
/// Implementación real sobre <see cref="Process"/>. Si se cancela, mata el árbol de procesos
/// para que Ghostscript no quede huérfano escribiendo un PDF a medias (RF-13).
/// </summary>
public sealed class EjecutorProceso : IEjecutorProceso
{
    public async Task<ResultadoProceso> EjecutarAsync(
        string ejecutable,
        IReadOnlyList<string> argumentos,
        CancellationToken ct = default)
    {
        var inicio = new ProcessStartInfo
        {
            FileName = ejecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in argumentos)
            inicio.ArgumentList.Add(arg);

        using var proceso = new Process { StartInfo = inicio };
        var salida = new StringBuilder();
        var error = new StringBuilder();

        proceso.OutputDataReceived += (_, e) => { if (e.Data is not null) salida.AppendLine(e.Data); };
        proceso.ErrorDataReceived += (_, e) => { if (e.Data is not null) error.AppendLine(e.Data); };

        proceso.Start();
        proceso.BeginOutputReadLine();
        proceso.BeginErrorReadLine();

        try
        {
            await proceso.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TerminarSilenciosamente(proceso);
            throw;
        }

        return new ResultadoProceso(proceso.ExitCode, salida.ToString(), error.ToString());
    }

    private static void TerminarSilenciosamente(Process proceso)
    {
        try
        {
            if (!proceso.HasExited)
                proceso.Kill(entireProcessTree: true);
        }
        catch
        {
            // El proceso ya murió por su cuenta: nada que hacer.
        }
    }
}
