# Guía de desarrollo

## Convenciones

**El código y los comentarios están en español.** Nombres de clases, métodos, variables y
mensajes al usuario. La excepción son los términos que no tienen traducción establecida en
.NET (`Task`, `CancellationToken`, `IProgress`) y las palabras clave del lenguaje.

**Nada de UI en `Core`.** Si te encuentras necesitando `Avalonia.*` dentro de `Core`, la
lógica está en el sitio equivocado. Lo que necesita hablar con el sistema (diálogos,
drag & drop) va en el *code-behind* de la vista, que es fino a propósito.

**Los comentarios explican el porqué, no el qué.** El código ya dice lo que hace. Los
comentarios que hay en el proyecto marcan reglas de negocio (`// RF-03: la regla de omisión…`)
o justifican una decisión no obvia (`// Un PDF a medio escribir es peor que ninguno`).

**Toda regla del plan tiene una prueba.** Si añades una regla nueva, añade la prueba que la
fija. La trazabilidad está en [06-TRAZABILIDAD.md](06-TRAZABILIDAD.md).

## Recetas

### Cambiar de motor de compresión

1. Implementa `ICompresorPdf` en `Core/Services/`.
2. Cámbialo en `App.axaml.cs`, método `CrearViewModel`.
3. No toques nada más: el orquestador, la UI y las pruebas dependen de la interfaz.

### Añadir un ajuste de usuario

1. Propiedad en `Config/PreferenciasUsuario.cs`, con su valor por defecto.
2. Propiedad `[ObservableProperty]` en `MainWindowViewModel`, inicializada en el constructor.
3. Volcarla en `GuardarPreferencias()`.
4. Control en `Views/MainWindow.axaml`.
5. Prueba en `PreferenciasTests` de que sobrevive al ciclo guardar/cargar.

El JSON antiguo sigue cargando: las propiedades que falten toman su valor por defecto.

### Añadir un estado de resultado

1. Valor en `Models/EstadoCompresion.cs`.
2. Texto en `FilaResultadoViewModel.EstadoTexto`, ícono en `.IconoClave` (clave de
   `Styles/Iconos.axaml`), y clasifícalo en una de las clases semánticas existentes
   (`EsExito`/`EsNeutral`/`EsAviso`/`EsPeligro`/`EsAcento`) — el color sale solo de
   `Styles/Controles.axaml` vía los tokens de Semi.Avalonia (ADR-007), nunca un hex suelto.
3. Clasificarlo en `ResumenLote.De` (¿cuenta como omitido, fallido o comprimido?).
4. Prueba del camino que lleva a ese estado.

### Probar algo que llama a Ghostscript

Usa `EjecutorEspia` (en `Fixtures/Dobles.cs`): captura los argumentos y simula la salida sin
lanzar procesos. Para una prueba contra el Ghostscript real, marca el método con
`[FactSiHayGhostscript]` y se saltará sola donde no esté instalado.

## Estructura de una prueba

Los nombres describen la regla en español, con guiones bajos:

```csharp
[Fact]
public async Task Un_pdf_por_debajo_del_umbral_se_omite_sin_llamar_al_motor()
```

Cuando la prueba toca disco, usa `CarpetaTemporal`, que se limpia sola:

```csharp
using var tmp = new CarpetaTemporal();
var ruta = tmp.CrearPdf("grande.pdf", bytes: 3 * 1024 * 1024);
```

`CrearPdf` genera un PDF sintético con cabecera válida y el tamaño exacto que pidas, y acepta
`protegido:` y `conFuentes:` para provocar cada rama del analizador.

## Trampas conocidas

**`Progress<T>` despacha de forma asíncrona.** Si cuentas eventos de progreso en una prueba,
necesitas un `await Task.Delay` antes de contar. Por lo mismo, el ViewModel reconcilia las
filas con la lista final de resultados al terminar el lote: el último `Report` puede llegar
después.

**`Directory.EnumerateFiles` es perezosa.** Un `try/catch` alrededor de la llamada no captura
nada; la excepción salta al iterar. Tenlo presente si tocas `GestorArchivos.ExpandirCarpetas`.

**Cancelar debe matar el árbol de procesos.** `EjecutorProceso` usa
`Kill(entireProcessTree: true)`; sin eso Ghostscript sobrevive al `CancellationToken` y sigue
escribiendo el PDF.

**`dotnet run --nologo -- ruta` no hace lo que parece.** Los argumentos que `dotnet run` no
reconoce se pasan a la aplicación, así que `--nologo` acabará siendo tu `args[0]`. Ejecuta el
`.dll` directamente cuando le pases argumentos posicionales.
