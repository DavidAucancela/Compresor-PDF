# Puesta en marcha

## Requisitos

| | macOS | Windows |
|---|---|---|
| .NET SDK 10 | `brew install dotnet` | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| Ghostscript | `brew install ghostscript` | [ghostscript.com/releases](https://www.ghostscript.com/releases/gsdnld.html) |

Verifica que ambos responden:

```bash
dotnet --version   # 10.x
gs --version       # 10.x
```

En macOS, si `dotnet` no aparece tras instalarlo con Homebrew:

```bash
export PATH="/opt/homebrew/bin:$PATH"
export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"
```

## Comandos

```bash
# Ejecutar la aplicación
dotnet run --project src/CompresorPdf.App

# Todas las pruebas (las de integración se saltan solas si no hay Ghostscript)
dotnet test

# Sólo la lógica, sin tocar disco de más
dotnet test --filter "FullyQualifiedName!~Integracion"

# Compilar todo en Release
dotnet build -c Release
```

## Si la app dice que no encuentra Ghostscript

La cabecera de la ventana muestra siempre qué motor está usando y desde dónde. Si avisa de
que no lo encuentra:

1. Confirma que `gs --version` funciona en tu terminal.
2. `LocalizadorGhostscript` busca, en este orden: la ruta configurada por el usuario, el
   `PATH`, y las carpetas habituales (`/opt/homebrew/bin`, `/usr/local/bin`, `/usr/bin`,
   `/opt/local/bin` en macOS; `C:\Program Files\gs\*\bin` en Windows).
3. Como último recurso, fija la ruta manualmente en el archivo de preferencias, campo
   `RutaGhostscript`.

## Dónde guarda sus cosas la aplicación

| Qué | macOS | Windows |
|---|---|---|
| Preferencias | `~/Library/Application Support/CompresorPdf/preferencias.json` | `%APPDATA%\CompresorPdf\preferencias.json` |
| Log | `~/Library/Application Support/CompresorPdf/compresor.log` | `%APPDATA%\CompresorPdf\compresor.log` |
| Resultados | `comprimidos/` junto a cada original (configurable) | ídem |
| Respaldos | `originales/` junto a cada original (opcional) | ídem |

Borrar `preferencias.json` devuelve la app a sus valores por defecto; un archivo corrupto
también, sin fallar.

## Empaquetado

Todo vive en `build/` y se ejecuta con un comando.

### macOS — `.app` + `.dmg`

```bash
./build/publicar-macos.sh            # arm64 (por defecto)
./build/publicar-macos.sh x64
./build/publicar-macos.sh universal  # binario universal arm64 + x64
```

Deja el resultado en `artifacts/macos/`. Sin firmar, la app funciona en tu equipo; en otro
Gatekeeper la bloqueará y el usuario tendrá que hacer **clic derecho › Abrir** la primera vez.

Para firmar y notarizar (requiere cuenta Apple Developer):

```bash
export IDENTIDAD_FIRMA="Developer ID Application: Tu Nombre (TEAMID)"
export PERFIL_NOTARIZACION="mi-perfil"   # xcrun notarytool store-credentials
./build/publicar-macos.sh arm64
```

Los *entitlements* de JIT ya están incluidos: sin ellos una app .NET firmada con
*hardened runtime* no arranca.

### Windows — `.exe` + `.msi`

```powershell
dotnet tool install --global wix     # una sola vez
.uild\publicar-windows.ps1         # x64
.uild\publicar-windows.ps1 -Arquitectura arm64
```

Para firmar: `$env:HUELLA_CERTIFICADO = "<huella del certificado>"` antes de ejecutarlo.

> El `.exe` compila correctamente desde macOS, pero **el MSI nunca se ha construido en una
> máquina Windows real**. Espera tener que ajustar algo la primera vez.

### Iconos

`build/icono.ps` es el original vectorial. Para regenerar `.icns` y `.ico`:

```bash
./build/icono.sh
```

### Lo que el instalador NO puede hacer

**Ni el `.dmg` ni el `.msi` pueden incluir Ghostscript**, ni descargarlo automáticamente
durante la instalación. Es AGPL: distribuirlo obligaría a publicar esta aplicación bajo
AGPL o a comprar una licencia comercial. Ver
[ADR-003](02-DECISIONES-ADR.md#adr-003--licencia-ghostscript-no-se-distribuye-con-la-aplicación).
