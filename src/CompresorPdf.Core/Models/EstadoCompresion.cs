namespace CompresorPdf.Core.Models;

/// <summary>Resultado final del procesamiento de un archivo dentro del lote.</summary>
public enum EstadoCompresion
{
    /// <summary>Aún no procesado.</summary>
    Pendiente,

    /// <summary>En proceso en este momento.</summary>
    Procesando,

    /// <summary>Comprimido correctamente y con ganancia de tamaño.</summary>
    Comprimido,

    /// <summary>No superaba el umbral configurado (RF-03).</summary>
    Omitido,

    /// <summary>Se comprimió pero el resultado no era menor: se conserva el original.</summary>
    SinGanancia,

    /// <summary>PDF protegido con contraseña (RF-12).</summary>
    Protegido,

    /// <summary>Archivo ilegible o no es un PDF válido (RF-12).</summary>
    Corrupto,

    /// <summary>El usuario canceló el lote (RF-13).</summary>
    Cancelado,

    /// <summary>Error inesperado durante el proceso.</summary>
    Error
}
