namespace CompresorPdf.Core.Models;

/// <summary>
/// Unidad en la que el usuario prefiere ver y editar el umbral de compresión (RF-25).
/// No cambia el umbral efectivo, sólo cómo se muestra: el valor canónico persistido sigue
/// siendo <see cref="Config.PreferenciasUsuario.UmbralMb"/> en megabytes.
/// </summary>
public enum UnidadTamano
{
    KB,
    MB
}
