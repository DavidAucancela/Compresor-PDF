using CompresorPdf.Core.Models;

namespace CompresorPdf.Core.Services;

/// <summary>Totales del lote para la barra de resumen de la UI (RF-07).</summary>
public sealed record ResumenLote(
    int Total,
    int Comprimidos,
    int Omitidos,
    int Fallidos,
    long BytesOriginales,
    long BytesFinales)
{
    public long BytesAhorrados => Math.Max(0, BytesOriginales - BytesFinales);

    public double PorcentajeAhorro =>
        BytesOriginales == 0 ? 0 : (double)BytesAhorrados / BytesOriginales * 100d;

    public string AhorroLegible => ArchivoPdf.FormatearTamano(BytesAhorrados);

    public static ResumenLote De(IEnumerable<ResultadoCompresion> resultados)
    {
        var lista = resultados as IList<ResultadoCompresion> ?? [.. resultados];

        var comprimidos = lista.Where(r => r.Estado == EstadoCompresion.Comprimido).ToList();

        return new ResumenLote(
            Total: lista.Count,
            Comprimidos: comprimidos.Count,
            Omitidos: lista.Count(r => r.Estado is EstadoCompresion.Omitido or EstadoCompresion.SinGanancia),
            Fallidos: lista.Count(r => r.Estado is EstadoCompresion.Error
                                                or EstadoCompresion.Corrupto
                                                or EstadoCompresion.Protegido),
            BytesOriginales: comprimidos.Sum(r => r.TamanoOriginal),
            BytesFinales: comprimidos.Sum(r => r.TamanoFinal));
    }
}
