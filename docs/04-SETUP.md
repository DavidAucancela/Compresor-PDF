# Puesta en marcha

---

## 1. Instalar la aplicación (usuario final, sin tocar código)

El instalador ya lleva el runtime de .NET adentro. **No necesitas instalar .NET.**
Solo hace falta Ghostscript, que por razones de licencia no puede ir dentro del instalador
([ADR-003](02-DECISIONES-ADR.md#adr-003--licencia-ghostscript-no-se-distribuye-con-la-aplicación)).

### Windows — paso a paso

#### Paso 1 — Instalar Ghostscript

1. Ve a [ghostscript.com/releases](https://www.ghostscript.com/releases/gsdnld.html).
2. Descarga el instalador de **64 bits** (el archivo se llama algo como `gs10050w64.exe`).
3. Ejecútalo y acepta las opciones por defecto. El instalador se instala en
   `C:\Program Files\gs\gs10.xx.x\` y añade esa carpeta al PATH.

Para confirmar que quedó bien, abre **PowerShell** o **cmd** y escribe:

```powershell
gswin64c --version
```

Debería responder con el número de versión (p. ej. `10.05.0`). Si da error, ve a
[Solucionar "no encuentra Ghostscript"](#si-la-app-dice-que-no-encuentra-ghostscript) más abajo.

> En Windows el binario se llama `gswin64c.exe`, no `gs`. La app lo busca por ese nombre
> y también rastrea la carpeta `C:\Program Files\gs\` automáticamente, así que aunque no
> esté en el PATH normalmente lo encuentra igual.

#### Paso 2 — Instalar la aplicación

1. Descarga el archivo `CompresorPdf-x.x.x-x64.msi`.
2. Haz doble clic para ejecutarlo y sigue el asistente.

> **SmartScreen puede mostrar una advertencia** del tipo "Windows protegió tu PC". Esto
> ocurre porque el instalador no está firmado con un certificado de code signing de pago,
> no porque sea peligroso. Para continuar: pulsa **"Más información"** → **"Ejecutar de
> todas formas"**.

#### Paso 3 — Verificar

Abre la app. La barra de título muestra la ruta de Ghostscript que está usando. Si dice
"Ghostscript no encontrado", ve a la sección de solución de problemas más abajo.

---

### macOS — paso a paso

```bash
# Instalar Ghostscript
brew install ghostscript

# Si dotnet no aparece tras instalarlo con Homebrew, añade al PATH:
export PATH="/opt/homebrew/bin:$PATH"
export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"
```

1. Monta el `.dmg` y arrastra la app a la carpeta Aplicaciones.
2. Primera vez: **clic derecho › Abrir** — Gatekeeper bloquea apps no notarizadas. Solo
   hay que hacerlo una vez.

---

## 2. Requisitos para desarrollar (código fuente)

| | macOS | Windows |
|---|---|---|
| .NET SDK 10 | `brew install dotnet` | [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) |
| Ghostscript | `brew install ghostscript` | [ghostscript.com/releases](https://www.ghostscript.com/releases/gsdnld.html) |

Verifica que ambos responden:

```bash
# macOS / Linux
dotnet --version   # 10.x
gs --version       # 10.x

# Windows (PowerShell o cmd)
dotnet --version   # 10.x
gswin64c --version # 10.x
```

## Comandos de desarrollo

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

---

## Si la app dice que no encuentra Ghostscript

La barra de título siempre muestra qué motor está usando y desde dónde. Si avisa de
que no lo encuentra:

1. **Confirma la instalación.** En Windows abre PowerShell y escribe `gswin64c --version`.
   En macOS/Linux escribe `gs --version`.

2. **Revisa dónde busca la app.** `LocalizadorGhostscript` busca en este orden:
   - La ruta configurada manualmente por el usuario (campo `RutaGhostscript` en las preferencias).
   - El `PATH` del sistema.
   - Carpetas habituales:
     - **Windows:** `C:\Program Files\gs\` y todas sus subcarpetas `bin\`.
     - **macOS:** `/opt/homebrew/bin`, `/usr/local/bin`, `/usr/bin`, `/opt/local/bin`.

3. **Configura la ruta manualmente (último recurso).** Abre el archivo de preferencias
   (`%APPDATA%\CompresorPdf\preferencias.json` en Windows) y añade o edita:
   ```json
   {
     "RutaGhostscript": "C:\\Program Files\\gs\\gs10.05.0\\bin\\gswin64c.exe"
   }
   ```
   Ajusta la versión al número que tengas instalado.

---

## Dónde guarda sus cosas la aplicación

| Qué | Windows | macOS |
|---|---|---|
| Preferencias | `%APPDATA%\CompresorPdf\preferencias.json` | `~/Library/Application Support/CompresorPdf/preferencias.json` |
| Log | `%APPDATA%\CompresorPdf\compresor.log` | `~/Library/Application Support/CompresorPdf/compresor.log` |
| Resultados | `comprimidos\` junto a cada original (configurable) | ídem |
| Respaldos | `originales\` junto a cada original (opcional) | ídem |

`%APPDATA%` normalmente es `C:\Users\<tu usuario>\AppData\Roaming\`.

Borrar `preferencias.json` devuelve la app a sus valores por defecto; un archivo corrupto
también, sin fallar.

---

## Empaquetado

Todo vive en `build/` y se ejecuta con un comando.

### Windows — `.exe` + `.msi`

Desde una máquina Windows con PowerShell:

```powershell
# Una sola vez: instalar WiX 7 y sus dependencias
dotnet tool install -g wix
wix eula accept wix7
wix extension add WixToolset.UI.wixext

# Generar el instalador
.\build\publicar-windows.ps1             # x64 (por defecto)
.\build\publicar-windows.ps1 -Arquitectura arm64
```

El resultado queda en `artifacts\windows\CompresorPdf-x.x.x-x64.msi`.

Para firmar el instalador (requiere un certificado de code signing, evita el aviso de SmartScreen):

```powershell
$env:HUELLA_CERTIFICADO = "<huella SHA1 del certificado>"
.\build\publicar-windows.ps1
```

> Verificado en Windows 11 con WiX 7.0.0 y .NET 10.0.400.

### macOS — `.app` + `.dmg`

```bash
./build/publicar-macos.sh            # arm64 (por defecto)
./build/publicar-macos.sh x64
./build/publicar-macos.sh universal  # binario universal arm64 + x64
```

Deja el resultado en `artifacts/macos/`. Sin firmar, la app funciona en tu equipo; en otro
Gatekeeper la bloqueará y el usuario tendrá que hacer **clic derecho › Abrir** la primera vez.

Para firmar y notarizar (requiere cuenta Apple Developer, 99 $/año):

```bash
export IDENTIDAD_FIRMA="Developer ID Application: Tu Nombre (TEAMID)"
export PERFIL_NOTARIZACION="mi-perfil"   # xcrun notarytool store-credentials
./build/publicar-macos.sh arm64
```

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
