namespace CompresorPdf.Core.Services;

/// <summary>Resuelve dónde está el binario de Ghostscript en esta máquina.</summary>
public interface ILocalizadorGhostscript
{
    /// <summary>Ruta al ejecutable, o null si no se encuentra.</summary>
    string? Localizar();
}
