namespace CompresorPdf.Core.Models;

/// <summary>
/// Progreso del análisis inicial de un lote (RF-19). Es deliberadamente el mismo espíritu que
/// <see cref="ProgresoLote"/> pero para la fase de "leer y clasificar archivos", que con lotes
/// grandes (~200 PDFs) puede tardar varios segundos y necesita correr fuera del hilo de UI.
/// </summary>
public sealed record ProgresoAnalisis(int Analizados, int Total, string ArchivoActual)
{
    public double PorcentajeGlobal => Total == 0 ? 0 : (double)Analizados / Total * 100d;
}
