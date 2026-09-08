# Roadmap

Leyenda: ✅ hecho y probado · 🟡 parcial · ⬜ pendiente

---

## Fase 1 — MVP · **completa** ✅

| | Requisito | Nota |
|---|---|---|
| ✅ | RF-01 Drag & drop + selector de archivos | Soltar una **carpeta** también funciona: se expande recursivamente |
| ✅ | RF-02 Carga múltiple | Con deduplicación por ruta |
| ✅ | RF-03 Umbral configurable (2 MB por defecto) | Si no lo supera, ni se invoca el motor |
| ✅ | RF-04 Perfil balanceado por defecto | `/ebook`, 150 dpi |
| ✅ | RF-05 Carpeta de salida configurable | Por defecto `comprimidos/` junto al original |
| ✅ | RF-06 No sobrescribir el original | Reforzado: *nunca* se sobrescribe (ADR-004) |
| ✅ | RF-07 Reporte por archivo | Original, final, % y estado en tabla |
| ✅ | RF-08 Indicador de progreso | Barra global + mensaje con el archivo en curso |
| ✅ | RNF-01 Compresión fuera del hilo de UI | `Parallel.ForAsync` + `IProgress` |
| ✅ | RNF-02 Un error no detiene el lote | Probado con archivo corrupto entre archivos sanos |
| ✅ | RNF-03 Original intacto | Prueba dedicada |
| ✅ | RNF-05 Funcionamiento offline | Sin una sola llamada de red |
| ✅ | RNF-07 Preferencias persistentes | JSON, tolerante a corrupción |
| ✅ | RNF-08 Logs | `compresor.log` en la carpeta de datos del usuario |
| ✅ | RNF-04 Instalable | `.app` + `.dmg` verificados en Mac; `.exe` + `.msi` verificados en Windows real |

**Verificado de extremo a extremo:** PDF real de 2.23 MB → 246 KB (−89.2 %), con el pequeño
omitido y el corrupto reportado sin frenar el lote. La app empaquetada arranca desde Finder
y localiza Ghostscript con el `PATH` mínimo de launchd.

---

## Empaquetado (RNF-04) — hecho, con una salvedad

Los scripts están en `build/` y se ejecutan con un comando:

```bash
./build/publicar-macos.sh arm64        # o x64, o universal
pwsh build/publicar-windows.ps1        # desde Windows
```

**Qué está verificado:** el `.app` de macOS se monta, arranca desde Finder y encuentra
Ghostscript incluso con el `PATH` mínimo de launchd; el `.dmg` se genera (46 MB). El
`.exe` de Windows compila y arranca en máquina real; el `.msi` generado con WiX 7 se
instala correctamente en Windows 11.

**Lo que falta y depende de ti (no de código):**

| Paso | Qué necesita | Sin ello |
|---|---|---|
| Firmar el `.app` | Apple Developer ID (99 $/año) | Gatekeeper lo bloquea en otros equipos; el usuario debe hacer clic derecho › Abrir |
| Notarizar el `.dmg` | La misma cuenta + `notarytool store-credentials` | Aviso de "no se puede comprobar si contiene malware" |
| Firmar el `.msi` | Certificado de code signing | SmartScreen avisa al instalar |

Los tres pasos ya están cableados en los scripts: se activan solos si defines
`IDENTIDAD_FIRMA`, `PERFIL_NOTARIZACION` o `HUELLA_CERTIFICADO`.

**Recordatorio:** ni el `.dmg` ni el `.msi` incluyen Ghostscript, y no deben incluirlo
([ADR-003](02-DECISIONES-ADR.md#adr-003--licencia-ghostscript-no-se-distribuye-con-la-aplicación)).

---

## Fase 2 — Uso diario

| | Requisito | Estado real |
|---|---|---|
| ✅ | RF-09 Procesamiento por lotes con progreso | Cola paralela con grado configurable |
| ✅ | RF-10 Niveles Bajo / Medio / Alto | Selector en la UI, mapeados a PDFSETTINGS |
| ✅ | RF-12 Casos especiales | Protegido, corrupto y escaneado detectados y reportados |
| ✅ | RF-13 Cancelar un proceso en curso | Mata el árbol de procesos y limpia salidas parciales |
| ⬜ | RF-11 Vista previa antes/después | Necesita render de páginas → traer PDFium (BSD) |

Lo que queda de Fase 2 es sólo la vista previa. Requiere revisar el ADR-005.

**Además, pendiente en esta fase:**
- ⬜ Progreso *por archivo*, no sólo global. Ghostscript no reporta avance; habría que
  estimarlo por páginas procesadas leyendo su salida.
- ✅ Acción para reintentar sólo los archivos fallidos → **RF-34** (Fase 6).

---

## Fase 3 — Producto pulido

| | Requisito | Punto de partida |
|---|---|---|
| 🟡 | RF-14 Perfiles guardables | `PerfilCompresion` ya existe con `Email()`, `Balanceado()`, `Impresion()`; falta persistir una lista y exponerla en la UI |
| ⬜ | RF-15 Carpeta vigilada | `FileSystemWatcher` + reutilizar `ServicioCompresionLote`. El `Core` ya sirve tal cual |
| ⬜ | RF-16 Historial de archivos procesados | Ampliar `RegistroArchivo` a un registro consultable (SQLite o JSON por lotes) |
| ⬜ | RF-17 Menú contextual del SO | Windows: entrada de registro. macOS: Servicio o extensión Finder. Es trabajo específico por plataforma |
| ⬜ | RF-18 Renombrado con patrón | `PreferenciasUsuario.SufijoSalida` ya existe; falta un patrón con variables (`{nombre}`, `{fecha}`, `{reduccion}`) |
| ⬜ | Auto-actualización | Sólo tiene sentido después del empaquetado firmado |

---

## Fase 4 — Rediseño visual completo

**Estado: 4.0–4.5 implementadas y compilando; 4.6–4.7 parciales.** Plan detallado en
[07-PLAN-DISENO.md](07-PLAN-DISENO.md). No añade ni quita requisitos funcionales del plan
original — es exclusivamente UI, y `CompresorPdf.Core` no se tocó (las 39 pruebas de Fase 1/2
siguen pasando igual).

| Sub-fase | Entregable | Estado |
|---|---|---|
| 4.0 Fundamentos | Semi.Avalonia integrado (ADR-007), íconos y controles propios | ✅ |
| 4.1 Shell de ventana | Barra de título propia, arrastre, min/max/cerrar | ✅ |
| 4.2 Zona de carga | Estado vacío + feedback visual de drag & drop | ✅ |
| 4.3 Lista de archivos | Tarjetas con iconografía de estado, reemplaza al `DataGrid` | ✅ verificado con Ghostscript real |
| 4.4 Panel de ajustes | Colapsable en un Flyout, selector segmentado de nivel | ✅ |
| 4.5 Progreso y resumen | Barra pulida, métricas destacadas (ahorro total en grande) | ✅ verificado con Ghostscript real |
| 4.6 Verificación claro/oscuro | Contraste AA en ambos temas | 🟡 compila y arranca en ambos; falta captura visual del tema claro |
| 4.7 Pulido y QA visual | Micro-interacciones (hechas); clic real en el chrome de ventana propio | 🟡 pendiente de probar a mano |

**Resultado ya verificado con datos reales:** un PDF de 2.23 MB comprimido a 246.64 KB
(-89.2 %) se ve en una tarjeta con ícono de éxito, chip de reducción en verde, y un resumen
destacado ("1.99 MB ahorrados") — ver detalle y capturas en
[07-PLAN-DISENO.md](07-PLAN-DISENO.md#7-fases-de-ejecución).

**Antes de dar la fase por cerrada del todo**, prueba a mano el chrome de ventana propio
(arrastrar, doble-clic para maximizar, los tres botones) y confirma visualmente el tema claro —
esta sesión no pudo verificarlo por una limitación del entorno (múltiples Spaces/monitores de
macOS), no por un problema conocido de la app.

---

## Fase 5 — Lotes grandes (selección, totales y exportación)

**Estado: completa ✅** Plan completo en [08-PLAN-LOTES-GRANDES.md](08-PLAN-LOTES-GRANDES.md).
Nace de un caso de uso real: cargar una carpeta con ~200 PDFs y poder gestionarlos (seleccionar,
quitar, ver el total agregado, obtener los resultados) sin que la interfaz se congele.

| RF | Descripción | Estado |
|---|---|---|
| RF-19 | Carga de ~200 PDFs sin bloquear la UI durante el análisis | ✅ verificado con 205 PDFs y un lote de 323 MB |
| RF-20 | Información por archivo a esta escala (ya cubierto por la Fase 4) | ✅ |
| RF-21 | Totales agregados en vivo (antes/después de todo el lote) | ✅ |
| RF-22 | Selección de archivos (individual y en bloque) | ✅ |
| RF-23 | Quitar archivos sin vaciar todo el lote | ✅ |
| RF-24 | Botón "Abrir carpeta de resultados" | ✅ |
| RF-24b | Carpeta de salida consolidada cuando el lote mezcla orígenes distintos | ✅ |
| RF-25 | Unidad del umbral configurable (KB/MB) | ✅ |

**RF-19:** `PrepararAsync` (corre en `Task.Run`, reporta progreso, respeta cancelación).
Verificado con 205 PDFs / 323 MB sin ningún bloqueo.

**RF-21:** totales acumulados en vivo durante la compresión (original → final + % de ahorro)
que crecen fila a fila en el panel de progreso, usando un acumulador `resultadosParciales`
dentro del callback de `Progress<ProgresoLote>`.

**RF-22 + RF-23:** checkbox por fila y checkbox de cabecera ("seleccionar todo"). Los cambios
en `FilaResultadoViewModel.Seleccionada` se propagan al ViewModel padre mediante suscripción
a `PropertyChanged`. "Quitar seleccionados" sólo borra las filas marcadas, sin limpiar el
lote completo.

**RF-24:** `AbrirCarpetaResultadosCommand` llama a `Process.Start(UseShellExecute=true)`
con la carpeta del primer archivo comprimido — abre Explorador en Windows y Finder en macOS.

**RF-24b:** `GestorArchivos.TieneOrigenesMixtos` detecta lotes con archivos de más de una
carpeta padre; en ese caso `ComprimirAsync` clona las preferencias y fuerza
`SalidaJuntoAlOriginal=false` con la carpeta de salida configurada (o la por defecto), para
que todos los comprimidos queden en un mismo sitio en vez de desperdigados.

---

## Fase 6 — Configuración visible y pulido previo al despliegue

**Estado: en curso.** Plan completo en
[09-PLAN-CONFIG-VISIBLE-Y-PULIDO.md](09-PLAN-CONFIG-VISIBLE-Y-PULIDO.md). Sale de la revisión
del sistema antes del primer despliegue.

Leyenda de esta tabla: 🟡 = código y pruebas unitarias integrados, pendiente la QA manual.

| RF | Descripción | Estado |
|---|---|---|
| RF-26 | Configuración en panel lateral fijo y plegable, en vez de un `Flyout` oculto | 🟡 |
| RF-27 | DPI de imágenes configurable | 🟡 sólo Core — el control se retiró de la UI por decisión del usuario |
| RF-28 | Convertir a escala de grises | 🟡 |
| RF-29 | Sufijo del nombre de salida editable en la UI (el campo ya se persistía) | 🟡 |
| RF-30 | Ruta manual de Ghostscript editable en la UI + "volver a comprobar" | 🟡 lógica lista, UI oculta por el usuario |
| RF-31 | Grado de paralelismo configurable en la UI (hoy fijo en 2) | 🟡 |
| RF-32 | Nivel de compatibilidad PDF configurable | 🟡 sólo Core — el control se retiró de la UI por decisión del usuario |
| RF-33 | "Descargar comprimidos": reunir todos los comprimidos del lote en la carpeta de salida + total antes/después | 🟡 |
| RF-34 | Reintentar sólo los archivos que fallaron por el motor, fusionando el resultado con el lote anterior | 🟡 |

**Raíz de RF-27/28/32, ya resuelta:** `PreferenciasUsuario.APerfil()` sólo propagaba `Nivel`
al motor; `CompresorGhostscript` ya sabía usar DPI, escala de grises y compatibilidad, pero
nunca le llegaban. `APerfil()` ahora los propaga todos (prueba
`APerfil_propaga_dpi_grises_y_compatibilidad_al_motor`). De las tres, sólo **escala de grises**
(RF-28) quedó expuesta en la UI: DPI personalizado y compatibilidad PDF se retiraron del panel
por decisión del usuario (la capacidad sigue en el Core y sus valores por defecto se
propagan), y el `NumericUpDown` del umbral pasó a `decimal?` para no reventar con
`InvalidCastException` al vaciar el campo.

**Recortes de UI adicionales pedidos por el usuario tras verlo en pantalla:** se quitó la
casilla "Respaldar el original" (se fuerza `CrearRespaldo = false`), se ocultó la sección
"Ruta de Ghostscript" del bloque Motor (queda sólo el estado del motor + "Archivos en
paralelo"; la lógica RF-30 permanece), y se añadió un carril con botón para volver a mostrar
el panel cuando está plegado.

**RF-33 · "Descargar comprimidos":** `GestorArchivos.CopiarA` reúne los PDF ya comprimidos
del lote (`Estado == Comprimido`) en la carpeta de salida configurada, omitiendo los que ya
estaban ahí y sin pisar nombres. Expuesto vía `ServicioCompresionLote.CopiarComprimidos` y el
comando `DescargarComprimidosCommand`. El resumen muestra además el total antes/después de
los comprimidos (`ResumenDescarga`, p. ej. `12 comprimido(s) · 45.2 MB → 12.1 MB`). Pruebas:
`CopiarA_reune_los_archivos_en_la_carpeta_destino`,
`CopiarA_omite_los_que_ya_estan_en_la_carpeta_destino`,
`CopiarA_no_pisa_un_nombre_que_ya_existe_en_el_destino`.

**Footer de acciones a ancho completo:** la fila de botones vivía dentro de la columna del
contenido y, con el panel de 300 px abierto, se montaba con él. Pasó a un `Border` propio en
una tercera fila de la ventana (`RowDefinitions="44,*,Auto"`), ocupa todo el ancho, usa un
`WrapPanel` (los botones bajan de línea antes que solaparse) y "Cancelar" sólo aparece
mientras hay un proceso en curso.

**RF-34 · Reintentar fallidos + presentación de estados que parecían errores.** Tras ver un
lote real con archivos en rojo:

- **Carrera al resolver la ruta de salida.** Con orígenes mixtos, varios PDF con el mismo
  nombre se redirigen a una carpeta única y `ResolverRutaSalida` (que sólo mira `File.Exists`)
  devolvía la misma ruta a dos hilos; uno terminaba con "Ghostscript terminó sin generar el
  archivo de salida". `ServicioCompresionLote` ahora reserva la ruta bajo cerrojo y crea el
  archivo vacío antes de invocar al motor. Prueba:
  `Dos_pdf_con_el_mismo_nombre_no_se_pisan_al_ir_a_una_carpeta_unica`.
- **Motor que dice "ok" sin escribir nada** → `Error` explícito ("El motor terminó sin
  escribir el PDF de salida") en vez de una fila "comprimida a 0 KB".
  `CompresorGhostscript` además rechaza un archivo de salida de 0 bytes. Pruebas:
  `Si_el_motor_dice_ok_pero_no_escribe_nada_se_marca_Error`.
- **"Ya optimizado" / "Omitido" mostraban `16 KB → 149 KB`**, como si el archivo hubiera
  crecido. `FilaResultadoViewModel.Aplicar` muestra `→ —` cuando no se entregó archivo.
- **Chip `-0 %`** en archivos que encogieron una milésima: `ReduccionLegible` devuelve `—`
  por debajo del 0.1 %. Pruebas en `ResultadoCompresionTests`.
- **`ReintentarFallidosCommand`** reprocesa sólo las filas en `Error`, fusiona los nuevos
  resultados con `_ultimosResultados` (`FusionarResultados`) y recalcula el resumen sobre el
  lote completo. Botón "Reintentar fallidos" en el footer, visible con `HayFallidos`.

**Pendiente para cerrar la fase:** la QA manual de la Parte C del plan — verificación visual
del tema claro (§4.6), prueba a mano del chrome de ventana (§4.7) y humo con Ghostscript real
(lote con DPI + grises + sufijo).

**Además, pendiente de pulido en esta fase** (detalle en el plan): reconciliar el recuento de
pruebas entre `CLAUDE.md` y la trazabilidad, cerrar la verificación visual del tema claro
(§4.6) y la prueba a mano del chrome de ventana (§4.7), y añadir el [ADR-008](02-DECISIONES-ADR.md#adr-008).

---

## Deuda técnica conocida

- **Sin pruebas de UI.** Los ViewModels no están cubiertos. Avalonia.Headless permitiría
  probarlos sin abrir ventana; merece la pena cuando la UI crezca.
- **`AnalizadorPdf` lee hasta 4 MB por archivo.** Para lotes de cientos de PDFs enormes,
  medir si conviene bajar el límite o leer sólo cabecera y cola.
- **El grado de paralelismo es fijo (2).** No se adapta al número de núcleos ni al tamaño de
  los archivos. Exponerlo en la UI es RF-31 (Fase 6); la adaptación automática sigue pendiente.
- **La estimación de páginas es aproximada** y hoy no se usa para nada visible.
