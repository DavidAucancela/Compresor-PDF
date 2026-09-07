# Trazabilidad de requisitos

Cada requisito del plan original, dónde está implementado y qué prueba lo fija.

## Requisitos funcionales

| RF | Descripción | Estado | Implementación | Prueba |
|---|---|---|---|---|
| RF-01 | Drag & drop y selector | ✅ | `MainWindow.AlSoltar`, `.AlAnadirArchivos` | `Filtrar_expande_carpetas_recursivamente` |
| RF-02 | Carga múltiple | ✅ | `MainWindowViewModel.AgregarRutasAsync` | `Filtrar_descarta_no_pdfs_y_duplicados` |
| RF-03 | Umbral configurable (2 MB) | ✅ | `ServicioCompresionLote.ProcesarUnoAsync` | `Un_pdf_por_debajo_del_umbral_se_omite_sin_llamar_al_motor`, `El_umbral_por_defecto_son_2_MB` |
| RF-04 | Perfil balanceado por defecto | ✅ | `PerfilCompresion.Balanceado` | `Cada_nivel_mapea_a_su_PDFSETTINGS` |
| RF-05 | Carpeta de salida configurable | ✅ | `GestorArchivos.ResolverRutaSalida` | `Salida_por_defecto_va_a_subcarpeta_comprimidos` |
| RF-06 | No sobrescribir el original | ✅ | `GestorArchivos` (ADR-004) | `Nunca_devuelve_la_ruta_del_original`, `No_sobrescribe_un_archivo_de_salida_existente` |
| RF-07 | Reporte por archivo | ✅ | `ResultadoCompresion`, `ResumenLote` | `El_resumen_agrega_solo_lo_realmente_comprimido` |
| RF-08 | Indicador de progreso | ✅ | `ProgresoLote` + `IProgress` | `El_progreso_se_reporta_una_vez_por_archivo` |
| RF-09 | Lotes con cola y progreso | ✅ | `Parallel.ForAsync` en el orquestador | `El_lote_completo_funciona_contra_el_motor_real` |
| RF-10 | Niveles Bajo/Medio/Alto | ✅ | `CompresorGhostscript.PdfSettingsDe` | `Cada_nivel_mapea_a_su_PDFSETTINGS` |
| RF-11 | Vista previa antes/después | ⬜ | — | — |
| RF-12 | Protegido / corrupto / escaneado | ✅ | `AnalizadorPdf` | `Pdf_con_diccionario_Encrypt_se_marca_protegido`, `Archivo_sin_cabecera_pdf_se_marca_corrupto`, `Pdf_con_imagenes_y_sin_fuentes_parece_escaneado` |
| RF-13 | Cancelar en curso | ✅ | `CancellationToken` + `Kill(entireProcessTree)` | `Cancelar_deja_los_pendientes_marcados_como_Cancelado` |
| RF-14 | Perfiles guardables | 🟡 | `PerfilCompresion` existe; falta persistir lista y UI | — |
| RF-15 | Carpeta vigilada | ⬜ | — | — |
| RF-16 | Historial | ⬜ | Base parcial en `RegistroArchivo` | — |
| RF-17 | Menú contextual del SO | ⬜ | — | — |
| RF-18 | Renombrado con patrón | 🟡 | `SufijoSalida`; falta patrón con variables | — |
| RF-19 | Cargar ~200 PDFs sin bloquear la UI durante el análisis | ✅ | `ServicioCompresionLote.PrepararAsync`, `MainWindowViewModel.AgregarRutasAsync` | `PrepararAsync_analiza_lo_mismo_que_Preparar`, `PrepararAsync_reporta_progreso_una_vez_por_archivo`, `PrepararAsync_respeta_la_cancelacion` |
| RF-20 | Información por archivo a esta escala | ✅ | Igual que RF-07/Fase 4, ya soportaba el volumen | — |
| RF-21 | Totales agregados en vivo | ⬜ | — | — |
| RF-22 | Selección de archivos | ⬜ | — | — |
| RF-23 | Quitar archivos sin vaciar el lote | ⬜ | — | — |
| RF-24 | Botón "Abrir carpeta de resultados" | ⬜ | — | — |
| RF-24b | Carpeta de salida consolidada (orígenes mixtos) | ⬜ | — | — |
| RF-25 | Unidad del umbral configurable (KB/MB) | ✅ | `MainWindowViewModel.UmbralValor`/`UnidadUmbral`, `PreferenciasUsuario.UnidadUmbral` | `La_unidad_del_umbral_es_MB_por_defecto`, `Las_preferencias_sobreviven_a_un_ciclo_guardar_cargar` |

## Requisitos no funcionales

| RNF | Descripción | Estado | Cómo se cumple | Prueba |
|---|---|---|---|---|
| RNF-01 | No bloquear la UI | ✅ | Todo el lote es `async` sobre `Parallel.ForAsync` | Implícito en las pruebas async |
| RNF-02 | Un error no detiene el lote | ✅ | Excepciones convertidas en `ResultadoCompresion` | `Un_archivo_corrupto_no_detiene_al_resto_del_lote`, `Un_fallo_del_motor_se_reporta_como_Error_y_el_lote_continua` |
| RNF-03 | Nunca modificar el original | ✅ | ADR-004, reforzado en varios puntos | `El_archivo_original_nunca_se_modifica`, `El_motor_real_produce_un_pdf_valido_y_no_toca_el_original` |
| RNF-04 | Instalable | ✅ | `build/publicar-macos.sh`, `build/publicar-windows.ps1`, `build/instalador.wxs` | Verificado a mano: el `.app` arranca desde Finder con `PATH` mínimo |
| RNF-05 | 100 % offline | ✅ | Sin dependencias de red en ningún proyecto | Auditable por inspección |
| RNF-06 | Licencia del motor | ✅ | ADR-003: Ghostscript no se distribuye | Decisión documentada |
| RNF-07 | Preferencias persistentes | ✅ | `RepositorioPreferenciasJson` | `Las_preferencias_sobreviven_a_un_ciclo_guardar_cargar`, `Un_json_corrupto_no_rompe_el_arranque` |
| RNF-08 | Logs | ✅ | `RegistroArchivo` con escritura serializada | — |

## Resumen

**43 pruebas, todas en verde** (40 unitarias + 3 de integración contra Ghostscript real).

- **Fase 1 (MVP): completa.** 15 de 15.
- Fase 2: completa salvo RF-11 (vista previa). 4 de 5.
- Fase 3: dos requisitos con base parcial, el resto sin empezar.
- Fase 5 (lotes grandes): RF-19 y RF-25 hechos; RF-20 heredado de fases anteriores; RF-21 a
  RF-24b pendientes de construir.
- **Total: 22 de 33 requisitos cerrados**, 2 parciales (RF-14, RF-18), 9 pendientes
  (RF-11, RF-15, RF-16, RF-17, RF-21, RF-22, RF-23, RF-24, RF-24b).

Salvedad sobre RNF-04: el MSI de Windows está definido pero nunca se ha construido ni
instalado en una máquina Windows real.
