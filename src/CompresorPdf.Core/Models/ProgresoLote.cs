namespace CompresorPdf.Core.Models;

/// <summary>Evento de progreso emitido por el orquestador del lote (RF-08 / RF-09).</summary>
public sealed record ProgresoLote(
    int Procesados,
    int Total,
    string ArchivoActual,
    ResultadoCompresion? UltimoResultado)
{
    public double PorcentajeGlobal => Total == 0 ? 0 : (double)Procesados / Total * 100d;
}
