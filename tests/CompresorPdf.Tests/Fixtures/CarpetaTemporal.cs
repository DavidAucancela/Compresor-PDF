namespace CompresorPdf.Tests.Fixtures;

/// <summary>Carpeta descartable para pruebas que tocan disco. Se borra al liberarse.</summary>
public sealed class CarpetaTemporal : IDisposable
{
    public CarpetaTemporal()
    {
        Ruta = Path.Combine(Path.GetTempPath(), "compresorpdf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Ruta);
    }

    public string Ruta { get; }

    public string Combinar(params string[] partes) => Path.Combine([Ruta, .. partes]);

    /// <summary>Escribe un PDF sintético mínimo pero con cabecera válida y el tamaño pedido.</summary>
    public string CrearPdf(string nombre, int bytes = 1024, bool protegido = false, bool conFuentes = true)
    {
        var ruta = Combinar(nombre);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);

        var cabecera = "%PDF-1.7\n";
        var cuerpo = "1 0 obj << /Type /Page >> endobj\n";
        if (conFuentes) cuerpo += "2 0 obj << /Font << /F1 3 0 R >> >> endobj\n";
        else cuerpo += "2 0 obj << /XObject << /Im1 4 0 R /Subtype /Image >> >> endobj\n";
        if (protegido) cuerpo += "trailer << /Encrypt 9 0 R >>\n";

        var relleno = new string('x', Math.Max(0, bytes - cabecera.Length - cuerpo.Length));
        File.WriteAllText(ruta, cabecera + cuerpo + relleno);
        return ruta;
    }

    public string CrearArchivoNoPdf(string nombre, int bytes = 512)
    {
        var ruta = Combinar(nombre);
        File.WriteAllText(ruta, new string('z', bytes));
        return ruta;
    }

    public void Dispose()
    {
        try { Directory.Delete(Ruta, recursive: true); } catch { /* limpieza best-effort */ }
    }
}
