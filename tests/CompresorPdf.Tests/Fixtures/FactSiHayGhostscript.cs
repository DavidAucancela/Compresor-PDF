using CompresorPdf.Core.Services;
using Xunit;

namespace CompresorPdf.Tests.Fixtures;

/// <summary>
/// Marca una prueba de integración que necesita Ghostscript instalado. En una máquina
/// sin él la prueba se salta en lugar de fallar, para no romper un CI limpio.
/// </summary>
public sealed class FactSiHayGhostscriptAttribute : FactAttribute
{
    internal static readonly string? Binario = new LocalizadorGhostscript().Localizar();

    public FactSiHayGhostscriptAttribute()
    {
        if (Binario is null)
            Skip = "Ghostscript no está instalado en esta máquina.";
    }
}
