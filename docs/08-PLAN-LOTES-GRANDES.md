# Plan — Fase 5: lotes grandes (selección, totales y exportación)

**Objetivo:** que cargar una carpeta con 200 PDFs sea una experiencia de primera clase, no
sólo "técnicamente soportada". Hoy el `Core` puede procesar 200 archivos sin problema; la
`App` no está pensada para que el usuario los **gestione** cómodamente a esa escala — no hay
selección, no hay un total agregado, y quitar un archivo significa vaciar toda la lista.

Este documento define los requisitos nuevos y, en la sección 4, las limitaciones técnicas
reales que encontré revisando el código actual — es la base para la conversación antes de
construir nada.

---

## 1. Lo que pides, en requisitos concretos

Traduzco tu descripción a requisitos numerados, continuando la numeración de
`PlanCompresorPDF.md` (el último era RF-18):

| RF | Descripción |
|---|---|
| **RF-19** | Cargar una carpeta (o selección múltiple) de ~200 PDFs sin bloquear la interfaz mientras se analizan. |
| **RF-20** | Cada archivo de la lista muestra su información individual (nombre, tamaño, estado, reducción) — ya existe desde la Fase 4, debe seguir funcionando a esta escala. |
| **RF-21** | Fila de **totales agregados**: suma de tamaño original y final de *todo el lote*, actualizada en vivo mientras se comprime (no sólo al terminar). |
| **RF-22** | **Selección** de archivos individuales o en bloque (todos / ninguno / por estado, p. ej. "seleccionar los que fallaron"). |
| **RF-23** | **Quitar** archivos de la lista: uno por uno o los seleccionados, sin tener que vaciar el lote entero con "Limpiar". |
| **RF-24** | Botón **"Abrir carpeta de resultados"**: revela en Finder/Explorador la carpeta donde quedó todo el lote (decisión confirmada, sección 3). |
| **RF-24b** | **Carpeta de salida consolidada** para el lote cuando mezcla archivos de más de un origen (decisión confirmada, sección 3). |
| **RF-25** | **Unidad del umbral configurable (KB/MB).** El campo "Comprimir si supera" hoy sólo admite MB; con lotes grandes y variados hace falta afinar en KB (p. ej. "cualquier PDF mayor a 300 KB"). Ver sección 7. |

**RF-19 y RF-25 ya están implementados y verificados** (ver secciones 4.1 y 6 para el
detalle). El resto (selección, totales en vivo, quitar, salida consolidada, abrir carpeta)
queda especificado en las secciones 3-5 para construir después.

**Verificación real de RF-19:** se generaron 205 PDFs reales con Ghostscript (200 pequeños +
5 de ~2.2 MB) y, por separado, un lote de 150 archivos de ~2.2 MB (323 MB en total) — ambos
cargaron y se listaron correctamente sin ningún error. En este hardware (SSD) el análisis
termina en bien menos de un segundo incluso para 323 MB, así que no hay una demora vistosa
que fotografiar — pero la corrección no depende de la velocidad del disco: `PrepararAsync`
corre dentro de `Task.Run`, así que nunca bloquea el hilo de UI sin importar cuánto tarde
(un disco de red o mecánico sí lo notaría, y ahí es donde el arreglo importa de verdad). Las
43 pruebas automatizadas cubren el análisis async, el reporte de progreso y la cancelación.

## 2. Qué ya tenemos a favor (no se parte de cero)

- La lista ya es un `ItemsControl` con `VirtualizingStackPanel` (Fase 4): mostrar 200 filas
  en pantalla no es un problema de por sí, sólo se renderizan las visibles.
- `ServicioCompresionLote.ProcesarAsync` ya corre en paralelo con `Parallel.ForAsync` y
  reporta progreso por archivo — la mecánica de fondo para lotes grandes ya existe.
- `GestorArchivos.FiltrarPdfs` ya expande carpetas recursivamente y deduplica por ruta —
  soltar una carpeta con 200 PDFs adentro ya "funciona" en el sentido de que los encuentra.
- `ResumenLote.De(...)` ya calcula agregados (bytes originales/finales, ahorro, %) — falta
  exponerlo *durante* el proceso, no sólo al final, y sobre una selección si aplica.

## 3. Decisiones confirmadas

### 3.1 — "Obtener los resultados" = abrir la carpeta (RF-24)

Se descartan el `.zip` y la opción combinada: un botón **"Abrir carpeta de resultados"** que
revela en Finder/Explorador dónde quedó todo el lote. Es la opción más simple
(`Process.Start` con el explorador del SO, sin dependencias nuevas) y la más honesta para una
app 100% offline (RNF-05) — el usuario ya tiene los archivos en su disco, no hace falta
empaquetarlos para "descargarlos" de ningún sitio.

### 3.2 — Carpeta de salida consolidada quando el lote mezcla orígenes (RF-24b)

Hoy (RF-05, ADR-004) cada archivo se comprime **junto a su original**, en una subcarpeta
`comprimidos/`. Si sueltas 200 PDFs que viven en 5 carpetas distintas, hoy terminas con
**5 subcarpetas `comprimidos/` separadas** — y el botón "Abrir carpeta de resultados" de la
3.1 no tendría *una* carpeta que abrir.

Se confirma: cuando el lote mezcla archivos de más de una carpeta de origen, la app usa
**una única carpeta de destino para todo el lote**, en vez de una `comprimidos/` por cada
origen. Esto es un **modo adicional**, no un reemplazo:

- **Un solo origen** (todos los archivos vienen de la misma carpeta, o el usuario suelta una
  sola carpeta): se mantiene el comportamiento actual — `comprimidos/` junto al origen. Es
  el caso simple y no hay ninguna razón para complicarlo.
- **Orígenes mixtos** (el lote combina archivos de más de una carpeta): la app pasa a un
  **destino único para el lote**. Por defecto, `PreferenciasUsuario.CarpetaSalida`
  (`RutasApp.CarpetaSalidaPorDefecto`, ya existe) — el usuario puede cambiarla desde el
  panel de ajustes como ya puede hacer hoy con `SalidaJuntoAlOriginal=false`.
- **Regla de decisión:** `GestorArchivos` necesita saber, en el momento de resolver la ruta
  de salida, si el conjunto de archivos preparados tiene más de una carpeta padre distinta.
  Esto se calcula una vez al preparar el lote (no por archivo), y se guarda como parte del
  contexto del lote — no una preferencia persistida, sino una decisión derivada de lo que
  el usuario acaba de cargar.

## 4. Limitantes y reglas técnicas reales (revisadas en el código, no en abstracto)

### 4.1 — El análisis de 200 archivos hoy bloquea la interfaz (esto es un bug de escala real) · ✅ HECHO

**El problema, tal como estaba antes de esta tanda:** `MainWindowViewModel.AgregarRutas` era
un método **síncrono** que llamaba directamente a `ServicioCompresionLote.Preparar(rutas)`,
también **síncrono**. `Preparar` hace dos cosas costosas por archivo: expandir carpetas
recursivamente y leer hasta 4 MB de cada PDF para el análisis heurístico (`AnalizadorPdf`).
Con 200 archivos de varios MB cada uno, esto podía tardar varios segundos — y como todo
corría en el hilo de UI (se llamaba directo desde el handler de drag&drop / selector de
archivos), **la ventana se congelaba** mientras tanto.

**Solución implementada:**

- `ServicioCompresionLote` gana `PrepararAsync(IEnumerable<string> rutas, IProgress<ProgresoAnalisis>? progreso, CancellationToken ct)`.
  El `Preparar` síncrono existente **se conserva tal cual** (lo siguen usando las pruebas que
  no necesitan async) — `PrepararAsync` es una sobrecarga aditiva, no un reemplazo.
- Nuevo record en `Models`: `ProgresoAnalisis(int Analizados, int Total, string ArchivoActual)`,
  mismo espíritu que `ProgresoLote`.
- Internamente: expandir carpetas (`GestorArchivos.FiltrarPdfs`, ya es rápido — es I/O de
  metadatos, no de contenido) y luego analizar cada ruta dentro de `Task.Run`, reportando
  progreso archivo a archivo. No hizo falta paralelizar el análisis en sí (leer 4 MB de 200
  archivos secuencialmente ya es rápido comparado con comprimirlos); lo único que había que
  arreglar es que corriera **fuera del hilo de UI**, con progreso visible mientras tanto.
- `MainWindowViewModel.AgregarRutas` pasó a `AgregarRutasAsync`, con un estado nuevo
  (`Analizando`, similar a `Procesando`) que deshabilita las acciones del footer mientras
  dura, y un mensaje tipo "Analizando 45/200…" reutilizando `MensajeEstado`. El estado vacío
  de la zona de carga (Fase 4) ahora muestra el ícono girando y ese mensaje mientras dura el
  análisis, en vez de quedarse mudo.
- El code-behind (`MainWindow.axaml.cs`) pasó de `void AlSoltar/AlAnadirArchivos` a
  `async void` que hace `await Modelo.AgregarRutasAsync(rutas)` — ya eran `async void` en
  parte (los diálogos de archivo ya usan `await`), así que el cambio fue local.

### 4.2 — Ghostscript es un proceso por archivo: el paralelismo tiene un techo real

Cada compresión lanza un proceso `gs` nuevo (arranque ~100-300 ms de overhead cada uno,
aparte del tiempo de compresión real). Con 200 archivos:

- El `GradoParalelismo` hoy es 2 por defecto, con techo en `Environment.ProcessorCount`
  (`PreferenciasUsuario.GradoParalelismo` + `Math.Clamp` en `ServicioCompresionLote`).
  Subirlo acelera el lote, pero cada proceso de Ghostscript puede consumir bastante RAM con
  PDFs grandes — lanzar 8-16 en paralelo con archivos de 50+ MB cada uno puede saturar
  memoria en un equipo modesto. No hay una regla mágica; lo razonable es un techo prudente
  (p. ej. `Environment.ProcessorCount`, ya está) y quizás un modo "conservador" opcional que
  limite además por tamaño total en vuelo, no sólo por cantidad de procesos.
- Con 200 archivos y 2 en paralelo, si cada uno tarda ~3-5 s, el lote completo puede tardar
  **5-8 minutos**. La barra de progreso global ya existe (Fase 1), pero a esta escala hace
  falta que el usuario vea *qué tan lejos* está sin tener que adivinar — el mensaje actual
  ("Procesando 45/200 · archivo.pdf") ya ayuda, pero el total agregado en vivo (RF-21) es lo
  que realmente responde "¿cuánto llevo ahorrado hasta ahora?".

### 4.3 — Selección y "Comprimir": confirmado, la selección no cambia el alcance

Se confirma: **"Comprimir" siempre procesa la lista completa**, tenga o no selección activa.
La selección (RF-22) sirve exclusivamente para acciones destructivas/de gestión —
**"Quitar seleccionados"** (RF-23) y, si tiene sentido más adelante, "reintentar sólo los
seleccionados que fallaron". Esto simplifica bastante el diseño: `ComprimirCommand` no
cambia su contrato en absoluto, y la selección vive enteramente en la capa de UI
(`FilaResultadoViewModel.Seleccionada`), sin tocar `ServicioCompresionLote`.

### 4.4 — Quitar archivos mientras el análisis o la compresión está en curso

Hoy `Limpiar` sólo tiene sentido antes o después del proceso (`PuedeLimpiar` exige
`!Procesando`). Si se permite quitar filas individuales, hay que decidir si eso también se
bloquea durante el procesamiento (lo más simple y seguro) o si se permite cancelar
selectivamente un archivo en curso (mucho más complejo: implica cancelar un
`Task` individual dentro del `Parallel.ForAsync`, no todo el lote). Recomiendo la opción
simple para esta fase: quitar y seleccionar sólo están habilitados cuando `!Procesando`,
igual que hoy funciona `Limpiar`.

### 4.5 — El picker nativo de archivos con 200+ elementos

`OpenFilePickerAsync` con `AllowMultiple=true` funciona con cientos de archivos sin problema
conocido en ambos sistemas operativos — no es un límite real. El caso de uso principal para
200 archivos de todas formas va a ser **soltar una carpeta** (ya soportado) más que
seleccionar 200 archivos uno por uno en el diálogo.

### 4.6 — Memoria de la lista en la UI

`ObservableCollection<FilaResultadoViewModel>` con 200 elementos no es un problema de memoria
por sí mismo (cada fila es un objeto pequeño). El riesgo real está en 4.1 y 4.2, no aquí.

---

## 5. Diseño técnico resultante (con las tres decisiones ya tomadas)

### 5.1 — `Core`: cambios necesarios

- **`ServicioCompresionLote.PrepararAsync`** (nuevo, junto al `Preparar` síncrono que se
  conserva para los tests que no necesitan async): recorre y analiza en un `Task.Run`,
  reportando `IProgress<ProgresoAnalisis>` (nuevo record: `Analizados`, `Total`,
  `ArchivoActual` — mismo espíritu que `ProgresoLote`).
- **`GestorArchivos`**: nuevo método `TieneOrigenesMixtos(IReadOnlyList<ArchivoPdf>)` y
  ajuste en `ResolverRutaSalida` para aceptar el destino consolidado cuando aplica (sección
  3.2). La regla de nunca sobrescribir el original (ADR-004) no cambia.
- **`ResumenLote`**: ya sirve tal cual para el total agregado; sólo hace falta calcularlo
  también *durante* el proceso (sobre los resultados parciales que ya han llegado), no sólo
  al final — es recalcular sobre la misma colección con más frecuencia, no un tipo nuevo.

### 5.2 — `App`: cambios necesarios

- `FilaResultadoViewModel`: propiedad `Seleccionada` (bool, `[ObservableProperty]`).
- `MainWindowViewModel`: `AgregarRutasAsync` (reemplaza la versión síncrona en el camino de
  UI), `SeleccionarTodo`/`SeleccionarNinguno`/`SeleccionarPorEstado` commands,
  `QuitarSeleccionadosCommand`, `AbrirCarpetaResultadosCommand`, y propiedades de sólo
  lectura para el total agregado en vivo (`TotalOriginalLegible`, `TotalFinalLegible`,
  `TotalAhorroLegible`) que se recalculan en cada `Progress.Report` del lote.
- UI: checkbox por fila + checkbox "todos" en una cabecera de lista (nueva, la lista hoy no
  tiene cabecera), fila de totales agregados sobre el resumen destacado de la Fase 4 (no lo
  reemplaza: el resumen destacado sigue siendo el cierre del proceso; el total agregado vive
  *durante*), y el botón "Abrir carpeta de resultados" junto a "Añadir archivos" en el
  footer, habilitado sólo cuando `HayResumen`.

### 5.3 — Nada de esto rompe lo existente

- El `Preparar` síncrono actual se conserva (lo usan directamente las pruebas de
  `ServicioCompresionLoteTests` y `IntegracionGhostscriptTests`); `PrepararAsync` es una
  sobrecarga adicional, no un reemplazo con ruptura.
- `ComprimirCommand` no cambia de firma ni de comportamiento (sección 4.3).
- El comportamiento de un solo origen (el caso común, pocos archivos) no cambia en absoluto.

## 6. RF-25 — Unidad del umbral configurable (KB/MB) · ✅ HECHO

**Problema:** `PreferenciasUsuario.UmbralMb` sólo se edita en MB (`NumericUpDown` con
incremento de 0.5 y la etiqueta fija "MB" al lado). Para afinar a "cualquier PDF mayor a
300 KB" hoy hay que escribir `0.29` MB — impreciso e incómodo.

**Diseño:**

- El valor canónico persistido **sigue siendo `UmbralMb` en `PreferenciasUsuario`**, sin
  cambios en `Core` ni en el JSON ya guardado de usuarios existentes (retrocompatible).
- Nuevo enum en `Core.Models`: `UnidadTamano { KB, MB }`, y una propiedad nueva
  `PreferenciasUsuario.UnidadUmbral` (por defecto `MB`) — es una preferencia de cómo el
  usuario quiere *ver y editar* el número, igual de legítima en `Core` que `Nivel` o
  `SufijoSalida`; no es una referencia a Avalonia ni a nada de UI.
- En `MainWindowViewModel`: `UmbralValor` (el número que edita el `NumericUpDown`) se
  calcula a partir de `UmbralMb` y `UnidadUmbral` — mismo umbral real, unidad de
  visualización distinta. Cambiar de unidad **no cambia el umbral efectivo**, sólo cómo se
  edita (pasar de "2 MB" a ver "2048 KB" y viceversa, sin sorpresas).
- La UI reutiliza el patrón de selector segmentado ya construido en la Fase 4
  (`ToggleButton.segmento` + `ConvertidorEnumIgual`) con dos opciones en vez de tres, junto
  al campo numérico, reemplazando la etiqueta fija "MB".
- El resumen de ajustes en la barra de título (`ResumenAjustes`, hoy "2 MB · Medio") respeta
  la unidad seleccionada.

**Nota de verificación:** el proyecto usa *compiled bindings* (`AvaloniaUseCompiledBindingsByDefault`),
así que cada propiedad nueva (`UmbralValor`, `UmbralMaximo`, `UmbralIncremento`, `UmbralFormato`)
se valida en tiempo de compilación — si alguna no existiera o tuviera el tipo equivocado, el
build habría fallado con un error del compilador XAML, no en tiempo de ejecución. El build
terminó sin errores ni warnings. No se consiguió una captura de pantalla del selector KB/MB
en esta sesión (el entorno tiene múltiples Spaces/monitores de macOS que interfieren con los
clics automatizados, igual que en la Fase 4) — pero reutiliza exactamente el mismo patrón
visual (`ToggleButton.segmento` + `ConvertidorEnumIgual`) ya verificado con capturas para el
selector de Nivel. Confírmalo a mano abriendo Ajustes (ícono ▤ en la barra de título).

## 7. Lo que no cambia

- El motor (Ghostscript), el umbral, los niveles de compresión y la regla de no tocar el
  original (ADR-004) siguen exactamente igual.
- El diseño visual de la Fase 4 (tarjetas, iconografía, tokens) se extiende, no se rehace —
  esta fase le añade una columna de selección y una fila de totales, no cambia el lenguaje
  visual ya construido.
- Las 43 pruebas actuales no deberían romperse: los cambios son aditivos sobre
  `ServicioCompresionLote` (una sobrecarga async de `Preparar`) y sobre la UI.
