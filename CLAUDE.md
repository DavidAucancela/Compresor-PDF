# Compresor de PDFs

App de escritorio (.NET 10 + Avalonia 11) que comprime PDFs por lotes vía Ghostscript,
sólo cuando superan un umbral configurable. macOS y Windows. 100 % offline.

## Comandos

```bash
dotnet run --project src/CompresorPdf.App   # ejecutar
dotnet test                                 # 43 pruebas
dotnet build -c Release

./build/publicar-macos.sh                   # .app + .dmg → artifacts/macos/
./build/icono.sh                            # regenerar .icns y .ico desde icono.ps
```

Requiere `dotnet` 10 y `gs` en el PATH (`brew install dotnet ghostscript`).
En macOS puede hacer falta `export PATH="/opt/homebrew/bin:$PATH"`.

## Reglas del proyecto

1. **El archivo original nunca se modifica.** No hay modo sobrescribir, ni con confirmación.
   Ver ADR-004.
2. **`CompresorPdf.Core` no conoce la UI.** Cero referencias a Avalonia, cero paquetes NuGet.
   Si necesitas Avalonia ahí, la lógica está en el sitio equivocado.
3. **Ghostscript no se distribuye con la app** — es AGPL. Nada de empaquetar `gs` en el
   instalador ni descargarlo automáticamente. Ver ADR-003.
4. **Código y comentarios en español.** Nombres, mensajes y pruebas incluidos.
5. **Toda regla del plan tiene una prueba** que la fija. Ver `docs/06-TRAZABILIDAD.md`.
6. **La UI usa Semi.Avalonia (ADR-007), no `FluentTheme`.** El azul de acento es el propio
   de Semi — no se puede inyectar el de marca desde fuera, no lo intentes de nuevo sin leer
   la nota del ADR. Los estados de las filas se colorean con clases semánticas
   (`EsExito`/`EsPeligro`/…) + tokens de Semi, nunca hex sueltos.

## Dónde está cada cosa

- Lógica y motor: `src/CompresorPdf.Core/Services/`
- Composición de dependencias: `src/CompresorPdf.App/App.axaml.cs` (a mano, sin contenedor de DI)
- Documentación: `docs/`, empezando por `docs/00-INDICE.md`
- Plan original del usuario: `PlanCompresorPDF.md` (histórico, no editar)

## Trampas

- `Progress<T>` despacha asíncronamente: en pruebas hay que esperar antes de contar eventos.
- `dotnet run --nologo -- ruta` pasa `--nologo` como `args[0]`; ejecuta el `.dll` directamente.
- Cancelar requiere `Kill(entireProcessTree: true)` o Ghostscript sobrevive.
- `CompresorPdf.App.csproj` tiene `InvariantGlobalization=false` (a propósito, contradice
  `Directory.Build.props`): Semi.Avalonia construye `CultureInfo` reales en su arranque y
  el modo invariante lo rompe con un `TypeInitializationException`. No lo actives aquí.
- Animar `RenderTransform` directamente falla en runtime ("No animator registered"). Anima
  `RotateTransform.Angle` (ver el ícono "Procesando" en `Styles/Controles.axaml`).
- Un ícono dibujado como línea perfectamente horizontal/vertical tiene bbox de altura/ancho
  cero y `Stretch="Uniform"` lo escala mal (casi invisible) — dale siempre una pendiente
  imperceptible (ver `IconoMinus` en `Styles/Iconos.axaml`).
