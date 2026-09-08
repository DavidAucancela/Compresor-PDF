# Fase 6 — Configuración visible y pulido previo al despliegue

**Estado: en curso.** Nace de la revisión del sistema antes del primer despliegue. Dos
carencias concretas y una lista de pulido:

1. **La configuración está escondida.** Hoy vive entera dentro de un `Flyout` que cuelga del
   ícono de engranaje de la barra de título (`MainWindow.axaml`, líneas 40-112). Hay que
   abrirlo para saber —o cambiar— con qué ajustes se va a comprimir. Pasa a un **panel
   lateral fijo, plegable**.
2. **Opciones de compresión descritas pero no cableadas.** `PreferenciasUsuario` y
   `PerfilCompresion` ya declaran campos que el motor Ghostscript sabe usar, pero que
   ningún control de la UI toca y que `APerfil()` ni siquiera propaga. Están muertos.
3. **Pulido y QA** que quedaba pendiente de fases anteriores (tema claro, chrome de ventana,
   recuento de pruebas) y que hay que cerrar antes de publicar.

No se toca `CompresorPdf.Core` en su arquitectura: se rellenan huecos ya previstos en sus
modelos. La regla 5 del proyecto sigue en pie — **cada regla nueva lleva su prueba**.

---

## Parte A — Configuración visible (RF-26)

### Qué hay hoy

- Todos los ajustes viven en un `Flyout` anclado al botón de engranaje de la barra de título.
- Cuando está cerrado, sólo se ve el resumen de una línea `ResumenAjustes`
  (`"2 MB · Medio"`) en la barra de título.
- El plan de diseño ([07-PLAN-DISENO.md](07-PLAN-DISENO.md) §5.4 y §8) lo llama "decisión
  confirmada". **Esta fase la revierte** — ver [ADR-008](02-DECISIONES-ADR.md#adr-008).

### Qué queremos

Panel lateral fijo a la derecha del contenido (~300 px), visible por defecto. El botón de
engranaje de la barra de título lo pliega y despliega; plegado, la columna desaparece y la
lista ocupa todo el ancho. Dentro del panel hay un segundo botón (chevron) para plegarlo.
Los valores activos se ven sin abrir nada, y el resumen de una línea de la barra de título
(`ResumenAjustes`) sigue estando para el vistazo rápido cuando está plegado.

```
┌───────────────┬──────────────┐
│ Zona de carga │ Configuración ▶
│ / lista       │  COMPRESIÓN   │
│ (scrollea)    │  Umbral [2]MB │
│               │  Nivel ▢▣▢    │
│               │  ☐ Grises     │
│               │  ─────────    │
│               │  SALIDA       │
│               │  ☑ junto orig.│
│               │  Sufijo […]   │
│               │  ☐ Respaldar  │
│               │  ─────────    │
│               │  MOTOR        │
│               │  Ruta gs […]  │
│               │  [Volver a…]  │
│               │  Paralelismo 2│
├───────────────┴──────────────┤
│  [Añadir] [Limpiar] [Comprimir]
└──────────────────────────────┘
```

> **Ajuste tras la primera prueba en pantalla:** se retiraron de la UI el "DPI personalizado"
> (RF-27) y "Compatibilidad PDF" (RF-32) por decisión del usuario — el Core conserva ambas
> capacidades y `APerfil()` propaga sus valores por defecto. El `NumericUpDown` del umbral
> pasó a `decimal?` (su `Value` es anulable; vaciar el campo mandaba `null` y reventaba con
> `InvalidCastException`). El `ItemsControl` de la lista de archivos se envolvió en un
> `ScrollViewer` — no scrollea por sí solo.

### Cambios

| Archivo | Cambio |
|---|---|
| `MainWindow.axaml` | La grilla de contenido (`Grid.Row="1"`) pasa a 2 columnas: `*` (contenido actual) y `Auto` (panel). Se elimina el bloque `<Button.Flyout>…</Button.Flyout>` y su contenido se traslada al panel, reorganizado en secciones ("Compresión", "Salida", "Motor") con `Separator` entre grupos. El botón de engranaje de la barra de título deja de abrir un flyout y pasa a conmutar `PanelConfigVisible`. |
| `MainWindowViewModel.cs` | Nueva `[ObservableProperty] bool _panelConfigVisible`, inicializada desde preferencias. Se vuelca en `GuardarPreferencias()`. |
| `PreferenciasUsuario.cs` | Nuevo `bool PanelConfiguracionVisible { get; set; } = true` (RNF-07: el estado del panel sobrevive entre sesiones). |
| `Styles/Controles.axaml` | Clase `panel-config` (fondo `SemiColorBackground1`, borde izquierdo `SemiColorBorder`, transición de ancho) y `panel-config.plegado`. |
| `Iconos.axaml` | Ícono "plegar/desplegar" (un chevron). `IconoAjustes` se conserva para el botón conmutador. |

### Decisiones tomadas

- **Inline en `MainWindow.axaml`, no un `UserControl` aparte.** Es el mismo criterio que ya
  tomó la Fase 4 (§7 del plan de diseño: se abandonó `PanelAjustes.axaml` para reducir el
  riesgo de errores de binding). El panel comparte `DataContext` con la ventana.
- **Ancho fijo 300 px; plegado = columna oculta** (`IsVisible=false`). Para volver a abrirlo
  se usa el botón de engranaje de la barra de título, que siempre está presente. Un carril
  estrecho permanente se valoró y se descartó por no añadir nada frente al botón del chrome.
- **Responsivo:** con la ventana en su `MinWidth` (760) el panel abierto deja ~416 px para la
  lista, que sigue siendo usable. No se auto-pliega por ancho en esta fase; si molesta en
  pruebas manuales, se añade un umbral después.
- El resumen `ResumenAjustes` de la barra de título se mantiene para el vistazo rápido con el
  panel plegado. La cabecera del propio panel dice "Configuración".
- El toggle se persiste **al instante** (`AlternarPanelConfigCommand` guarda), no sólo al
  cerrar la ventana.

### Prueba

`PreferenciasTests`: `El_estado_del_panel_de_configuracion_sobrevive_a_un_ciclo_guardar_cargar`.
(Las vistas no tienen cobertura — ver "Deuda técnica" del roadmap; el binding se verifica a
mano en la QA de la Parte C.)

---

## Parte B — Opciones de compresión no habilitadas (RF-27 … RF-32)

### B0 · Raíz del problema: `APerfil()` no propaga nada

`PreferenciasUsuario.APerfil()` construye el `PerfilCompresion` que llega al motor y **sólo
copia `Nivel`**:

```csharp
public PerfilCompresion APerfil() => new()
{
    Nombre = "Actual",
    Nivel = Nivel
};
```

`CompresorGhostscript.ConstruirArgumentos` ya sabe traducir `DpiImagenes`, `EscalaDeGrises` y
`NivelCompatibilidad` a conmutadores de Ghostscript — pero como `APerfil()` nunca los rellena,
esas ramas sólo se ejercitan desde las fábricas estáticas `PerfilCompresion.Email()` /
`Impresion()`, que **nadie llama** en la app real. Arreglar `APerfil()` es el prerrequisito de
todo lo demás en esta parte.

```csharp
public PerfilCompresion APerfil() => new()
{
    Nombre = "Actual",
    Nivel = Nivel,
    DpiImagenes = DpiImagenes,               // nuevo campo en PreferenciasUsuario
    EscalaDeGrises = EscalaDeGrises,          // nuevo campo
    NivelCompatibilidad = NivelCompatibilidad // nuevo campo, default "1.7"
};
```

**Prueba** (`CompresorGhostscriptTests`, sin Ghostscript real):
`APerfil_propaga_dpi_grises_y_compatibilidad_al_perfil_del_motor`.

### B1 · DPI de imágenes (RF-27)

- **Campo:** `int? DpiImagenes` en `PreferenciasUsuario` (default `null` = usa el DPI que ya
  trae el nivel: 300/150/72).
- **UI:** en "Compresión", un `NumericUpDown` con casilla "DPI personalizado". Sin marcar =
  `null` = "Auto".
- **Motor:** ya soportado (`-dDownsample*Images=true` + `-d*ImageResolution=<dpi>`).
- **Pruebas:** ciclo guardar/cargar en `PreferenciasTests`; y en `CompresorGhostscriptTests`
  que un perfil con `DpiImagenes=120` produce `-dColorImageResolution=120` (extiende el test
  `El_perfil_Email_fuerza_el_dpi_configurado` al camino real).

### B2 · Escala de grises (RF-28)

- **Campo:** `bool EscalaDeGrises` en `PreferenciasUsuario` (default `false`).
- **UI:** casilla "Convertir a escala de grises" en "Compresión", con subtítulo "reduce
  mucho, elimina el color".
- **Motor:** ya soportado (`-sColorConversionStrategy=Gray -dProcessColorModel=/DeviceGray`).
- **Pruebas:** ciclo guardar/cargar; el test `Escala_de_grises_anade_la_conversion_de_color`
  ya cubre el motor — se añade uno que verifica que el ajuste llega vía `APerfil()`.

### B3 · Sufijo del nombre de salida en la UI (RF-29)

- **Campo:** `string SufijoSalida` — **ya existe, ya se persiste, ya lo usa
  `GestorArchivos.ResolverRutaSalida`**. Sólo falta el control.
- **UI:** `TextBox` "Sufijo del archivo" en "Salida", con ejemplo en vivo:
  `documento{sufijo}.pdf`.
- Cierra la mitad "sufijo fijo" de RF-18. El patrón con variables (`{nombre}`, `{fecha}`,
  `{reduccion}`) sigue fuera de alcance (Fase 3).
- **Prueba:** ya hay `Las_preferencias_sobreviven_a_un_ciclo_guardar_cargar` con
  `SufijoSalida = "-min"`. No hace falta una nueva; sí un caso en `GestorArchivosTests` de
  que el sufijo aparece en la ruta resuelta si no lo hubiera ya.

### B4 · Ruta manual de Ghostscript en la UI (RF-30)

- **Campo:** `string? RutaGhostscript` — **ya existe, ya se persiste**, `App.axaml.cs` ya la
  pasa a `LocalizadorGhostscript` al arrancar. El texto de `AvisoMotor` **ya le dice al
  usuario "o indica su ruta en Ajustes"… y no hay dónde.** Promesa rota.
- **UI:** en "Motor", `TextBox` + botón de selector de archivo. Botón "Volver a comprobar".
- **Aplicar el cambio sin reiniciar:** `LocalizadorGhostscript` se construye una sola vez en
  el arranque. Para que "Volver a comprobar" tenga efecto:
  - **Opción elegida (mínima):** al pulsar "Volver a comprobar", reconstruir el
    `LocalizadorGhostscript` y el `CompresorGhostscript` dentro del ViewModel y refrescar
    `GhostscriptDisponible` / `AvisoMotor`. Requiere que el VM reciba una *factory*
    (`Func<string?, CompresorGhostscript>`) desde `App.axaml.cs` en lugar de sólo la
    instancia — cambio acotado a la composición de dependencias.
  - Si eso se complica, *fallback*: aceptar la ruta, persistirla y mostrar "se aplicará al
    reiniciar". Se documenta en el propio panel.
- **Pruebas:** ciclo guardar/cargar de `RutaGhostscript`; `LocalizadorGhostscript` ya tiene
  cobertura de "ruta manual válida gana al PATH" — verificar que sigue verde.

### B5 · Grado de paralelismo en la UI (RF-31)

- **Campo:** `int GradoParalelismo` — **ya existe, ya se persiste**, `ServicioCompresionLote`
  ya lo aplica con `Math.Clamp(valor, 1, Environment.ProcessorCount)`. Es deuda técnica
  declarada en el roadmap ("El grado de paralelismo es fijo (2)…").
- **UI:** en "Motor", `NumericUpDown` de 1 a `Environment.ProcessorCount`, con nota "más
  archivos a la vez = más rápido, más CPU y RAM".
- **Prueba:** ciclo guardar/cargar; `ServicioCompresionLoteTests` ya ejercita el lote — no
  necesita una nueva salvo un caso de que el clamp respeta el mínimo 1.

### B6 · Nivel de compatibilidad PDF (RF-32) — opcional, baja prioridad

- **Campo:** `string NivelCompatibilidad` en `PreferenciasUsuario` (default `"1.7"`).
- **UI:** `ComboBox` 1.4 / 1.5 / 1.6 / 1.7 en "Compresión", plegado bajo un "Avanzado".
- **Motor:** ya soportado (`-dCompatibilityLevel=<v>`).
- Es un ajuste de nicho. Si aprieta el tiempo, se propaga vía `APerfil()` (B0) con el default
  y se deja el control para más adelante — no bloquea el despliegue.
- **Prueba:** cubierta por el test de B0.

### Resumen de campos nuevos en `PreferenciasUsuario`

| Campo | Tipo | Default | RF |
|---|---|---|---|
| `PanelConfiguracionVisible` | `bool` | `true` | RF-26 |
| `DpiImagenes` | `int?` | `null` | RF-27 |
| `EscalaDeGrises` | `bool` | `false` | RF-28 |
| `NivelCompatibilidad` | `string` | `"1.7"` | RF-32 |

`SufijoSalida`, `RutaGhostscript` y `GradoParalelismo` ya existen. El JSON viejo sigue
cargando: los campos que falten toman su default (RNF-07).

---

## Parte D — "Descargar comprimidos" (RF-33)

Pedido tras ver la app en pantalla. Cuando se guarda "junto al original", los comprimidos
quedan repartidos en subcarpetas `comprimidos/`; hace falta poder reunirlos.

- **`GestorArchivos.CopiarA(rutasOrigen, carpetaDestino)`** (Core): copia archivos a una
  carpeta única, omite los que ya están en el destino, no pisa nombres (reusa `RutaLibre`).
  **No mueve** — original y comprimido en su sitio quedan intactos (RNF-03).
- **`ServicioCompresionLote.CopiarComprimidos(resultados, carpeta)`**: filtra a
  `Estado == Comprimido` con `RutaSalida` y delega en `CopiarA`. Passthrough como
  `TieneOrigenesMixtos`.
- **ViewModel:** guarda `_ultimosResultados` del último lote; `DescargarComprimidosCommand`
  resuelve la carpeta (campo de la UI → `Preferencias.CarpetaSalida` → carpeta por defecto) y
  llama al servicio; deja `_carpetaResultados` apuntando ahí para que "Abrir carpeta"
  funcione. `ResumenDescarga` muestra el total antes/después de los comprimidos
  (`12 comprimido(s) · 45.2 MB → 12.1 MB`).
- **Vista:** botón "Descargar comprimidos" en el footer (visible con `HayDescarga`) y el
  total a la izquierda de la fila de acciones.
- **Corrección de paso:** el clon de preferencias para lotes con orígenes mixtos no copiaba
  `EscalaDeGrises` / `DpiImagenes` / `NivelCompatibilidad` — se perdían en ese caso. Añadido.
- **Pruebas:** `CopiarA_reune_los_archivos_en_la_carpeta_destino`,
  `CopiarA_omite_los_que_ya_estan_en_la_carpeta_destino`,
  `CopiarA_no_pisa_un_nombre_que_ya_existe_en_el_destino`.

---

## Parte E — Estados que parecían errores + reintentar (RF-34)

Con un lote real de ~40 PDF aparecieron filas en rojo y métricas raras. No todas eran bugs;
varias eran mala presentación.

| Síntoma | Causa | Arreglo |
|---|---|---|
| "Ghostscript terminó sin generar el archivo de salida" en PDF sanos | Carrera: con orígenes mixtos, varios PDF con el mismo nombre → una sola carpeta → `ResolverRutaSalida` (sólo mira `File.Exists`) daba la misma ruta a dos hilos; uno borraba la salida del otro | `ServicioCompresionLote` reserva la ruta bajo cerrojo y crea el archivo vacío antes de invocar al motor |
| Fila "comprimida" con `→ 0 KB` | El motor devolvía `Ok()` sin escribir; el placeholder de 0 bytes contaba como salida válida | Guard: `tamaño == 0` → `Error` ("El motor terminó sin escribir el PDF de salida"). `CompresorGhostscript` también rechaza salida de 0 bytes |
| "Ya optimizado" mostrando `16 KB → 149 KB` (parece que creció) | `Aplicar` mostraba el tamaño del intento descartado | `TamanoFinal = "—"` cuando `RutaSalida is null` (no se entregó archivo) |
| Chip verde `-0 %` | Reducción de una milésima marcada como compresión | `ReduccionLegible` devuelve `—` por debajo del 0.1 % |
| No había forma de reintentar | — | `ReintentarFallidosCommand`: reprocesa sólo las filas en `Error`, `FusionarResultados` mezcla con `_ultimosResultados`, el resumen se recalcula sobre el lote completo. Botón en el footer con `HayFallidos` |

**Nota:** "Documento escaneado: revisa la legibilidad" **no** es un error — es una nota
informativa sobre una compresión correcta (icono verde). No se toca.

**Pruebas nuevas:** `Dos_pdf_con_el_mismo_nombre_no_se_pisan_al_ir_a_una_carpeta_unica`,
`Si_el_motor_dice_ok_pero_no_escribe_nada_se_marca_Error`, y `ResultadoCompresionTests`
(reducción insignificante / real / sin ganancia). El reintento y la presentación de la fila
se validan a mano (sin pruebas de UI, deuda conocida).

---

## Parte C — Pulido y QA previo al despliegue

Checklist para cerrar antes de publicar. No todo es código.

- [x] **Recuento de pruebas.** `dotnet test` → **57** (54 unitarias + 3 de integración).
      `CLAUDE.md`, `06-TRAZABILIDAD.md`, `README.md` y `01-ARQUITECTURA.md` actualizados.
- [ ] **Tema claro.** El plan de diseño §4.6 sigue en 🟡: "falta captura visual del tema
      claro". Arrancar con `RequestedThemeVariant=Light`, revisar contraste AA del panel
      nuevo y de todos los controles añadidos.
- [ ] **Chrome de ventana propio** (§4.7, 🟡): probar a mano arrastrar, doble-clic para
      maximizar y los tres botones, en macOS y Windows.
- [ ] **Prueba de humo con Ghostscript real:** un lote con DPI personalizado + escala de
      grises + sufijo, verificando que el PDF sale, el original no se toca y los conmutadores
      llegan (revisar `compresor.log`).
- [ ] **Panel plegado/desplegado** persiste entre reinicios.
- [ ] **Migración de preferencias:** abrir con un `prefs.json` de la versión anterior (sin
      los campos nuevos) y confirmar que arranca con los defaults.
- [ ] **Textos:** que `AvisoMotor` y las notas del panel no prometan cosas que no están.
- [x] Actualizar `03-ROADMAP.md`, `06-TRAZABILIDAD.md`, `07-PLAN-DISENO.md` (§5.4/§8) y
      añadir [ADR-008](02-DECISIONES-ADR.md#adr-008). Hecho.

### Fuera de alcance de esta fase (se quedan para Fase 2/3)

- RF-11 vista previa antes/después (necesita PDFium — revisar ADR-005).
- Progreso *por archivo* (Ghostscript no lo reporta).
- Reintentar sólo los archivos fallidos.
- Patrón de renombrado con variables (RF-18, resto).
- Perfiles guardables como lista (RF-14).

---

## Orden de ejecución

1. ✅ **Core primero (sin riesgo de UI):** `PreferenciasUsuario` — campos nuevos
   (`DpiImagenes`, `EscalaDeGrises`, `NivelCompatibilidad`, `PanelConfiguracionVisible`) +
   `APerfil()` propagando DPI / grises / compatibilidad. `PreferenciasTests` (+4) y
   `CompresorGhostscriptTests` (+1) en verde.
2. ✅ **Composición:** `App.axaml.cs` pasa `FabricarMotor(string? ruta)` al VM, que
   reconstruye orquestador + compresor (para RF-30 "Volver a comprobar").
3. ✅ **ViewModel:** `[ObservableProperty]` para los 9 ajustes nuevos/expuestos, init en el
   constructor, volcado en `GuardarPreferencias()`, comandos `AlternarPanelConfigCommand` y
   `VolverAComprobarMotorCommand` (ambos persisten al instante).
4. ✅ **Vista:** panel lateral en `MainWindow.axaml` (elimina el `Flyout`), secciones
   Compresión / Salida / Motor, estilo `panel-config`, ícono `IconoPlegar`, handler
   `AlElegirGhostscript` en el code-behind. `dotnet build` limpio, sin warnings.
5. ✅ **RF-33 "Descargar comprimidos"** (Parte D) y recortes de UI pedidos tras la 1ª prueba
   en pantalla (Respaldar fuera, ruta de Ghostscript oculta, carril para reabrir el panel,
   footer a ancho completo).
6. ✅ **RF-34 reintentar fallidos + presentación de estados** (Parte E): carrera de ruta de
   salida, guard de salida vacía, `→ —` en no-entregados, sin chip `-0 %`.
7. ⬜ **QA manual** (Parte C): tema claro, chrome de ventana, humo con Ghostscript real.
8. ✅ **Documentos:** ADR-008, roadmap (Fase 6), trazabilidad, nota en el plan de diseño
   (§5.4 y §8), recuento de pruebas en `CLAUDE.md` / `README.md` / `01-ARQUITECTURA.md`.

**Falta sólo la QA manual (paso 7)** para dar la fase por cerrada.

## Impacto en documentos

| Documento | Cambio |
|---|---|
| `02-DECISIONES-ADR.md` | Nuevo **ADR-008**: configuración en panel lateral visible; revierte la decisión de `Flyout` de §5.4. |
| `03-ROADMAP.md` | Nueva **Fase 6** con su tabla RF-26…RF-34; nota en la fila §4.4 de Fase 4; el pendiente "reintentar fallidos" de Fase 2 pasa a ✅ (RF-34). |
| `06-TRAZABILIDAD.md` | Filas RF-26…RF-34; RF-18 pasa a reflejar que el sufijo fijo ya está en la UI; recuento de pruebas. |
| `07-PLAN-DISENO.md` | Nota en §5.4 y §8: la decisión del `Flyout` queda revertida por ADR-008 (mismo patrón que la nota del azul de marca en ADR-007). |
| `00-INDICE.md` | Entrada para este documento. |
| `CLAUDE.md`, `README.md`, `01-ARQUITECTURA.md` | Recuento real de pruebas (57). |

## Riesgos

- **Binding del panel.** Trasladar el contenido del `Flyout` puede romper bindings sutiles
  (los `Converter` de enum, el `NumericUpDown` del umbral con su doble unidad). Mitigación:
  mover el XAML tal cual primero, verificar que compila y arranca, y sólo después
  reorganizar en secciones.
- **RF-30 en caliente.** Reconstruir el compresor desde el VM toca la composición de
  dependencias hecha a mano. Si se complica, el *fallback* "se aplica al reiniciar" es
  aceptable para el primer despliegue.
- **Espacio horizontal.** 300 px de panel + lista en una ventana de 760 puede quedar
  apretado. Verificar en QA; el plan B es un umbral de auto-plegado.
- **Sin pruebas de UI.** El panel se valida a mano. Es la deuda técnica ya conocida; no la
  agranda, pero conviene tenerla presente al tocar tanto XAML.
