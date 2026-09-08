using CompresorPdf.Core.Config;
using CompresorPdf.Core.Models;
using CompresorPdf.Core.Services;
using CompresorPdf.Tests.Fixtures;
using Xunit;

namespace CompresorPdf.Tests.Servicios;

public class CompresorGhostscriptTests
{
    [Theory]
    [InlineData(NivelCompresion.Alto, "/screen")]
    [InlineData(NivelCompresion.Medio, "/ebook")]
    [InlineData(NivelCompresion.Bajo, "/printer")]
    public void Cada_nivel_mapea_a_su_PDFSETTINGS(NivelCompresion nivel, string esperado)
    {
        // Este es el contrato del RF-10 descrito en la sección 6 del plan.
        Assert.Equal(esperado, CompresorGhostscript.PdfSettingsDe(nivel));
    }

    [Fact]
    public void Los_argumentos_incluyen_entrada_salida_y_modo_seguro()
    {
        var args = CompresorGhostscript.ConstruirArgumentos(
            "/in/doc.pdf", "/out/doc.pdf", PerfilCompresion.Balanceado());

        Assert.Contains("-sDEVICE=pdfwrite", args);
        Assert.Contains("-dSAFER", args);
        Assert.Contains("-dBATCH", args);
        Assert.Contains("-sOutputFile=/out/doc.pdf", args);
        Assert.Equal("/in/doc.pdf", args[^1]);
    }

    [Fact]
    public void El_perfil_Email_fuerza_el_dpi_configurado()
    {
        var args = CompresorGhostscript.ConstruirArgumentos("/in.pdf", "/out.pdf", PerfilCompresion.Email());

        Assert.Contains("-dColorImageResolution=96", args);
        Assert.Contains("-dDownsampleColorImages=true", args);
    }

    [Fact]
    public void Escala_de_grises_anade_la_conversion_de_color()
    {
        var perfil = PerfilCompresion.Balanceado();
        perfil.EscalaDeGrises = true;

        var args = CompresorGhostscript.ConstruirArgumentos("/in.pdf", "/out.pdf", perfil);

        Assert.Contains("-sColorConversionStrategy=Gray", args);
    }

    [Fact]
    public void Los_ajustes_de_usuario_llegan_al_motor_a_traves_de_APerfil()
    {
        // Cadena completa RF-27/28/32: PreferenciasUsuario -> APerfil() -> argumentos de gs.
        // Antes APerfil() sólo copiaba el nivel y estos conmutadores nunca se generaban.
        var perfil = new PreferenciasUsuario
        {
            DpiImagenes = 110,
            EscalaDeGrises = true,
            NivelCompatibilidad = "1.6"
        }.APerfil();

        var args = CompresorGhostscript.ConstruirArgumentos("/in.pdf", "/out.pdf", perfil);

        Assert.Contains("-dColorImageResolution=110", args);
        Assert.Contains("-dDownsampleColorImages=true", args);
        Assert.Contains("-sColorConversionStrategy=Gray", args);
        Assert.Contains("-dCompatibilityLevel=1.6", args);
    }

    [Fact]
    public async Task Sin_binario_disponible_devuelve_un_mensaje_accionable()
    {
        var compresor = new CompresorGhostscript(new LocalizadorFijo(null));

        Assert.False(compresor.EstaDisponible);

        var resultado = await compresor.ComprimirAsync("/a.pdf", "/b.pdf", PerfilCompresion.Balanceado());

        Assert.False(resultado.Exitoso);
        Assert.Contains("Ghostscript", resultado.Mensaje);
    }

    [Fact]
    public async Task Un_codigo_de_salida_distinto_de_cero_se_reporta_como_fallo()
    {
        using var tmp = new CarpetaTemporal();
        var entrada = tmp.CrearPdf("in.pdf");
        var espia = new EjecutorEspia(codigoSalida: 1, salidaError: "Error: /undefinedfilename");
        var compresor = new CompresorGhostscript(new LocalizadorFijo("/bin/gs"), espia);

        var resultado = await compresor.ComprimirAsync(
            entrada, tmp.Combinar("out.pdf"), PerfilCompresion.Balanceado());

        Assert.False(resultado.Exitoso);
        Assert.Contains("undefinedfilename", resultado.Mensaje);
    }

    [Fact]
    public async Task Si_el_motor_no_genera_salida_no_se_reporta_exito()
    {
        using var tmp = new CarpetaTemporal();
        var entrada = tmp.CrearPdf("in.pdf");
        var espia = new EjecutorEspia { CrearSalida = false };
        var compresor = new CompresorGhostscript(new LocalizadorFijo("/bin/gs"), espia);

        var resultado = await compresor.ComprimirAsync(
            entrada, tmp.Combinar("out.pdf"), PerfilCompresion.Balanceado());

        Assert.False(resultado.Exitoso);
    }
}
