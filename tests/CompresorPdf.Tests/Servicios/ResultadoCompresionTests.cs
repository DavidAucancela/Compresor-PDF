using CompresorPdf.Core.Models;
using Xunit;

namespace CompresorPdf.Tests.Servicios;

public class ResultadoCompresionTests
{
    private static ResultadoCompresion Con(long original, long final) => new()
    {
        Origen = new ArchivoPdf { RutaCompleta = "/x.pdf", TamanoBytes = original },
        TamanoFinal = final
    };

    [Fact]
    public void Una_reduccion_insignificante_no_muestra_porcentaje()
    {
        // -0.03 %: a efectos prácticos el archivo no encogió; no queremos un chip de "-0 %".
        Assert.Equal("—", Con(1_000_000, 999_700).ReduccionLegible);
    }

    [Fact]
    public void Una_reduccion_real_se_muestra_con_signo()
    {
        Assert.Equal("-75 %", Con(1_000_000, 250_000).ReduccionLegible);
    }

    [Fact]
    public void Si_el_resultado_no_encoge_no_hay_reduccion()
    {
        Assert.Equal("—", Con(1_000_000, 1_200_000).ReduccionLegible);
    }
}
