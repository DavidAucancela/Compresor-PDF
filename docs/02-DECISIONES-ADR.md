# Decisiones de arquitectura (ADR)

El plan original dejaba cinco decisiones abiertas. Aquí están resueltas, con su motivo y su
coste. Cada una indica qué haría falta para revertirla.

---

## ADR-001 · Framework de UI: Avalonia 11

**Estado:** aceptada · **Sustituye a:** la pregunta 1 del plan original

**Decisión:** la aplicación se construye con Avalonia 11 sobre .NET 10.

**Motivo.** Las tres opciones del plan se evaluaron contra dos restricciones reales:
desarrollas en un Mac y el público objetivo puede incluir Windows.

- **WPF** obliga a tener un entorno Windows para compilar y probar. Con un Mac como máquina
  principal, cada iteración pasaría por una VM. Es el mayor coste diario de los tres.
- **MAUI** compila en Mac, pero su soporte de escritorio es el menos maduro de los tres y
  su punto fuerte (móvil) aquí no se aprovecha.
- **Avalonia** compila y se ejecuta nativamente en el Mac, está enfocado a escritorio y usa
  XAML y MVVM, así que el conocimiento es transferible a WPF si algún día hiciera falta.

**Coste que aceptamos.** Avalonia es un proyecto de comunidad, no de Microsoft; hay menos
respuestas en internet que para WPF y algunos controles (el `DataGrid`) van en paquetes aparte.

**Cómo se revierte.** El proyecto `Core` no tiene ninguna referencia a Avalonia. Cambiar de
framework significa reescribir `CompresorPdf.App` — dos ViewModels y una vista — sin tocar
la lógica ni las pruebas.

---

## ADR-002 · Motor de compresión: Ghostscript como proceso externo

**Estado:** aceptada

**Decisión:** la compresión la hace el binario `gs` invocado como proceso externo, detrás de
la interfaz `ICompresorPdf`.

**Motivo.** Es el único de los candidatos que reduce de verdad el peso de las imágenes, que
es donde está el tamaño de un PDF real. Las medidas de este proyecto: **2.23 MB → 246 KB
(−89.2 %)** con el perfil balanceado. Además funciona igual en macOS y Windows, y conserva
el texto como vectores, así que el PDF resultante **sigue siendo buscable** (pregunta 4 del
plan original, resuelta a favor de preservar la capa de texto).

Los tres niveles del RF-10 se mapean a los perfiles nativos de Ghostscript:

| Nivel | PDFSETTINGS | DPI | Uso |
|---|---|---|---|
| Bajo | `/printer` | 300 | Impresión |
| Medio *(por defecto)* | `/ebook` | 150 | Uso general |
| Alto | `/screen` | 72 | Email y pantalla |

**Coste que aceptamos.** Ghostscript es un requisito externo: si no está instalado, la app
lo detecta y lo dice en la cabecera, pero no puede comprimir. Es un paso más en la
instalación, y el precio de no heredar su licencia (ver ADR-003).

**Cómo se revierte.** Escribir otra clase que implemente `ICompresorPdf` y cambiarla en
`App.axaml.cs`. Nada más depende de Ghostscript.

---

## ADR-003 · Licencia: Ghostscript NO se distribuye con la aplicación

**Estado:** aceptada · **Es la decisión con más consecuencias legales del proyecto**

**Decisión:** el instalador **no incluye** el binario de Ghostscript. El usuario lo instala
por su cuenta y la app lo localiza en el sistema.

**Motivo.** Ghostscript es AGPL. La AGPL se activa por *distribución*: si empaquetáramos
`gs` dentro del instalador, estaríamos distribuyendo software AGPL junto al nuestro y
tendríamos que publicar el código fuente de la aplicación completa bajo AGPL, o comprar
una licencia comercial a Artifex. Al no distribuirlo, la app se limita a invocar un
programa que ya está en la máquina del usuario, igual que quien lo llama desde una terminal.

**Qué NO puedes hacer sin revisar esta decisión:**

- Meter `gs` (o sus DLL/dylib) en el `.dmg`, `.pkg`, `.msi` o `.msix`.
- Descargar Ghostscript automáticamente desde la app como parte de la instalación.
- Enlazar contra la librería `libgs` en lugar de invocar el ejecutable.

**Si en el futuro hace falta una experiencia "instalar y listo"**, hay tres caminos, en
orden de coste creciente:

1. Que el instalador *detecte* la ausencia y guíe al usuario a la web oficial (sin descargar
   por él). Coste: cero legal, un poco de fricción.
2. Licencia comercial de Artifex. Coste: dinero, cero fricción.
3. Cambiar de motor a uno con licencia permisiva (Docotic.Pdf es comercial pero sin AGPL;
   PDFsharp es MIT pero comprime mucho menos). Coste: reescribir `ICompresorPdf`.

**Acción pendiente:** si el destino final es distribuir a clientes, confirma esta decisión
con quien lleve el tema legal **antes** de construir el instalador.

---

## ADR-004 · El archivo original nunca se modifica

**Estado:** aceptada · **Sustituye a:** la pregunta 5 del plan original

**Decisión:** la compresión siempre escribe en un archivo nuevo. No existe modo
"sobrescribir", ni siquiera con confirmación.

**Motivo.** El daño de un fallo es asimétrico: un PDF de salida corrupto se vuelve a generar;
un original perdido, no. Además elimina toda una clase de errores (escritura a medias, proceso
matado, disco lleno) en lugar de gestionarlos.

**Cómo se materializa en el código:**

- `GestorArchivos.ResolverRutaSalida` nunca devuelve la ruta del original, y si la salida
  coincidiría con él le añade un sufijo.
- Si el destino ya existe, añade `(2)`, `(3)`… en vez de pisarlo.
- Si Ghostscript falla o se cancela, el archivo de salida a medias se borra.
- Si el resultado no es más pequeño, se descarta y el estado es `SinGanancia`.
- El respaldo (`originales/`) sigue existiendo como opción para quien quiera una copia extra.

Hay una prueba dedicada a esto: `El_archivo_original_nunca_se_modifica`.

---

## ADR-005 · Análisis del PDF por heurística, sin librerías externas

**Estado:** aceptada

**Decisión:** `AnalizadorPdf` inspecciona bytes buscando marcadores en lugar de parsear el
formato PDF. `Core` no tiene ninguna dependencia NuGet.

**Motivo.** Lo único que necesitamos decidir es *cómo tratar* el archivo: si está protegido,
si es válido y si conviene avisar de que es un escaneo. Para eso, buscar `/Encrypt`, `%PDF-`
y `/Font` es suficiente y cuesta cero dependencias, cero licencias y microsegundos.

**Coste que aceptamos.** Falsos positivos posibles en el detector de "escaneado". El efecto
máximo es un aviso de más en la columna Detalle; no cambia el resultado de la compresión.

**Cuándo revisarla.** Al implementar la vista previa (RF-11) hará falta renderizar páginas
de verdad, y ahí entrará una librería (PDFium es BSD y sirve bien para render). En ese
momento tiene sentido reimplementar `IAnalizadorPdf` sobre ella.

---

## ADR-006 · Plataformas objetivo: macOS y Windows

**Estado:** aceptada · **Sustituye a:** la pregunta 2 del plan original

**Decisión:** ambas, desde una única base de código.

**Motivo.** Es consecuencia directa del ADR-001 y no cuesta trabajo extra: Avalonia y
Ghostscript funcionan en las dos. El código que depende del sistema operativo está aislado
en dos sitios — `LocalizadorGhostscript` (dónde vive el binario, `gswin64c.exe` vs `gs`) y
`RutasApp` (dónde van preferencias y logs).

Linux funcionaría casi con seguridad, pero no está en el alcance ni se prueba.

---

## ADR-007 · Base de componentes para el rediseño: Semi.Avalonia

**Estado:** aceptada · **Contexto:** [07-PLAN-DISENO.md](07-PLAN-DISENO.md), sección 3

**Decisión:** sustituir `FluentTheme` (los controles genéricos de Avalonia) por
**Semi.Avalonia** como motor de componentes, con la paleta y tipografía de marca
superpuestas encima de sus tokens.

**Motivo.** Para llegar a un acabado premium había tres caminos: personalizar `FluentTheme`
a mano control por control (control total, pero rehacer plantillas de botón/checkbox/combo
desde cero), adoptar FluentAvaloniaUI (controles WinUI con Mica — pero Mica es exclusivo de
Windows 11; en macOS se degradaría a un panel plano, rompiendo la consistencia entre
plataformas que exige el ADR-006), o adoptar Semi.Avalonia, que da un tema ya pulido y
consistente en ambos sistemas operativos sin depender de una API específica de ninguno.

**Coste que aceptamos.** Una dependencia de terceros más (MIT, activa) además de Ghostscript
y CommunityToolkit.Mvvm. Ninguna funcionalidad de la app dependerá de un control exclusivo
de Semi que no se pueda reimplementar a mano si hiciera falta (son botones, inputs y
checkboxes, no lógica de negocio).

**Cómo se revierte.** El impacto está contenido en `src/CompresorPdf.App/Styles/` y las
vistas; `Core` no la referencia en absoluto. Si en el futuro se prefiere volver a `FluentTheme`
personalizado a mano (opción A del plan de diseño), el cambio no toca ni el ViewModel ni el
motor de compresión.

**Hallazgo durante la implementación — el azul de marca NO se puede inyectar en Semi.Avalonia.**
Se intentó sobreescribir `SemiColorPrimary` (y su familia `…Pointerover`/`…Active`) desde
`Application.Resources` para forzar el azul de marca (`#4A8FF9`/`#143FBA`) en toda la UI.
Prueba empírica: una app mínima con esa sobreescritura no cambia el color de un botón
`Classes="Primary"` — sigue saliendo el azul propio de Semi. Motivo: Semi resuelve esa cadena
de alias (`ButtonDefaultPrimaryForeground → SemiColorPrimary → SemiBlue5Color`) con
`StaticResource` dentro de sus propios diccionarios, que se resuelve una sola vez al cargar el
paquete — nunca vuelve a mirar `Application.Resources`. Solo `DynamicResource` (o los
`ThemeDictionaries` con los que arma el propio Semi) reaccionan a algo externo, y esa cadena en
particular no lo usa.

**Decisión de fallback, ya aplicada:** la UI adopta el azul propio de Semi.Avalonia
(`SemiColorPrimary`, familia `#0064FA`) como único acento en vez de forzar el de marca. Es de
la misma familia visual que el degradado del ícono de la app (`#4A8FF9`→`#143FBA`), así que no
hay choque perceptible, y evita abrir una guerra de recursos internos de una librería de
terceros por una diferencia de tono que nadie notaría. El resto del sistema de estados
(éxito/aviso/peligro/neutral) usa igualmente los tokens semánticos ya provistos por Semi
(`SemiColorSuccess`, `SemiColorWarning`, `SemiColorDanger`, `SemiColorText…`) en vez de una
paleta propia paralela — menos superficie que mantener, mismo resultado sistemático.

---

## ADR-008 · Configuración en panel lateral visible (revierte el `Flyout` de §5.4)

**Estado:** aceptada · **Contexto:** [09-PLAN-CONFIG-VISIBLE-Y-PULIDO.md](09-PLAN-CONFIG-VISIBLE-Y-PULIDO.md), Parte A

**Decisión:** los ajustes dejan de vivir en un `Flyout` colgado del ícono de engranaje de la
barra de título y pasan a un **panel lateral fijo, plegable** a la derecha del contenido. El
botón de engranaje deja de abrir un popover y pasa a plegar/desplegar ese panel. El estado
(plegado o no) se persiste entre sesiones (RNF-07).

**Qué revierte.** El plan de diseño ([07-PLAN-DISENO.md](07-PLAN-DISENO.md) §5.4 y §8) fijó
como "decisión confirmada" que el panel completo se colapsara detrás del ícono y se abriera
como popover/flyout, con sólo un resumen de una línea visible cuando estaba cerrado. Ese
resumen (`ResumenAjustes`) se conserva, ahora en la cabecera del panel plegado.

**Motivo.** La configuración determina *con qué* se comprime —umbral, nivel, DPI, escala de
grises, carpeta y sufijo de salida, motor—. Tenerla tras un clic la volvía fácil de ignorar
y difícil de auditar de un vistazo antes de lanzar un lote. Un panel siempre presente (aunque
plegable para recuperar espacio) hace que el estado activo sea visible sin interacción. La
Fase 6 además añade varios ajustes nuevos (RF-27…RF-32) que no cabían cómodos en un popover.

**Coste que aceptamos.** ~300 px de ancho cuando el panel está abierto; en la ventana a su
`MinWidth` (760) la lista queda en ~416 px, usable pero justa. Si molesta en pruebas
manuales, el plan B es un umbral de auto-plegado por ancho.

**Cómo se revierte.** El impacto está contenido en `MainWindow.axaml` (estructura de la
grilla de contenido y el bloque del panel), un puñado de propiedades del `MainWindowViewModel`
y `PreferenciasUsuario.PanelConfiguracionVisible`. `Core` no se entera. Volver al `Flyout` es
recolocar ese mismo XAML dentro de un `<Button.Flyout>`.
