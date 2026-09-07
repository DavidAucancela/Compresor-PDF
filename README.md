# Compresor de PDFs

Aplicación de escritorio que comprime PDFs **sólo cuando superan un umbral configurable**
(2 MB por defecto). Multiplataforma (macOS y Windows), 100 % offline: ningún archivo sale del equipo.

## Estado

**Fases 1 y 2 completas** (salvo RF-11 vista previa). Verificado de extremo a extremo: un PDF
real con imágenes pasa de **2.23 MB a 246 KB (−89.2 %)** manteniendo la capa de texto
buscable. El rediseño visual (Fase 4, sub-fases 4.0–4.5) está implementado con Semi.Avalonia.
La carga de ~200 PDFs sin bloquear la UI (RF-19) y la unidad configurable del umbral KB/MB
(RF-25) ya están disponibles.

En total, **22 de los 33 requisitos** están cerrados. Ver [`docs/03-ROADMAP.md`](docs/03-ROADMAP.md) para el detalle.

---

## Instalar la aplicación (usuario final)

El instalador ya lleva el runtime de .NET adentro — **no necesitas instalar .NET**.
El único requisito externo es **Ghostscript** (el motor de compresión).

### Windows

1. Descarga e instala **Ghostscript** desde [ghostscript.com/releases](https://www.ghostscript.com/releases/gsdnld.html).
   Elige el instalador de 64 bits (`gs_10.x.x_x64.exe`).
2. Descarga el `.msi` de Compresor de PDFs y ejecútalo.
3. Abre la app — la barra superior confirma si Ghostscript se detectó correctamente.

> Si SmartScreen avisa al instalar el `.msi`, pulsa **"Más información" → "Ejecutar de todas formas"**.
> Ocurre porque el instalador no está firmado con un certificado de code signing de pago.

### macOS

1. `brew install ghostscript`
2. Monta el `.dmg` y arrastra la app a Aplicaciones.
3. Primera vez: **clic derecho › Abrir** (Gatekeeper bloquea apps no notarizadas).

---

## Cómo se usa

1. Arrastra PDFs (o carpetas enteras) a la ventana, o pulsa **Añadir archivos…**
2. Ajusta el umbral y el nivel de compresión si hace falta.
3. Pulsa **Comprimir**. Los resultados salen en `comprimidos/` junto a cada original.

El archivo original **nunca se modifica**.

---

## Para desarrolladores (código fuente)

Requisitos: **.NET 10 SDK** y **Ghostscript** instalados en la máquina de desarrollo.

```bash
# macOS
brew install dotnet ghostscript

# Windows — instala desde sus webs oficiales:
# .NET 10 SDK: https://dotnet.microsoft.com/download
# Ghostscript:  https://www.ghostscript.com/releases/gsdnld.html
```

```bash
dotnet run --project src/CompresorPdf.App   # ejecutar
dotnet test                                 # 43 pruebas
dotnet build -c Release
```

## Generar el instalable

```bash
./build/publicar-macos.sh            # → artifacts/macos/*.dmg
.\build\publicar-windows.ps1         # → artifacts/windows/*.msi (desde Windows)
```

Detalles, firma y notarización en [`docs/04-SETUP.md`](docs/04-SETUP.md).

## Estructura

```
src/CompresorPdf.Core    Lógica pura, sin UI. Es donde vive todo lo importante.
src/CompresorPdf.App     Interfaz Avalonia (MVVM).
tests/CompresorPdf.Tests 43 pruebas, incluidas 3 de integración con Ghostscript real.
build/                   Iconos y scripts de empaquetado (.dmg / .msi).
docs/                    Arquitectura, decisiones, roadmap y guía de desarrollo.
```

## Documentación

Empieza por [`docs/00-INDICE.md`](docs/00-INDICE.md).
