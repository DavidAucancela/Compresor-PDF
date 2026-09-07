using CompresorPdf.Core.Config;
using CompresorPdf.Core.Models;
using CompresorPdf.Core.Services;
using CompresorPdf.Tests.Fixtures;
using Xunit;

namespace CompresorPdf.Tests.Servicios;

/// <summary>
/// Pruebas contra el Ghostscript real de la máquina. Se omiten solas si no está instalado,
/// para que el resto de la suite siga corriendo en un CI sin él.
/// </summary>
public class IntegracionGhostscriptTests
{
    private static string? Binario => FactSiHayGhostscriptAttribute.Binario;

    /// <summary>Genera un PDF real invocando a Ghostscript con un fragmento PostScript.</summary>
    private static string GenerarPdfReal(CarpetaTemporal tmp, string nombre, int repeticiones)
    {
        var ruta = tmp.Combinar(nombre);
        var texto = string.Join(" ", Enumerable.Range(0, repeticiones)
            .Select(i => $"72 {700 - i % 600} moveto (Linea de prueba {i} con texto real) show"));

        var proceso = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Binario!,
            ArgumentList =
            {
                "-sDEVICE=pdfwrite", "-dNOPAUSE", "-dQUIET", "-dBATCH",
                $"-sOutputFile={ruta}",
                "-c", $"/Helvetica findfont 11 scalefont setfont {texto} showpage"
            },
            UseShellExecute = false,
            RedirectStandardError = true
        })!;
        proceso.WaitForExit();
        return ruta;
    }

    [FactSiHayGhostscript]
    public void El_localizador_encuentra_ghostscript_si_esta_instalado()
    {
        Assert.True(File.Exists(Binario));
    }

    [FactSiHayGhostscript]
    public async Task El_motor_real_produce_un_pdf_valido_y_no_toca_el_original()
    {

        using var tmp = new CarpetaTemporal();
        var entrada = GenerarPdfReal(tmp, "origen.pdf", repeticiones: 400);
        var bytesAntes = new FileInfo(entrada).Length;
        var salida = tmp.Combinar("salida.pdf");

        var compresor = new CompresorGhostscript(new LocalizadorGhostscript());
        var resultado = await compresor.ComprimirAsync(entrada, salida, PerfilCompresion.Balanceado());

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        Assert.True(File.Exists(salida));

        // La salida es un PDF de verdad.
        using var fs = File.OpenRead(salida);
        var cabecera = new byte[5];
        fs.ReadExactly(cabecera);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(cabecera));

        // RNF-03: el original sigue byte a byte igual.
        Assert.Equal(bytesAntes, new FileInfo(entrada).Length);
    }

    [FactSiHayGhostscript]
    public async Task El_lote_completo_funciona_contra_el_motor_real()
    {

        using var tmp = new CarpetaTemporal();
        var grande = GenerarPdfReal(tmp, "grande.pdf", repeticiones: 400);
        var roto = tmp.CrearArchivoNoPdf("roto.pdf");

        var servicio = new ServicioCompresionLote(
            new CompresorGhostscript(new LocalizadorGhostscript()));

        // Umbral 0 para forzar que el PDF real entre a compresión.
        var prefs = new PreferenciasUsuario { UmbralMb = 0, SalidaJuntoAlOriginal = true };
        var resultados = await servicio.ProcesarAsync(servicio.Preparar([grande, roto]), prefs);

        var resultadoGrande = resultados.Single(r => r.Origen.Nombre == "grande.pdf");
        var resultadoRoto = resultados.Single(r => r.Origen.Nombre == "roto.pdf");

        // Comprimido o ya-optimizado: ambos son desenlaces correctos; lo que no vale es fallar.
        Assert.True(
            resultadoGrande.Estado is EstadoCompresion.Comprimido or EstadoCompresion.SinGanancia,
            $"Estado inesperado: {resultadoGrande.Estado} — {resultadoGrande.Mensaje}");

        Assert.Equal(EstadoCompresion.Corrupto, resultadoRoto.Estado);
    }
}
