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
| 4.6 Verificación claro/oscuro | Contraste AA en ambos temas | ✅ toggle implementado en Fase 7 (ADR-009); Semi.Avalonia responde a `RequestedThemeVariant` |
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

**Estado: completa ✅** Plan completo en
[09-PLAN-CONFIG-VISIBLE-Y-PULIDO.md](09-PLAN-CONFIG-VISIBLE-Y-PULIDO.md).

| RF | Descripción | Estado |
|---|---|---|
| RF-26 | Configuración en panel lateral fijo y plegable, en vez de un `Flyout` oculto | ✅ |
| RF-27 | DPI de imágenes configurable | ✅ sólo Core — el control se retiró de la UI por decisión del usuario |
| RF-28 | Convertir a escala de grises | ✅ |
| RF-29 | Sufijo del nombre de salida editable en la UI | ✅ |
| RF-30 | Ruta manual de Ghostscript editable en la UI + "volver a comprobar" | ✅ lógica lista, UI oculta por decisión del usuario |
| RF-31 | Grado de paralelismo configurable en la UI | ✅ |
| RF-32 | Nivel de compatibilidad PDF configurable | ✅ sólo Core — el control se retiró de la UI por decisión del usuario |
| RF-33 | Reunir todos los archivos del lote en la carpeta de salida (auto al terminar) | ✅ ver Fase 7 |
| RF-34 | Reintentar sólo los archivos que fallaron por el motor, fusionando con el lote anterior | ✅ |

**RF-27/28/32:** `PreferenciasUsuario.APerfil()` ahora propaga DPI, escala de grises y
compatibilidad al motor. Sólo escala de grises quedó en la UI; DPI y compatibilidad se
retiraron por decisión del usuario (capacidad disponible en el Core).

**RF-33 · reunión automática** (comportamiento final, ver Fase 7): `GestorArchivos.CopiarA`
reúne comprimidos y originales de archivos omitidos/sin-ganancia en la carpeta de salida al
terminar el lote. Pruebas: `CopiarComprimidos_incluye_omitidos_junto_a_los_comprimidos`,
`CopiarComprimidos_incluye_sin_ganancia_junto_a_los_comprimidos`.

**RF-34 · Reintentar fallidos:** `ReintentarFallidosCommand` reprocesa sólo filas en `Error`,
fusiona con `FusionarResultados` y recalcula el resumen sobre el lote completo.

---

## Fase 7 — Tema oscuro/claro, mensaje persistente y reunión automática de omitidos

**Estado: completa ✅**

| Mejora | Descripción | Implementación |
|---|---|---|
| RF-35 Tema oscuro/claro (ADR-009) | Toggle luna/sol en la barra de título; persiste entre sesiones | `PreferenciasUsuario.TemaOscuro`, `MainWindowViewModel.AlternarTemaCommand`, `MainWindow.AplicarTema` → `Application.RequestedThemeVariant` |
| Barra de título reestructurada | Orden: logo · toggle-tema · toggle-ajustes · spacer · resumen · — · □ · × | Elimina la confusión del guion junto a — □ × |
| `MensajeInformativo` persistente | Texto visible tras terminar la animación de progreso; resume qué pasó y dónde quedaron los archivos | `[ObservableProperty] _mensajeInformativo`, Row 4 en `MainWindow.axaml` |
| Reunión automática de omitidos | Con carpeta de salida explícita, los omitidos/sin-ganancia se copian solos al terminar el lote; el usuario encuentra todos sus archivos en un sitio sin hacer nada | `MainWindowViewModel.ReunirOmitidosAutomaticamente` llama a `ServicioCompresionLote.CopiarComprimidos` |
| Botón "Descargar" eliminado | Ya no era necesario tras la reunión automática; se retiró junto con `HayDescarga`, `ResumenDescarga`, `_descargaRealizada` y `DescargarComprimidosCommand` | — |

---

## Deuda técnica conocida

- **Sin pruebas de UI.** Los ViewModels no están cubiertos. Avalonia.Headless permitiría
  probarlos sin abrir ventana; merece la pena cuando la UI crezca.
- **`AnalizadorPdf` lee hasta 4 MB por archivo.** Para lotes de cientos de PDFs enormes,
  medir si conviene bajar el límite o leer sólo cabecera y cola.
- **El grado de paralelismo es fijo (2).** No se adapta al número de núcleos ni al tamaño de
  los archivos. Exponerlo en la UI es RF-31 (Fase 6); la adaptación automática sigue pendiente.
- **La estimación de páginas es aproximada** y hoy no se usa para nada visible.
