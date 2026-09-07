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
            UnidadUmbral = UnidadTamano.KB
        });

        var cargadas = repo.Cargar();

        Assert.Equal(5.5, cargadas.UmbralMb);
        Assert.Equal(NivelCompresion.Alto, cargadas.Nivel);
        Assert.True(cargadas.CrearRespaldo);
        Assert.Equal("-min", cargadas.SufijoSalida);
        Assert.Equal(UnidadTamano.KB, cargadas.UnidadUmbral);
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
