# Compresor de PDFs

Aplicación de escritorio que comprime PDFs **sólo cuando superan un umbral configurable**
(2 MB por defecto). Multiplataforma (macOS y Windows), 100 % offline: ningún archivo sale del equipo.

## Estado

**Fase 1 (MVP) completa** — 15 de 15 requisitos. Verificado de extremo a extremo: un PDF
real con imágenes pasa de **2.23 MB a 246 KB (−89.2 %)** manteniendo la capa de texto
buscable. La aplicación empaquetada (`.app` / `.dmg`) arranca desde Finder y localiza
Ghostscript sin depender del `PATH` del shell.

En total, **20 de los 26 requisitos** del plan están cerrados.

## Puesta en marcha (60 segundos)

```bash
# 1. Requisitos: .NET 10 SDK y Ghostscript
brew install dotnet ghostscript        # macOS
# Windows: instalar .NET 10 SDK y Ghostscript desde sus webs

# 2. Ejecutar
dotnet run --project src/CompresorPdf.App

# 3. Pruebas
dotnet test
```

## Cómo se usa

1. Arrastra PDFs (o carpetas enteras) a la ventana, o pulsa **Añadir archivos…**
2. Ajusta el umbral y el nivel de compresión si hace falta.
3. Pulsa **Comprimir**. Los resultados salen en `comprimidos/` junto a cada original.

El archivo original **nunca se modifica**.

## Estructura

```
src/CompresorPdf.Core   Lógica pura, sin UI. Es donde vive todo lo importante.
src/CompresorPdf.App    Interfaz Avalonia (MVVM).
tests/CompresorPdf.Tests 43 pruebas, incluidas 3 de integración con Ghostscript real.
build/                  Iconos y scripts de empaquetado (.dmg / .msi).
docs/                   Arquitectura, decisiones, roadmap y guía de desarrollo.
```

## Generar el instalable

```bash
./build/publicar-macos.sh          # → artifacts/macos/*.dmg
.\build\publicar-windows.ps1       # → artifacts/windows/*.msi (desde Windows)
```

Detalles y firma en [`docs/04-SETUP.md`](docs/04-SETUP.md#empaquetado).

## Documentación

Empieza por [`docs/00-INDICE.md`](docs/00-INDICE.md).
