# Arquitectura

## Principio rector

**La lógica de compresión no sabe que existe una interfaz gráfica.** Todo lo que importa
vive en `CompresorPdf.Core`, un proyecto sin una sola referencia a Avalonia. Esto compra
tres cosas concretas:

1. Se puede probar el sistema entero sin abrir una ventana (así están escritas las 43 pruebas).
2. Añadir una CLI o un servicio de carpeta vigilada (RF-15) no duplica lógica.
3. Cambiar de framework de UI no toca el motor.

## Mapa de proyectos

```
CompresorPdf.slnx
│
├── src/CompresorPdf.Core        ← sin dependencias externas (sólo BCL)
│   ├── Models/                  ArchivoPdf, ResultadoCompresion, PerfilCompresion,
│   │                            NivelCompresion, EstadoCompresion, ProgresoLote,
│   │                            ProgresoAnalisis, UnidadTamano
│   ├── Services/
│   │   ├── ICompresorPdf        contrato del motor
│   │   ├── CompresorGhostscript implementación vía proceso externo
│   │   ├── IEjecutorProceso     abstracción de Process (permite probar sin gs)
│   │   ├── ILocalizadorGhostscript / LocalizadorGhostscript
│   │   ├── IAnalizadorPdf / AnalizadorPdf
│   │   ├── IGestorArchivos / GestorArchivos
│   │   ├── ServicioCompresionLote  orquestador
│   │   └── ResumenLote          totales del lote
│   ├── Config/                  PreferenciasUsuario + repositorio JSON
│   └── Diagnostico/             IRegistro, RegistroArchivo, RutasApp
│
├── src/CompresorPdf.App         ← Avalonia 11, patrón MVVM
│   ├── Program.cs / App.axaml   arranque y composición de dependencias
│   ├── ViewModels/              MainWindowViewModel, FilaResultadoViewModel
│   ├── Views/                   MainWindow (drag&drop y diálogos de archivo)
│   └── Converters/
│
└── tests/CompresorPdf.Tests     ← xunit; 40 unitarias + 3 de integración real
```

## El flujo, de arriba abajo

```
 Usuario suelta archivos
         │
         ▼
 MainWindow.AlSoltar ──────────► MainWindowViewModel.AgregarRutasAsync
                                          │
                                          ▼
                         ServicioCompresionLote.Preparar
                                          │
                            GestorArchivos.FiltrarPdfs   (¿es .pdf? ¿existe? ¿duplicado?
                                          │               ¿es carpeta? → expandir)
                                          ▼
                            AnalizadorPdf.Analizar        (tamaño, /Encrypt, %PDF-, /Font)
                                          │
                                          ▼
                                   List<ArchivoPdf>  ──► una fila por archivo en la tabla

 Usuario pulsa «Comprimir»
         │
         ▼
 ServicioCompresionLote.ProcesarAsync   (Parallel.ForAsync, N en paralelo, CancellationToken)
         │
         ├── ¿corrupto?   → Corrupto
         ├── ¿protegido?  → Protegido
         ├── ¿≤ umbral?   → Omitido            ← RF-03, no se llama al motor
         │
         ├── GestorArchivos.CrearRespaldo      ← sólo si el usuario lo pidió
         ├── GestorArchivos.ResolverRutaSalida ← nunca devuelve la ruta del original
         │
         ▼
 ICompresorPdf.ComprimirAsync
         │
         └── CompresorGhostscript → IEjecutorProceso → gs -sDEVICE=pdfwrite …
                                          │
                                          ▼
                       ¿el resultado es más pequeño?
                         sí → Comprimido       no → se borra la salida, SinGanancia
                                          │
                                          ▼
                            IProgress<ProgresoLote> → la fila se actualiza en la UI
```

## Decisiones estructurales que conviene conocer

### El motor está detrás de una interfaz, y el `Process` también

`CompresorGhostscript` no llama a `Process.Start` directamente: depende de `IEjecutorProceso`.
Gracias a eso las pruebas verifican los argumentos exactos que se le pasarían a Ghostscript
sin necesitar Ghostscript. Las tres pruebas que sí lo usan de verdad están marcadas con
`[FactSiHayGhostscript]` y se saltan solas en una máquina sin él.

### El análisis del PDF es deliberadamente heurístico

`AnalizadorPdf` no parsea el formato: lee los primeros 4 MB y busca marcadores
(`%PDF-`, `/Encrypt`, `/Font`, `/Image`). Es suficiente para **decidir cómo tratar** un
archivo y no añade dependencias. Cuando haga falta precisión (contar imágenes reales,
vista previa de página — RF-11), se sustituye la implementación: los consumidores dependen
de `IAnalizadorPdf`, no de la clase.

Limitación conocida: un PDF con la capa de texto muy al final de un archivo enorme podría
clasificarse como "escaneado" por error. El único efecto es un aviso de más en la UI.

### El orquestador convierte excepciones en resultados

`ServicioCompresionLote` captura todo lo que no sea una cancelación y lo traduce a un
`ResultadoCompresion` con estado `Error`. Un archivo roto jamás detiene el lote (RNF-02).
La cancelación sí se propaga, y al recogerla marca como `Cancelado` todo lo que quedaba
pendiente, para que la tabla no deje filas en un estado mentiroso.

### No hay contenedor de inyección de dependencias

El grafo se construye a mano en `App.axaml.cs` (unas diez líneas). Con este tamaño, un
contenedor añadiría configuración e indirección sin resolver ningún problema real. Si el
proyecto crece hasta necesitarlo, el cambio está localizado en ese único método.
