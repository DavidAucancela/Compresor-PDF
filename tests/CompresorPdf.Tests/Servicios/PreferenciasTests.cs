using CompresorPdf.Core.Config;
using CompresorPdf.Core.Models;
using CompresorPdf.Tests.Fixtures;
using Xunit;

namespace CompresorPdf.Tests.Servicios;

public class PreferenciasTests
{
    [Fact]
    public void El_umbral_por_defecto_son_2_MB()
    {
        Assert.Equal(2 * 1024 * 1024, new PreferenciasUsuario().UmbralBytes);
    }

    [Fact]
    public void Las_preferencias_sobreviven_a_un_ciclo_guardar_cargar()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.Combinar("prefs.json");
        var repo = new RepositorioPreferenciasJson(ruta);

        repo.Guardar(new PreferenciasUsuario
        {
            UmbralMb = 5.5,
            Nivel = NivelCompresion.Alto,
            CrearRespaldo = true,
            SufijoSalida = "-min",
            UnidadUmbral = UnidadTamano.KB,
            DpiImagenes = 120,
            EscalaDeGrises = true,
            NivelCompatibilidad = "1.5",
            GradoParalelismo = 4,
            PanelConfiguracionVisible = false
        });

        var cargadas = repo.Cargar();

        Assert.Equal(5.5, cargadas.UmbralMb);
        Assert.Equal(NivelCompresion.Alto, cargadas.Nivel);
        Assert.True(cargadas.CrearRespaldo);
        Assert.Equal("-min", cargadas.SufijoSalida);
        Assert.Equal(UnidadTamano.KB, cargadas.UnidadUmbral);
        Assert.Equal(120, cargadas.DpiImagenes);
        Assert.True(cargadas.EscalaDeGrises);
        Assert.Equal("1.5", cargadas.NivelCompatibilidad);
        Assert.Equal(4, cargadas.GradoParalelismo);
        Assert.False(cargadas.PanelConfiguracionVisible);
    }

    [Fact]
    public void El_panel_de_configuracion_arranca_visible_por_defecto()
    {
        Assert.True(new PreferenciasUsuario().PanelConfiguracionVisible);
    }

    [Fact]
    public void Sin_dpi_configurado_APerfil_lo_deja_en_null_para_usar_el_del_nivel()
    {
        var perfil = new PreferenciasUsuario().APerfil();

        Assert.Null(perfil.DpiImagenes);
        Assert.False(perfil.EscalaDeGrises);
        Assert.Equal("1.7", perfil.NivelCompatibilidad);
    }

    [Fact]
    public void APerfil_propaga_dpi_grises_y_compatibilidad_al_motor()
    {
        var perfil = new PreferenciasUsuario
        {
            DpiImagenes = 96,
            EscalaDeGrises = true,
            NivelCompatibilidad = "1.6"
        }.APerfil();

        Assert.Equal(96, perfil.DpiImagenes);
        Assert.True(perfil.EscalaDeGrises);
        Assert.Equal("1.6", perfil.NivelCompatibilidad);
    }

    [Fact]
    public void La_unidad_del_umbral_es_MB_por_defecto()
    {
        Assert.Equal(UnidadTamano.MB, new PreferenciasUsuario().UnidadUmbral);
    }

    [Fact]
    public void Un_json_corrupto_no_rompe_el_arranque()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.Combinar("prefs.json");
        File.WriteAllText(ruta, "{ esto no es json");

        var cargadas = new RepositorioPreferenciasJson(ruta).Cargar();

        Assert.Equal(2.0, cargadas.UmbralMb);
    }

    [Fact]
    public void Sin_archivo_previo_se_devuelven_los_valores_por_defecto()
    {
        using var tmp = new CarpetaTemporal();
        var cargadas = new RepositorioPreferenciasJson(tmp.Combinar("no-existe.json")).Cargar();

        Assert.Equal(NivelCompresion.Medio, cargadas.Nivel);
    }
}
