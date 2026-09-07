using CompresorPdf.Core.Services;
using CompresorPdf.Tests.Fixtures;
using Xunit;

namespace CompresorPdf.Tests.Servicios;

public class AnalizadorPdfTests
{
    private readonly AnalizadorPdf _analizador = new();

    [Fact]
    public void Pdf_valido_se_analiza_sin_marcas_de_error()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.CrearPdf("bueno.pdf", bytes: 5000);

        var archivo = _analizador.Analizar(ruta);

        Assert.False(archivo.EsCorrupto);
        Assert.False(archivo.EstaProtegido);
        Assert.Equal(5000, archivo.TamanoBytes);
        Assert.Equal("bueno.pdf", archivo.Nombre);
    }

    [Fact]
    public void Archivo_sin_cabecera_pdf_se_marca_corrupto()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.CrearArchivoNoPdf("falso.pdf");

        var archivo = _analizador.Analizar(ruta);

        Assert.True(archivo.EsCorrupto);
        Assert.NotNull(archivo.DetalleAnalisis);
    }

    [Fact]
    public void Archivo_inexistente_se_marca_corrupto()
    {
        var archivo = _analizador.Analizar("/ruta/que/no/existe.pdf");

        Assert.True(archivo.EsCorrupto);
    }

    [Fact]
    public void Pdf_con_diccionario_Encrypt_se_marca_protegido()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.CrearPdf("protegido.pdf", protegido: true);

        Assert.True(_analizador.Analizar(ruta).EstaProtegido);
    }

    [Fact]
    public void Pdf_con_imagenes_y_sin_fuentes_parece_escaneado()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.CrearPdf("escaneado.pdf", conFuentes: false);

        Assert.True(_analizador.Analizar(ruta).PareceEscaneado);
    }

    [Fact]
    public void Pdf_con_fuentes_no_parece_escaneado()
    {
        using var tmp = new CarpetaTemporal();
        var ruta = tmp.CrearPdf("texto.pdf", conFuentes: true);

        Assert.False(_analizador.Analizar(ruta).PareceEscaneado);
    }
}
