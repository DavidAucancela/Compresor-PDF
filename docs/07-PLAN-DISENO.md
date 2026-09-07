# Plan de diseño — Fase 4 (rediseño visual completo)

**Objetivo:** llevar la interfaz de "funcional y genérica" a un nivel de acabado premium,
sin tocar `CompresorPdf.Core` ni el patrón MVVM. Es un plan de **UI**, no de producto: no
añade ni quita requisitos funcionales del plan original.

---

## 1. Diagnóstico honesto del estado actual

La app funciona (Fase 1 y 2 cerradas), pero visualmente no ha pasado de un prototipo:

| Problema | Dónde se ve |
|---|---|
| Cero identidad de marca | Todo son controles `FluentTheme` por defecto, sin paleta propia |
| `DataGrid` genérico para la lista | Rejilla de tabla de escritorio de los 2000, no una lista de archivos moderna |
| Sin iconografía | Ni un solo ícono en toda la ventana — ni en los botones ni en los estados |
| Jerarquía visual plana | Todo el texto pesa igual; nada guía la vista hacia lo importante (el ahorro logrado) |
| Sin motion | Las filas aparecen de golpe, la barra de progreso no tiene personalidad, no hay feedback de hover/drag |
| Barra de título del SO | Ventana con chrome nativo genérico; ninguna app "premium" actual lo deja así |
| Estados de color arbitrarios | Los 7 estados (`Comprimido`, `Omitido`, `Protegido`...) usan hex sueltos en el ViewModel, no un sistema |

Captura de referencia: la que ya se envió (`app.png` / `app-sin-path.png`) — controles apilados
en un `WrapPanel`, sin espaciado sistemático, sin ningún color de marca.

**Lo que sí es correcto y no se toca:** la separación `Core`/`App`, el patrón MVVM, el
binding compilado, la lógica de drag & drop. El rediseño es una reescritura de `Views/` y
la incorporación de un sistema de estilos — no una reescritura de la app.

---

## 2. Dirección de diseño

**Estilo:** minimalismo funcional con acento de marca — superficie neutra (casi monocromo),
un único azul de acento (el del ícono ya generado, `#4A8FF9` → `#143FBA`), tipografía Inter
(ya incluida vía `Avalonia.Fonts.Inter`) y todo el color con propósito: si algo es azul, es
interactivo o es el elemento más importante de la pantalla en ese momento. Nada decorativo.

**Referencias de calidad** (no de estilo exacto, sino de nivel de acabado): Linear, Raycast,
Arc — herramientas de escritorio/utilidad con identidad fuerte pero minimalista, no dashboards
recargados. Encaja con lo que es esta app: una herramienta de un solo propósito que se abre,
se usa 30 segundos y se cierra.

**Lo que esto significa en la práctica:**
- Superficie de fondo casi plana, con **una** superficie elevada (la tarjeta de resultados).
- El acento azul se reserva para: el botón primario, el estado "Comprimido", los elementos
  arrastrando-encima, y el número de ahorro total (el dato que el usuario vino a ver).
- Iconografía lineal de 1.5px de grosor, no rellena — coherente con el ícono de la app.
- Nada de gradientes decorativos, sombras exageradas, ni glassmorphism. Los degradados
  fuertes se reservan sólo para el ícono de marca, no para la UI de trabajo.

---

## 3. Decisión técnica: base de componentes

**Problema concreto:** Avalonia trae `FluentTheme`, que da controles correctos pero
genéricos — literalmente los mismos que cualquier app Avalonia sin personalizar (se ve en
la captura actual). Para llegar a un acabado premium hay tres caminos:

| Opción | Qué da | Coste |
|---|---|---|
| **A. `FluentTheme` + `ControlTheme` a mano** | Control total, cero dependencias nuevas | Alto: hay que rehacer plantillas de botón, checkbox, combobox, etc. desde cero para que dejen de verse "de fábrica" |
| **B. Semi.Avalonia** | Tema completo ya pulido (botones, inputs, badges, switches) con sistema de tokens de color propio, MIT, activo | Una dependencia nueva; hay que aprender su sistema de tokens |
| **C. FluentAvaloniaUI** | Controles WinUI (Mica, `InfoBar`, `NavigationView`) | Mica es exclusivo de Windows 11 — en macOS se degrada a un panel plano, rompiendo la consistencia entre plataformas que es un requisito del proyecto (ADR-006) |

**Decisión: Opción B, Semi.Avalonia.** Documentada como [ADR-007](02-DECISIONES-ADR.md#adr-007--base-de-componentes-para-el-rediseño-semiavalonia)
y confirmada. Motivo: es la única
que da un salto real de acabado en el tiempo disponible sin fragmentar la experiencia entre
macOS y Windows (a diferencia de C, que depende de una API exclusiva de Windows 11). La
paleta y tipografía de marca se superponen sobre sus tokens; Semi.Avalonia se usa como
motor de componentes, no como su look-and-feel por defecto.

`Avalonia.Controls.DataGrid` se retira: la lista de archivos pasa a ser un `ItemsControl`
con `VirtualizingStackPanel` y una plantilla de tarjeta por fila (sección 5.3). Es la pieza
de mayor cambio visual de todo el plan.

---

## 4. Sistema de diseño (tokens)

Todo color, espaciado y radio sale de un diccionario de recursos — nada de valores sueltos
en el XAML de las vistas, que es el problema actual (`Background="#14808080"` a pelo en la
línea 19 de `MainWindow.axaml`).

### 4.1 Color — semántico, no literal

```
Superficie
  --fondo              fondo de la ventana
  --superficie         tarjetas (la lista de resultados, el panel de ajustes)
  --superficie-hover   fila bajo el cursor
  --borde              separadores, contornos de tarjeta

Texto
  --texto-primario     nombres de archivo, valores importantes
  --texto-secundario   etiquetas, tamaños, metadatos
  --texto-terciario    texto deshabilitado, hints

Marca / estado
  --acento             azul de marca — interactivo, "Comprimido", ahorro total
  --exito              verde — reservado a la confirmación final del lote
  --aviso              ámbar — "Cancelado", avisos no bloqueantes
  --peligro            rojo — "Error", "Corrupto"
  --neutral            gris — "Omitido", "Protegido" (no son errores, son decisiones correctas)
```

Cada token tiene su par claro/oscuro. Esto sustituye directamente a
`FilaResultadoViewModel.EstadoColor`, que hoy devuelve strings hex — pasará a devolver
**una clave de estado** (`"exito"`, `"neutral"`, `"peligro"`...) y la vista resuelve el
color real vía `DynamicResource`, para que cambiar de tema no rompa nada y para que el
color de marca se pueda ajustar en un solo sitio.

### 4.2 Tipografía

Inter ya está incluido. Escala de 5 pasos, no más:

| Uso | Tamaño | Peso |
|---|---|---|
| Métrica destacada (ahorro total) | 28px | SemiBold |
| Título de sección | 15px | SemiBold |
| Cuerpo (nombre de archivo, valores) | 13px | Regular |
| Metadato (tamaño, ruta) | 12px | Regular, texto secundario |
| Etiqueta pequeña (chip de estado) | 11px | Medium, mayúsculas con tracking |

### 4.3 Espaciado y radio

Grid de 4px (4/8/12/16/24/32). Radio: 6px en controles pequeños (botones, chips), 12px en
tarjetas y contenedores. Hoy la ventana mezcla `CornerRadius="8"` con paddings sueltos
(`12`, `16`, `4,10,4,10`) sin ninguna relación entre sí — eso desaparece.

### 4.4 Elevación

Sólo dos niveles: superficie plana (fondo) y superficie elevada (tarjetas), diferenciadas
por color de fondo + un borde de 1px, no por sombra. Una sombra sutil (`BoxShadow`) sólo en
elementos flotantes reales: el estado "arrastrando archivo encima".

### 4.5 Movimiento

| Interacción | Duración | Curva |
|---|---|---|
| Hover (botón, fila) | 120 ms | ease-out |
| Fila nueva entra a la lista | 180 ms | ease-out, fade + desplazamiento de 8px |
| Barra de progreso | 250 ms | ease-in-out (ya lo hace Avalonia por defecto; se afina) |
| Icono de estado "Procesando" | continuo | rotación lineal 900ms/vuelta |
| Borde de la zona de drop al arrastrar encima | 150 ms | pulso de opacidad del borde de acento |

Se implementa con `Transitions` de Avalonia (soportado de forma nativa, sin librerías de
animación externas).

---

## 5. Rediseño por zona

### 5.1 Barra de título

Se extiende el área de cliente (`ExtendClientAreaToDecorationsHint="True"`) para pintar una
barra de título propia, de 44px, idéntica en macOS y Windows (decisión confirmada, sección
8): nombre de la app a la izquierda, ícono de ajustes (▤, abre el panel colapsable de 5.4)
a la derecha, sin más adorno. En macOS esto sustituye a los tres semáforos nativos por
controles propios con la misma función; en Windows sustituye a los botones min/max/cerrar
del sistema. Requiere manejar el arrastre de ventana (`PointerPressed` sobre la barra) y
los tres botones de ventana a mano — es el ítem de mayor riesgo técnico de la fase 4.1,
porque el comportamiento nativo de doble-clic-para-maximizar y las zonas de resize hay que
reimplementarlas.

### 5.2 Zona de carga (estado vacío y drag & drop)

Hoy es un texto centrado dentro de un borde delgado. Pasa a ser el elemento dominante de la
pantalla cuando no hay archivos:

- Ícono de "subir documento" (línea, 48px, en `--texto-terciario`), centrado.
- Texto primario: "Arrastra tus PDFs aquí".
- Texto secundario: "o pulsa para elegir archivos" — **el texto entero es clicable**, no
  hace falta un botón aparte para el caso vacío (el botón "Añadir archivos…" del footer
  se mantiene para cuando ya hay una lista).
- Al arrastrar un archivo encima de la ventana: el borde de la zona pasa a
  `--acento` con el pulso de opacidad de la sección 4.5, y el ícono cambia a una flecha
  hacia abajo. Hoy no hay ningún feedback visual de "esto va a funcionar si sueltas aquí".

### 5.3 Lista de archivos — de tabla a tarjetas

Sustituye el `DataGrid`. Cada archivo es una fila-tarjeta con esta estructura horizontal:

```
[ícono de estado]  Nombre-del-archivo.pdf                    2.23 MB → 246 KB   [-89.2%]
                    Detalle o mensaje, si lo hay                                 Comprimido
```

- El ícono de estado reemplaza al texto de estado como primera señal (ver 5.5): un check
  verde, un guion gris, una espiral azul girando, una equis roja.
- Los tamaños "original → final" van en una sola línea tabular (números alineados, fuente
  monoespaciada para los tamaños — es un detalle de pulido real: los números deben alinear
  verticalmente entre filas).
- El porcentaje de reducción es un **chip** con el color de éxito, no texto plano — es el
  dato que más quiere ver el usuario.
- Fila con `--superficie-hover` al pasar el cursor.
- Virtualizado (`VirtualizingStackPanel`) para que lotes de cientos de archivos no
  degraden el scroll — el `DataGrid` actual ya virtualiza, así que esto es mantener una
  propiedad existente, no una regresión.

### 5.4 Panel de ajustes

Hoy es un `WrapPanel` con etiquetas y controles sueltos sin agrupación. Pasa a:

- Fila única con tres controles agrupados visualmente (umbral, nivel, checkboxes) con
  separadores verticales sutiles entre grupos, no una caja de fondo gris genérica
  (`Background="#14808080"` actual).
- El umbral usa un ícono de balanza o filtro antes del campo numérico.
- El nivel de compresión (Bajo/Medio/Alto) dijo de pasar de `ComboBox` a un
  **segmented control** de 3 opciones — es una elección entre 3 valores fijos, no una
  lista abierta; un selector segmentado comunica eso mejor y es más rápido de usar.
- **El panel completo se colapsa** detrás de un ícono de ajustes (▤) en la barra de título
  (decisión confirmada, sección 8). Por defecto la ventana muestra sólo la zona de carga y
  la lista; los ajustes se abren en un popover/flyout anclado al ícono, con los valores
  actuales (umbral y nivel) resumidos en una línea pequeña cuando el panel está cerrado,
  para que no queden "escondidos a ciegas".

### 5.5 Iconografía

Sin dependencias de íconos externas (`Material.Icons.Avalonia`, etc.) para no repetir el
patrón de "una librería por cada cosita" — se define un `IconosApp.axaml` con
`<Geometry>` reutilizables (igual de barato que un `Path`, cero peso de assets, se colorea
vía `DynamicResource` automáticamente en cada tema). Set mínimo necesario:

| Ícono | Uso |
|---|---|
| Documento con flecha abajo | Zona de carga vacía, marca de la app |
| Check | Estado "Comprimido" |
| Guion / minus | Estado "Omitido" / "Ya optimizado" |
| Candado | Estado "Protegido" |
| Advertencia (triángulo) | Estado "Corrupto" / "Error" |
| X circular | Estado "Cancelado" |
| Espiral (con animación de rotación) | Estado "Procesando" |
| Carpeta | Botón "Elegir carpeta" |
| Escoba / trash | Botón "Limpiar" |
| Más | Botón "Añadir archivos" |

10 íconos cubren el 100% de la interfaz. Se dibujan a mano en un editor SVG simple (o se
reaprovecha el flujo de Ghostscript ya usado para el ícono de marca) y se convierten a
`Geometry` de Avalonia con la herramienta `PathMarkupConverter` o pegando el `d=` del SVG
directamente — es sintaxis compatible.

### 5.6 Progreso y resumen

- La barra de progreso pasa de una línea genérica de 6px a una versión con extremos
  redondeados y color de acento con un leve brillo animado mientras avanza.
- El resumen final dijo de ser texto plano (`"3 comprimidos · 1 omitido..."`) a una fila de
  **estadísticas destacadas**: el ahorro total en grande (tipografía de 28px, sección 4.2)
  con las demás cifras (comprimidos/omitidos/fallidos) como métricas secundarias alrededor.
  Es el momento de mayor satisfacción del usuario — hoy se entierra en una línea de texto
  del mismo tamaño que todo lo demás.

### 5.7 Modo claro / oscuro

Los tokens de la sección 4.1 se definen para ambos desde el principio (no se retrofita
después). Se verifica contraste AA (4.5:1 texto normal, 3:1 texto grande) en ambas
variantes antes de cerrar la fase — hoy el modo claro ni siquiera se ha mirado, sólo se ha
probado en oscuro (ver capturas enviadas).

---

## 6. Estructura de archivos que se añade

```
src/CompresorPdf.App/
├── Styles/
│   ├── Tokens.axaml            colores, tipografía, espaciado, radio (sección 4)
│   ├── Tokens.Claro.axaml      overrides del tema claro
│   ├── Tokens.Oscuro.axaml     overrides del tema oscuro
│   ├── Iconos.axaml            geometrías reutilizables (sección 5.5)
│   └── Controles.axaml         ControlTheme para botón primario/secundario, chip, tarjeta
├── Views/
│   ├── MainWindow.axaml        rehecha con el layout de la sección 5
│   ├── ZonaCarga.axaml         UserControl: estado vacío + drag&drop (5.2)
│   ├── FilaArchivo.axaml       UserControl: tarjeta de resultado (5.3)
│   └── PanelAjustes.axaml      UserControl: controles de configuración (5.4)
```

`ViewModels/` cambia mínimamente: `FilaResultadoViewModel.EstadoColor` pasa a
`EstadoClave` (string semántico en vez de hex), y se añade un
`ClaveIcono` que la vista resuelve contra `Iconos.axaml`. Cero cambios en `Core`.

---

## 7. Fases de ejecución

| Fase | Entregable | Estado |
|---|---|---|
| **4.0 — Fundamentos** | Semi.Avalonia integrado (ADR-007), `Styles/Iconos.axaml` (12 íconos propios), `Styles/Controles.axaml` | ✅ hecho |
| **4.1 — Shell de ventana** | Barra de título propia (`ExtendClientAreaChromeHints="NoChrome"`), arrastre, min/max/cerrar | ✅ hecho |
| **4.2 — Zona de carga** | Estado vacío clicable + feedback de drag&drop (borde de acento pulsante) | ✅ hecho |
| **4.3 — Lista de archivos** | Tarjetas con `ItemsControl` + `VirtualizingStackPanel`, retira el `DataGrid`, iconografía por estado | ✅ hecho, verificado con Ghostscript real |
| **4.4 — Panel de ajustes** | Colapsable en un `Flyout` desde la barra de título, selector segmentado de nivel | ✅ hecho |
| **4.5 — Progreso y resumen** | Barra pulida + tarjeta de métricas destacadas (ahorro en grande) | ✅ hecho, verificado con Ghostscript real |
| **4.6 — Verificación claro/oscuro** | Build y arranque sin excepciones en ambos temas | 🟡 parcial — ver nota abajo |
| **4.7 — Pulido y QA visual** | Micro-interacciones, prueba con Ghostscript real | 🟡 parcial — ver nota abajo |

Implementadas 4.0–4.5 completas. En 4.6 se dejó `RequestedThemeVariant="Default"` (sigue al
sistema) tras confirmar que el tema claro **compila y arranca sin excepciones** — pero no se
consiguió una captura de pantalla del tema claro: el entorno de esta sesión tiene múltiples
Spaces/monitores de macOS y la ventana de la app se abría reproducible en un Space distinto al
que `screencapture` podía fotografiar, algo ajeno a la propia app. Queda pendiente una
verificación visual humana de ambos temas y de contraste AA.

En 4.7, las micro-interacciones de hover/focus/transición SÍ están implementadas (Styles/Controles.axaml:
resalte de fila, pulso del borde al arrastrar, ícono de "Procesando" girando). Lo que falta y
requiere confirmación manual del usuario es la interacción real con el chrome de ventana propio
(arrastrar para mover, doble-clic para maximizar, clics en los tres botones): el código usa las
APIs estándar de Avalonia (`BeginMoveDrag`, `WindowState`, `Close()`) sin lógica propia de
riesgo, pero no se pudo hacer clic real sobre ellos en esta sesión por la misma causa de
Spaces/monitores — los intentos de clic por coordenadas aterrizaban en otras ventanas del
escritorio. **Antes de dar la Fase 4 por cerrada del todo, prueba a mano**: arrastrar la
ventana por la barra de título, doble-clic para maximizar/restaurar, y los tres botones.

### Desviación registrada respecto al plan original

La sección 6 proponía `UserControl` separados (`ZonaCarga.axaml`, `FilaArchivo.axaml`,
`PanelAjustes.axaml`). En la implementación se dejaron **inline** dentro de `MainWindow.axaml`
(plantillas de datos y el contenido del `Flyout`) para reducir el riesgo de errores de binding
entre archivos en una sola pasada grande. El resultado visual y funcional es idéntico; separar
esas plantillas en sus propios archivos sigue siendo una refactorización mecánica de bajo riesgo
si se quiere hacer después.

### Hallazgos técnicos durante la implementación (bugs reales encontrados y corregidos)

- **`InvariantGlobalization=true`** (fijado en `Directory.Build.props` desde la Fase 1) rompe la
  inicialización estática de Semi.Avalonia, que construye `CultureInfo` reales. Se desactivó
  sólo en `CompresorPdf.App.csproj`.
- **Animar `RenderTransform` directamente falla en tiempo de ejecución** ("No animator
  registered for the property RenderTransform"). El giro del ícono "Procesando" anima
  `RotateTransform.Angle` en su lugar — verificado con una app de prueba mínima antes de
  aplicarlo.
- **Una línea de ícono perfectamente horizontal tiene una caja delimitadora de altura cero**, y
  `Stretch="Uniform"` la escala mal (sale casi invisible) — le pasó al ícono de minimizar. Se
  corrigió con una pendiente imperceptible (`M5 12 L19 12.01` en vez de `M5 12 H19`).

---

## 8. Decisiones — confirmadas

Las tres decisiones abiertas de este plan ya están resueltas:

1. **Base de componentes: Semi.Avalonia.** [ADR-007](02-DECISIONES-ADR.md#adr-007--base-de-componentes-para-el-rediseño-semiavalonia).
2. **Panel de ajustes: colapsable**, detrás del ícono de ajustes en la barra de título
   (sección 5.1), con un resumen de una línea del umbral y nivel actuales cuando está
   cerrado.
3. **Barra de título: personalizada, idéntica en macOS y Windows** — sustituye tanto los
   semáforos de macOS como los botones min/max/cerrar de Windows por controles propios.

Con esto no quedan bloqueos: la fase 4.0 puede empezar.

---

## 9. Qué NO cambia

- `CompresorPdf.Core`: cero cambios. Ningún requisito funcional (RF/RNF) se toca.
- Las 39 pruebas existentes siguen pasando tal cual — no prueban UI.
- El patrón MVVM y el binding compilado.
- El empaquetado (`build/`): el ícono de marca ya generado se conserva; puede refinarse en
  4.0 si el nuevo acento de color diverge del azul actual, pero no es necesario.

## 10. Riesgos

- **La barra de título propia es el punto de mayor riesgo técnico** (sección 5.1):
  arrastre de ventana, doble-clic para maximizar, zonas de resize en los bordes y el
  comportamiento de los tres botones deben reimplementarse a mano en ambos SO. Se aísla
  en la fase 4.1 precisamente para descubrir pronto si algo de esto no se comporta bien
  en Avalonia antes de construir el resto encima.
- **Semi.Avalonia es una dependencia de terceros activa pero más joven que Avalonia
  mismo** — si un futuro upgrade de Avalonia rompe compatibilidad, hay que esperar su
  actualización. Mitigación: la app no depende de ningún control exclusivo de Semi que no
  se pueda reimplementar (son botones, inputs, checkboxes — reemplazables).
- **Virtualizar la lista de tarjetas es más trabajo que el `DataGrid`**, que ya lo trae
  gratis. Se verifica explícitamente en la fase 4.3 con un lote grande antes de darla por
  cerrada.
- **Doblar el trabajo de QA:** cada componente nuevo se prueba en claro y oscuro. Se
  absorbe teniendo los tokens correctos desde 4.0 en vez de parchear después.
