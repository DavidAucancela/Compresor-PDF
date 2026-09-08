# Documentación — Compresor de PDFs

Punto de entrada a los documentos de guía del proyecto. El orden sugerido de lectura
es el numérico.

| Documento | Para qué sirve | Léelo si… |
|---|---|---|
| [01-ARQUITECTURA.md](01-ARQUITECTURA.md) | Cómo está partido el sistema y por qué | vas a tocar código |
| [02-DECISIONES-ADR.md](02-DECISIONES-ADR.md) | Las decisiones cerradas y sus motivos | te preguntas "¿por qué así?" |
| [03-ROADMAP.md](03-ROADMAP.md) | Qué está hecho y qué sigue, por fases | planificas la próxima tarea |
| [04-SETUP.md](04-SETUP.md) | Entorno, ejecución, empaquetado | acabas de clonar el repo |
| [05-GUIA-DESARROLLO.md](05-GUIA-DESARROLLO.md) | Convenciones y recetas para añadir cosas | vas a escribir código nuevo |
| [06-TRAZABILIDAD.md](06-TRAZABILIDAD.md) | Cada RF/RNF → dónde está implementado y probado | verificas el alcance |
| [07-PLAN-DISENO.md](07-PLAN-DISENO.md) | Rediseño visual completo (Fase 4): sistema de diseño, componentes, fases | vas a trabajar en la UI |
| [08-PLAN-LOTES-GRANDES.md](08-PLAN-LOTES-GRANDES.md) | Fase 5: selección, totales agregados y exportación para lotes de ~200 PDFs | vas a trabajar en escala/rendimiento |
| [09-PLAN-CONFIG-VISIBLE-Y-PULIDO.md](09-PLAN-CONFIG-VISIBLE-Y-PULIDO.md) | Fase 6: configuración en panel lateral visible, opciones de compresión no cableadas, QA previo al despliegue | vas a preparar el primer despliegue |

## Contexto

El documento de partida es [`../PlanCompresorPDF.md`](../PlanCompresorPDF.md), que dejaba
cinco decisiones abiertas. **Todas están resueltas** en
[02-DECISIONES-ADR.md](02-DECISIONES-ADR.md); ese es el documento vivo. El plan original
se conserva sin cambios como referencia histórica del alcance.

## Resumen en una pantalla

- **Qué es:** app de escritorio que comprime PDFs por lotes, sólo los que superan un umbral.
- **Framework:** Avalonia 11 sobre .NET 10 (macOS + Windows desde una sola base de código).
- **Motor:** Ghostscript invocado como proceso externo, detrás de la interfaz `ICompresorPdf`.
- **Regla inviolable:** el archivo original nunca se modifica.
- **Estado:** Fase 1 y Fase 2 (salvo RF-11 vista previa) completas. Fase 4 (rediseño visual):
  sub-fases 4.0–4.5 implementadas y verificadas con datos reales; 4.6–4.7 parciales. Fase 5
  (lotes grandes): completa — RF-19, RF-21, RF-22, RF-23, RF-24, RF-24b y RF-25 implementados.
  MSI de Windows verificado en máquina real.
