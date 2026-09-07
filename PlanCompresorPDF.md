# Sistema de Compresión de PDFs — Aplicación de Escritorio (.NET / C#)

**Documento de alcance, requisitos y plan de desarrollo**
Autor: David Aucancela · Versión: 0.1 (borrador para validación)

---

## 1. Visión general

Aplicación de escritorio **instalable** (no web) que permite cargar uno o varios archivos PDF y
reducir su tamaño aplicando compresión, **solo cuando el archivo supera un umbral configurable**
(por defecto 2 MB). Los archivos por debajo del umbral se omiten y se marcan como "no requiere compresión".

**Objetivo del MVP:** que un usuario pueda arrastrar PDFs a la app, comprimir automáticamente los que
superen el umbral y obtener los resultados en una carpeta de salida, con un reporte de tamaño antes/después.

---

## 2. Decisión de framework (⚠️ pendiente de confirmar)

| Opción | Plataformas | Madurez escritorio | Corre en tu Mac M5 | Recomendado si… |
|--------|-------------|--------------------|--------------------|------------------|
| **WPF** | Solo Windows | Muy alta | ❌ (necesitas Windows/VM) | Los usuarios finales son 100% Windows |
| **MAUI** | Windows + macOS | Media (escritorio) | ✅ (Mac Catalyst) | Quieres compilar en Mac y/o entregar multiplataforma |
| **Avalonia** *(alternativa)* | Windows/macOS/Linux | Alta (enfocado en escritorio) | ✅ | Quieres el mejor escritorio multiplataforma en .NET |

**Recomendación:** como desarrollas en Mac, evalúa **MAUI** o **Avalonia**. Reservar WPF solo si el
público objetivo es exclusivamente Windows (y en ese caso compilarás desde un entorno Windows).
El resto de este documento es **agnóstico al framework** salvo en la sección técnica.

---

## 3. Requisitos funcionales (RF)

### MVP (Fase 1)
- **RF-01** Cargar PDFs por *drag & drop* y por selector de archivos.
- **RF-02** Carga múltiple (varios archivos a la vez).
- **RF-03** Umbral de tamaño configurable (por defecto 2 MB). Si el PDF ≤ umbral → se omite.
- **RF-04** Compresión con un perfil balanceado por defecto.
- **RF-05** Carpeta de salida configurable (con subcarpeta `comprimidos/` por defecto).
- **RF-06** No sobrescribir el original sin confirmación (respaldo o copia).
- **RF-07** Reporte por archivo: tamaño original, tamaño final, % de reducción, estado (comprimido / omitido / error).
- **RF-08** Indicador de progreso durante el proceso.

### Fase 2 (uso diario)
- **RF-09** Procesamiento por lotes con cola y progreso individual + global.
- **RF-10** Niveles de compresión seleccionables (Bajo / Medio / Alto).
- **RF-11** Vista previa de la primera página antes/después.
- **RF-12** Manejo de casos especiales: PDF protegido con contraseña, PDF corrupto, PDF escaneado (solo imágenes).
- **RF-13** Cancelar un proceso en curso.

### Fase 3 (producto pulido)
- **RF-14** Perfiles de compresión guardables (ej. "Email", "Impresión").
- **RF-15** Carpeta vigilada (*watch folder*): comprime automáticamente lo que se deposite.
- **RF-16** Historial / log de archivos procesados.
- **RF-17** Integración con el menú contextual del sistema operativo (clic derecho → "Comprimir").
- **RF-18** Renombrado con patrón configurable.

---

## 4. Requisitos no funcionales (RNF)

- **RNF-01 Rendimiento:** la compresión debe correr en un hilo secundario (no bloquear la UI).
- **RNF-02 Robustez:** un archivo con error no debe detener el lote completo.
- **RNF-03 Portabilidad de datos:** nunca modificar el original sin respaldo explícito.
- **RNF-04 Instalable:** entregable como instalador nativo (.msi/.msix en Windows, .dmg/.pkg en macOS).
- **RNF-05 Sin conexión:** funcionamiento 100% offline (dato sensible: los PDFs no salen del equipo).
- **RNF-06 Licenciamiento:** el motor de compresión debe tener licencia compatible con el uso previsto
  (ver sección 6 — importante si se distribuye a clientes).
- **RNF-07 Configuración persistente:** guardar preferencias del usuario entre sesiones.
- **RNF-08 Logs:** registro de errores para diagnóstico.

---

## 5. Arquitectura propuesta

Separación en capas para que la lógica de compresión no dependa de la UI (facilita cambiar de
framework o añadir una CLI más adelante):

```
CompresorPdf.sln
│
├── CompresorPdf.Core          // Lógica pura, sin UI (reutilizable)
│   ├── Models/                // ArchivoPdf, ResultadoCompresion, PerfilCompresion
│   ├── Services/
│   │   ├── ICompresorPdf.cs   // Interfaz del motor
│   │   ├── CompresorGhostscript.cs
│   │   ├── AnalizadorPdf.cs   // detecta tamaño, si es escaneado, si está protegido
│   │   └── GestorArchivos.cs  // respaldo, salida, renombrado
│   └── Config/                // Umbral, perfiles, preferencias
│
├── CompresorPdf.App           // UI (WPF o MAUI/Avalonia)
│   ├── Views/
│   ├── ViewModels/            // Patrón MVVM
│   └── App.xaml
│
└── CompresorPdf.Tests         // Pruebas unitarias del Core
```

**Patrón:** MVVM (estándar en .NET para desktop). El `Core` no conoce la UI; la UI solo llama servicios.

---

## 6. Motor de compresión (decisión técnica clave)

| Motor | Cómo se usa | Licencia | Nota |
|-------|-------------|----------|------|
| **Ghostscript (CLI)** | Invocado como proceso externo | AGPL (comercial requiere licencia) | El más potente para reducir imágenes/DPI. Multiplataforma. |
| **PDFsharp / MigraDoc** | Librería .NET | MIT | Optimiza estructura; poca compresión de imágenes. |
| **iText7** | Librería .NET | AGPL / comercial | Muy capaz, pero licencia restrictiva para software cerrado. |
| **Docotic.Pdf** | Librería .NET | Comercial (pago) | API de compresión muy buena, sin fricción de licencia AGPL. |
| **PDFium (vía wrapper)** | Nativo | BSD | Bueno para render/preview, no tanto para compresión. |

**Estrategia recomendada para el MVP:**
Usar **Ghostscript como proceso externo** (funciona en Mac y Windows, control fino de calidad con los
perfiles `/screen`, `/ebook`, `/printer`, `/prepress`). Aísla el motor detrás de `ICompresorPdf` para
poder cambiarlo sin tocar la UI. **Verificar el licenciamiento antes de distribuir a clientes** (AGPL).

Los tres niveles del RF-10 mapean naturalmente a los perfiles de Ghostscript:
- **Alto** → `/screen` (72 dpi, máxima reducción)
- **Medio** → `/ebook` (150 dpi, balanceado) ← *por defecto*
- **Bajo** → `/printer` (300 dpi, calidad alta)

---

## 7. Flujo del MVP (paso a paso)

1. Usuario arrastra/selecciona PDFs → se listan en la app.
2. Por cada archivo: leer tamaño → comparar con umbral.
   - ≤ umbral → estado "Omitido".
   - > umbral → encolar para comprimir.
3. Comprimir en hilo secundario con el perfil por defecto.
4. Guardar en carpeta de salida (sin tocar el original).
5. Mostrar reporte: original → final, % reducido, estado.

---

## 8. Plan de desarrollo por fases

**Fase 1 — MVP (núcleo funcional)**
- Estructura de solución + `Core` con `ICompresorPdf` y `CompresorGhostscript`.
- Umbral configurable + regla de omisión.
- UI mínima: carga, botón comprimir, tabla de resultados, progreso.
- Empaquetado básico instalable.

**Fase 2 — Uso diario**
- Cola de lotes, niveles de compresión, vista previa, manejo de errores especiales, cancelación.

**Fase 3 — Producto pulido**
- Perfiles guardables, watch folder, historial, integración menú contextual, auto-actualización.

---

## 9. Riesgos / puntos de atención

- **Licencia de Ghostscript (AGPL)** si se distribuye comercialmente → definir a tiempo.
- **PDFs escaneados** vs con texto: la compresión agresiva puede degradar legibilidad → detectar el tipo.
- **WPF vs Mac:** si se elige WPF, necesitarás un entorno Windows para compilar/probar.
- **Preservar capa de texto/OCR:** confirmar si es requisito que el PDF siga siendo buscable tras comprimir.

---

## 10. Decisiones pendientes (necesarias antes de construir)

1. **Framework definitivo:** ¿WPF (solo Windows), MAUI o Avalonia (multiplataforma, corre en tu Mac)?
2. **Plataforma(s) objetivo de los usuarios finales:** ¿Windows, macOS o ambos?
3. **Uso previsto:** ¿herramienta personal o se distribuirá a clientes? (define licencia del motor).
4. **Requisito de calidad:** ¿debe preservarse la capa de texto/búsqueda tras comprimir?
5. **Comportamiento con el original:** ¿copia en carpeta nueva, respaldo, o sobrescribir con confirmación?
