using CompresorPdf.Core.Config;
using CompresorPdf.Core.Models;
using CompresorPdf.Core.Services;
using CompresorPdf.Tests.Fixtures;
using Xunit;

namespace CompresorPdf.Tests.Servicios;

public class GestorArchivosTests
{
    private readonly GestorArchivos _gestor = new();

    private static ArchivoPdf Archivo(string ruta) =>
        new() { RutaCompleta = ruta, TamanoBytes = new FileInfo(ruta).Length };

    [Fact]
    public void Salida_por_defecto_va_a_subcarpeta_comprimidos()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.CrearPdf("doc.pdf");
        var prefs = new PreferenciasUsuario { SalidaJuntoAlOriginal = true };

        var salida = _gestor.ResolverRutaSalida(Archivo(ruta), prefs);

        Assert.Equal(tmp.Combinar("comprimidos", "doc.pdf"), salida);
        Assert.True(Directory.Exists(tmp.Combinar("comprimidos")));
    }

    [Fact]
    public void Nunca_devuelve_la_ruta_del_original()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.CrearPdf("doc.pdf");
        var prefs = new PreferenciasUsuario
        {
            SalidaJuntoAlOriginal = false,
            CarpetaSalida = tmp.Ruta
        };

        var salida = _gestor.ResolverRutaSalida(Archivo(ruta), prefs);

        Assert.NotEqual(ruta, salida);
    }

    [Fact]
    public void No_sobrescribe_un_archivo_de_salida_existente()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.CrearPdf("doc.pdf");
        var prefs = new PreferenciasUsuario { SalidaJuntoAlOriginal = true };

        var primera = _gestor.ResolverRutaSalida(Archivo(ruta), prefs);
        File.WriteAllText(primera, "ocupado");
        var segunda = _gestor.ResolverRutaSalida(Archivo(ruta), prefs);

        Assert.NotEqual(primera, segunda);
        Assert.Contains("(2)", segunda);
    }

    [Fact]
    public void Respaldo_copia_el_original_y_lo_deja_intacto()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.CrearPdf("doc.pdf", bytes: 3000);
        var prefs = new PreferenciasUsuario { CrearRespaldo = true };

        var respaldo = _gestor.CrearRespaldo(Archivo(ruta), prefs);

        Assert.NotNull(respaldo);
        Assert.True(File.Exists(respaldo));
        Assert.True(File.Exists(ruta));
        Assert.Equal(3000, new FileInfo(ruta).Length);
    }

    [Fact]
    public void Sin_la_preferencia_activa_no_crea_respaldo()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.CrearPdf("doc.pdf");

        Assert.Null(_gestor.CrearRespaldo(Archivo(ruta), new PreferenciasUsuario { CrearRespaldo = false }));
    }

    [Fact]
    public void Filtrar_descarta_no_pdfs_y_duplicados()
    {
        using var tmp = new CarpetaTemporal();
        var pdf = tmp.CrearPdf("a.pdf");
        var txt = tmp.Combinar("b.txt");
        File.WriteAllText(txt, "hola");

        var filtrados = _gestor.FiltrarPdfs([pdf, pdf, txt, "/no/existe.pdf"]);

        Assert.Single(filtrados);
        Assert.Equal(pdf, filtrados[0]);
    }

    [Fact]
    public void Filtrar_expande_carpetas_recursivamente()
    {
        using var tmp = new CarpetaTemporal();
        tmp.CrearPdf("raiz.pdf");
        tmp.CrearPdf(Path.Combine("sub", "anidado.pdf"));

        var filtrados = _gestor.FiltrarPdfs([tmp.Ruta]);

        Assert.Equal(2, filtrados.Count);
    }

    // RF-24b: cuando los PDFs vienen de carpetas distintas, el lote se redirige a una
    // carpeta consolidada para que los comprimidos no queden desperdigados.

    [Fact]
    public void TieneOrigenesMixtos_devuelve_true_cuando_hay_mas_de_una_carpeta_origen()
    {
        var archivos = new List<ArchivoPdf>
        {
            new() { RutaCompleta = Path.Combine("carpeta1", "a.pdf"), TamanoBytes = 0 },
            new() { RutaCompleta = Path.Combine("carpeta2", "b.pdf"), TamanoBytes = 0 },
        };

        Assert.True(_gestor.TieneOrigenesMixtos(archivos));
    }

    [Fact]
    public void TieneOrigenesMixtos_devuelve_false_cuando_todos_vienen_del_mismo_directorio()
    {
        var archivos = new List<ArchivoPdf>
        {
            new() { RutaCompleta = Path.Combine("misma", "a.pdf"), TamanoBytes = 0 },
            new() { RutaCompleta = Path.Combine("misma", "b.pdf"), TamanoBytes = 0 },
            new() { RutaCompleta = Path.Combine("misma", "c.pdf"), TamanoBytes = 0 },
        };

        Assert.False(_gestor.TieneOrigenesMixtos(archivos));
    }

    // RF-33: "Descargar comprimidos" reúne en una sola carpeta los PDF ya comprimidos.

    [Fact]
    public void CopiarA_reune_los_archivos_en_la_carpeta_destino()
    {
        using var tmp = new CarpetaTemporal();
        var a = tmp.CrearPdf(Path.Combine("uno", "comprimidos", "a.pdf"));
        var b = tmp.CrearPdf(Path.Combine("dos", "comprimidos", "b.pdf"));
        var destino = tmp.Combinar("salida");

        var copiados = _gestor.CopiarA([a, b], destino);

        Assert.Equal(2, copiados.Count);
        Assert.True(File.Exists(tmp.Combinar("salida", "a.pdf")));
        Assert.True(File.Exists(tmp.Combinar("salida", "b.pdf")));
        // Los originales no se tocan (no es "mover").
        Assert.True(File.Exists(a));
        Assert.True(File.Exists(b));
    }

    [Fact]
    public void CopiarA_omite_los_que_ya_estan_en_la_carpeta_destino()
    {
        using var tmp = new CarpetaTemporal();
        var yaEnDestino = tmp.CrearPdf(Path.Combine("salida", "a.pdf"));
        var fuera = tmp.CrearPdf(Path.Combine("otra", "b.pdf"));

        var copiados = _gestor.CopiarA([yaEnDestino, fuera], tmp.Combinar("salida"));

        Assert.Single(copiados);
        Assert.EndsWith("b.pdf", copiados[0]);
    }

    [Fact]
    public void CopiarA_no_pisa_un_nombre_que_ya_existe_en_el_destino()
    {
        using var tmp = new CarpetaTemporal();
        var origen = tmp.CrearPdf(Path.Combine("origen", "a.pdf"), bytes: 2000);
        tmp.CrearPdf(Path.Combine("salida", "a.pdf"), bytes: 10);   // ocupa el nombre

        var copiados = _gestor.CopiarA([origen], tmp.Combinar("salida"));

        Assert.Single(copiados);
        Assert.Contains("(2)", copiados[0]);
        Assert.Equal(2000, new FileInfo(copiados[0]).Length);
    }
}
