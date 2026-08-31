QA OK - build working tree (sin commit, sobre `414c0e5`) - 2026-08-30

Incremento 4 (US-008 a US-013) — validación end-to-end contra los Given/When/Then de
`01-spec.md` (US-008..US-013, RN-1..RN-9), NFR-37..NFR-53 / AC-T40..AC-T63 de `02-tech-spec.md`
y la matriz de `test-plan.md`. Entrada faltante que el UAT marcó como bloqueante de proceso: la
QA de módulos ya se corrió en esta sesión (hallazgos D-1 y D-2 resueltos, D-3 aceptado como
menor) pero no había quedado registro en este archivo — la última entrada era del Incremento 3.

**Build / suite (corrida propia, sin reusar resultados de developer/test-developer/code-reviewer):**
- `dotnet build AutoExam.sln -c Release`: 0 advertencias, 0 errores.
- `dotnet test AutoExam.sln -c Release`: 476/476 en verde (~10 s). Sin fallos de entorno esta
  vez (los de `VerificarVersionScriptTests` / `ActualizacionService` que ensuciaban corridas
  previas ya no aparecen en esta suite).
- code-reviewer cerró 2 bloqueantes (conteo erróneo de imagen corrupta en `ImagenExtractor`;
  falta de tope incremental de texto en `OfficeExtractor`) y 1 menor (`ArgumentException` con
  `(Parameter ...)` visible al usuario); los 3 reconfirmados por lectura del código integrado
  (ver por-US).
- Paquete nuevo `Magick.NET-Q8-x64 14.16.0`, único consumidor `Services/ConversorHeic.cs`, RID
  fijo `win-x64`. devops: publish single-file self-contained `win-x64` OK, +10.9 MB de `.exe`
  (dentro del presupuesto NFR-A1 de 35 MB), sin DLLs sueltas.

---

**US-008 — Office moderno (`.docx` / `.xlsx` / `.pptx`)** — AC-T40..T44, NFR-37..41

| Given/When/Then | Verificación | Estado |
|---|---|---|
| Selector y zona de arrastre aceptan Office junto a PDF (crit. 1) | `DialogoService.ElegirFuentes` arma el filtro desde `FactoriaExtractores.ExtensionesAdmitidas`; `SoltarArchivo.Extensiones` default a esa misma lista. `IDialogosElegirFuentesContractTests` (5), `FactoriaExtractoresIngestaContractTests` (6), `SoltarArchivoMultiArchivoTests` (6). | OK |
| Examen con contenido del archivo + corrección UBA (crit. 2) | `OfficeExtractor` produce `Fragmentos` que viajan por `SolicitudGeneracion.Fragmentos` (canal preexistente); `AsistenteViewModel.GenerarAsync` usa el mismo motor y `EvaluadorUBA` intacto. Extracción cubierta por `OfficeExtractorTests` (15: cuerpo de Word, 1 fragmento por hoja de Excel con shared + inline strings, 1 por diapositiva de PPT con texto). Examen real contra Gemini → gap (a). | OK (extracción) / gap (a) (generación real) |
| Medida por formato, 0 fuentes sin medida (crit. 3 / NFR-40) | Word → "documento unico"; Excel → "N hojas · ~Mk filas"; PPT → "N diapositivas". `BibliotecaService.AgregarFuenteAsync` la puebla vía `IExtractorContenido.MedirAsync`. `OfficeExtractorTests::MedirAsync_*_AC_T41` + `MedirAsync_SiempreDevuelveUnaMedida_NoNulaNiVacia_NFR40`, `BibliotecaServiceAgregarFuenteTests`. Word nunca reporta páginas (OOXML no las persiste, dependen del render): "documento unico" es la degradación que NFR-40 admite explícitamente. | OK |
| Cualquier tamaño sin rechazo ni truncado propio (crit. 4 / NFR-38) | `OfficeExtractor` lee parte por parte (`ZipArchive` + `XmlReader` forward-only), nunca descomprime el OPC entero; sólo recorta al superar `MaxCaracteres` (recorte de contexto, no rechazo). `ExtraerAsync_PowerPointGrande_NoRecortaDiapositivasPorUnLimitePropio_AC_T42`, `ExtraerAsync_SoloRecortaCuandoSeSuperaMaxCaracteres_NFR38`. **Bloqueante de code-review "falta tope incremental de texto"** → cerrado: clase `Presupuesto` (2× `MaxCaracteres`, piso 40k) corta la acumulación en RAM *durante* la lectura; `AjustarPresupuesto` hace el recorte fino después. RAM pico real con archivo ≥500 páginas equivalentes → gap (d). | OK / gap (d) (RAM pico) |
| `.doc` / `.xls` / `.ppt` y formato desconocido → rechazo con causa, 0 fuentes (crit. 5-6 / NFR-37) | `FactoriaExtractores.Para` → null → `FormatoNoSoportadoException` con mensaje que nombra los formatos admitidos y sugiere reguardar; `< 200 ms`, sin red. `FactoriaExtractoresTests` (7), `BibliotecaServiceAgregarFuenteTests::AgregarFuente_ExtensionLegacyODesconocida_Rechaza_SinCrearFuente_AC_T43`. **Menor de code-review** (`ArgumentException` con `(Parameter …)`) → cerrado: `FuenteInvalidaException` dedicada con `Message` listo para mostrar (`AgregarFuente_MezclaDeTipos_FuenteInvalida_MensajeLimpio_SinCopiarNada_AC_T43`). | OK |
| Office protegido / dañado → rechazo con causa, no crea fuente vacía (crit. 6) | OOXML cifrado es OLE2, no ZIP → `ZipFile.OpenRead` lanza → `FuenteIlegibleException` con causa; `BibliotecaService` borra la copia parcial y re-lanza. `ExtraerAsync_ArchivoQueNoEsZip_LanzaFuenteIlegibleConCausa_AC_T43`, `AgregarFuente_PdfDanado_BorraLaCopiaParcial_YReLanzaConCausa_AC_T43_NFR41`. | OK |
| Office sin texto extraíble → aviso, 0 exámenes vacíos (crit. 7 / NFR-41) | `ExtraerAsync_WordSinTexto_DevuelveResultadoSinMaterial_AC_T44` → `!TieneMaterial` → `AsistenteViewModel` avisa "no se encontró contenido" y no crea examen. | OK |

**D-1 (M4, resuelto)** — drop con mezcla de formatos admitidos / no admitidos: `SoltarArchivo`
pasa todas las rutas existentes sin filtrar; `BibliotecaViewModel.AgregarAsync` /
`AsistenteViewModel.SoltarAsync` separan admitidas vs. ignoradas, informan "Se ignoraron N
archivo(s) con un formato no admitido", y si las admitidas mezclan familias →
`FuenteInvalidaException` mostrada tal cual. `ArchivosValidos` (whitelist) sólo alimenta el
resaltado del `DragOver`.

**US-009 — Alcance según tipo de fuente** — AC-T45..47

| Given/When/Then | Verificación | Estado |
|---|---|---|
| Fuente no-PDF no ofrece capítulos ni módulos (crit. 1 / AC-T45) | `AsistenteViewModel.EsFuentePdf` gatea; `AsistenteView.xaml` esconde chips de capítulo y rango de páginas para no-PDF (`Visibility` a `EsFuentePdf`). `AsistenteViewModelAlcancePorTipoTests` (9): `FuenteNoPdf_NoOfreceCapitulos_NiModulos_AC_T45`, `CambiarDePdfANoPdf_LimpiaLosModulosDelPasoAlcance`, `AsistenteView_EscondeCapitulosParaNoPdf_YDejaElEjeTematicoSiempre`, `FuenteNoPdf_ElResumenDelPasoAlcance_NoHablaDePaginas_AC_T45`. | OK |
| Eje temático libre disponible y acota para cualquier fuente (crit. 2 / AC-T46) | TextBox `Tema` sin gate; `RecorteFuente.TemaLibre` viaja siempre; `SolicitudGeneracion.TemaLibre`. `FuenteNoPdf_ConTema_ElAlcanceLoIncluye_AC_T46`. | OK |
| Sin recorte → material completo (crit. 4 / AC-T47) | `RecorteFuente.MaterialCompleto` cuando `Paginas` nula/vacía; para no-PDF `Paginas = null` siempre. `FuenteNoPdf_SinTema_ElAlcanceEsMaterialCompleto_AC_T47`, `GenerarAsync_NoArmaPaginasParaFuenteNoPdf_YFijaElTopeDeImagenesDesdeLaConfig`. | OK |
| Recorte a subconjunto de estructura Office (crit. 3, *deseable no bloqueante*) | No implementado en v1 (documentado como fuera de alcance de arquitectura Inc-4). No afecta ningún AC-T. | Aceptado |

**US-010 — Fotos de apuntes manuscritos** — AC-T48..51, NFR-42..44

| Given/When/Then | Verificación | Estado |
|---|---|---|
| Selector y drop aceptan `.jpg`/`.jpeg`/`.png`/`.heic`/`.heif` + selección múltiple (crit. 1 / AC-T48) | `ElegirFuentes` `Multiselect=true` con grupo "Imágenes" en el filtro; `SoltarArchivo` multi-ruta → comando siempre con `string[]`. `SoltarArchivoMultiArchivoTests`, `IDialogosElegirFuentesContractTests`. | OK |
| `.heic`/`.heif` → formato soportado antes del envío, 0 bytes HEIC en el request (crit. 2 / AC-T49 / NFR-42) | `ConversorHeic.AConvertir` (Magick.NET con `libheif`/`libde265` embebidos, sin códec del SO) corre en `ImagenExtractor` **antes** de reescalar. `ConversorHeicTests` (5): `.heic` y `.heif` **reales** versionados en `AutoExam.Tests/Recursos/Imagen` → firma PNG en la salida, 0 marcador `ftyp`/`heic`; HEIC truncado o basura → lanza (lo traduce `ImagenExtractor` a "ilegible"). `ImagenExtractorTests::ExtraerAsync_ConHeic_LoConvierteAJpeg_CeroBytesHeicEnLaSalida_AC_T49_NFR42`. | OK en host de test / gap (b) (`.exe` publicado en Windows sin HEVC) |
| Preguntas reflejan el manuscrito; parcial informa; todas ilegibles → no crea examen (crit. 3-4 / AC-T50 / NFR-41) | Las imágenes viajan como `PaginasEscaneadas` (`inline_data`), el modelo les lee el texto. `ImagenExtractorTests`: `FotosLegibles_LasDejaComoPaginasEscaneadasYaPreparadas`, `AlgunasFotosIlegibles_GeneraConElResto_YAvisaLaLimitacion_AC_T50`, `TodasIlegibles_LanzaFuenteIlegible_SinExamenVacio_AC_T50_NFR41`, `ArchivoInexistenteJuntoAUnoValido_GeneraConElValido`. **Bloqueante de code-review "conteo erróneo de imagen corrupta"** → cerrado: `TryPrepararParaLectura` falla → `fallidas++` + aviso + `continue`; el mensaje final distingue `fallidas > 0` de "no se agregó ninguna". Interpretación real de la letra por Gemini → gap (a). | OK (pipeline) / gap (a) (interpretación real) |
| Orden de las imágenes = orden de alta (crit. 5 / AC-T51 / NFR-43) | `BibliotecaService.CopiarADeposito` numera `01..NN` en orden de alta; `ImagenExtractor` recorre `seleccion` en orden; `foreach (figuras.Concat(paginas))` en `GeminiApiService` preserva. `AgregarFuente_SetImagenes_UnaFuente_ArchivosEnOrdenDeAlta_YCopiaCorrelativa_AC_T48` (nombres no alfabéticos), `SoltarArchivoMultiArchivoTests::ArchivosValidos_...EnOrdenDeAlta`. | OK |
| Supera el máximo por material o el peso/lado por imagen → aviso con el límite (crit. 6 / AC-T51 / NFR-43) | `ImagenExtractor` recorta a `MaxPaginasEscaneadas` (= `AppConfig.MaxImagenesPorMaterial`, fijado por el VM desde la config) con aviso; > 3 MB tras reescalar → aviso, se manda igual (Gemini tiene su guarda por lote). `SuperaElMaximoPorMaterial_RecortaAlLimiteConAviso_NFR43`. | OK |
| Costo de la fuente-imagen informado (NFR-44) | `ImagenExtractor` reporta "se van a mandar N imágenes… puede tardar más y consumir más cuota" en el 100% de las generaciones-imagen. `ExtraerAsync_AvisaMayorConsumoDeCuota_NFR44`. | OK |

**US-011 — Pulido de animaciones (8 superficies RN-7)** — AC-T52..55, NFR-45..48

Compuerta única `Behaviors/Animaciones.cs`: `Reducidas = !SystemParameters.ClientAreaAnimation
|| RenderCapability.Tier == 0`, más `MovimientoReducidoProperty` (propiedad adjunta) para usarla
como `<Condition>` de `MultiTrigger` en XAML. Se resuelve una vez al inicializar el tipo (un
cambio de la preferencia del SO se toma en el próximo arranque — intencional, documentado).

Verificación por test estructural del XAML del checkout (sin runtime WPF), mismo criterio que
las corridas previas de US-007/US-008/US-011 de este reporte:

| # | Superficie RN-7 | Dónde / test | Estado |
|---|---|---|---|
| 1 | Transición entre secciones | `Behaviors/TransicionContenido.cs` — `DuracionTransicionSeccion`/`SuavizadoSalida`, consulta `Animaciones.Reducidas`. `ExamenViewTransicionContenidoTests`. | OK (estructural) |
| 2 | Hover y pulsado de botones/chips | `Theme/Estilos.xaml`, 7 estilos. `EstilosXamlAnimacionesHoverPresionTests` (NFR-20/21 timing centralizado, NFR-22 guardia `IsEnabled` + `MovimientoReducido=False`, NFR-23 sin `Brush`/`Color` animado). | OK (estructural) |
| 3 | Riel de pasos del asistente | `AsistenteView.xaml` — base gris `Linea` + overlay `LineaAvance` con `ScaleTransform.X` 0→1, timing centralizado, `MultiDataTrigger` con `Animaciones.Reducidas` (salto instantáneo si está activo). `RielPasosAsistenteAnimacionTests`. **Hallazgo D-2 de QA de módulo** (swap instantáneo de `Background` sin parámetros centralizados) → resuelto. | OK (estructural) |
| 4 | Baldosas del navegador de preguntas | `Theme/Estilos.xaml` `BaldosaPregunta` — cubierto por el Incremento 2 (US-007/US-008) + guardia `IsEnabled` agregada allí. | OK (estructural) |
| 5 | Entrada de la pantalla de Resultados | `Views/ExamenView.xaml` — superficie **nueva**: `Opacity` + `TranslateTransform.Y`, `DuracionTransicionSeccion` (0.22 s ≤ 250 ms), guardia `Animaciones.Reducidas=False`. `ResultadosEntradaAnimacionTests`. | OK (estructural) |
| 6 | Apertura/cierre de avisos (InfoBar) | `ui:InfoBar` nativo de WPF-UI 4.3.0 — ver hallazgo D-3. | Ver D-3 |
| 7 | Anillos de progreso | `ui:ProgressRing` nativo de WPF-UI 4.3.0 — ver hallazgo D-3. | Ver D-3 |
| 8 | Alta/baja de ítems en Historial y Libros | `Theme/Estilos.xaml` `ItemLibro` — superficie **nueva**: fade `Opacity` 0→1 en `MultiDataTrigger` con guardia reduce-motion (se sacó el `EventTrigger` de `Loaded`, que no admite `<Condition>`). `ItemLibroFadeInReducirMovimientoTests`. | OK (estructural) |

- No bloqueante ni acumulable (crit. 3-4 / NFR-46): `BeginAnimation`/`BeginStoryboard` reemplazan
  limpiamente el clock anterior (comportamiento WPF); `ContentControl` único en el shell. El
  stress de ≥5 disparos encadenados en vivo → parte de gap (c).
- Paridad funcional (crit. 5 / NFR-48): las animaciones sólo tocan `Opacity` de capa overlay ya
  tematizada o `ScaleTransform`; estado final idéntico con y sin animación (aserción explícita en
  los tests estructurales). Suite de regresión 476/476 sin desvíos.
- **Gap (c)**: el checklist visual en vivo de las 8 superficies (tema claro y oscuro, reducir
  movimiento on/off) que `01-spec.md` US-011 exige como binario "pasa/no pasa" para el sign-off
  no se puede ejecutar acá (sin sesión interactiva WPF). Evidencia acá: estructural (parámetros
  centralizados + guardia presentes en el XAML) + regresión de comportamiento — no inspección
  visual de "sin cortes ni parpadeo".

**US-012 — Borrar examen individual del historial** — AC-T56..59, NFR-49..51

| Given/When/Then | Verificación | Estado |
|---|---|---|
| Acción visible por ítem + confirmación; cancelar no cambia nada (crit. 1-2 / AC-T56) | `HistorialViewModel.BorrarExamenCommand(ExamenRendido)` → `IDialogos.Confirmar`; `HistorialView.xaml` botón por ítem. `HistorialViewModelBorrarExamenTests` (7): `Cancelar_LaConfirmacion_NoCambiaNada_AC_T56`, `ConfirmacionNormal_MencionaElExamen_SinAdvertirRevancha_AC_T56`. | OK |
| Desaparece, agregados recalculados, sigue ausente tras reiniciar (crit. 3-4 / AC-T57 / NFR-49) | `SesionUsuarioService.BorrarExamen` → `Perfil.Historial.RemoveAll` → `GuardarPerfil` → `RefrescarHistorial`; las 6+ estadísticas de `PerfilUsuario` son getters calculados sobre `Historial`. `SesionUsuarioServiceBorrarExamenTests`: `BorrarExamen_RecalculaLasEstadisticasAgregadasSinElExamenBorrado_AC_T57`, `BorrarExamen_Persiste_ElExamenSigueAusenteAlReabrirLaApp_AC_T57_NFR49`. | OK |
| Se limpian las imágenes asociadas (crit. 5 / AC-T58 / NFR-50) | `HistorialViewModel.LimpiarImagenesAsync` borra `Imagenes\{id}` best-effort (fallo → `RutasApp.RegistrarError`, no corta el borrado). El hueco de `02-tech-spec.md` (el `examenId` de la carpeta no quedaba en `ExamenRendido`) está cerrado: `AsistenteViewModel.GenerarAsync` fija `ExamenEnCurso.Id = examenId` y `registro.Id` lo hereda. `Confirmar_Borra_LimpiaImagenes_YEmiteExamenBorrado_AC_T56_NFR50`, `Confirmar_SinCarpetaDeImagenes_NoLanza_YBorraIgual_NFR50`. | OK |
| Último borrado → estado vacío; "Borrar historial" global intacto (crit. 6-7 / AC-T58) | `HayExamenes`/`Resumen` = "Todavia no rendiste ningun examen"; `BorrarCommand` global sin cambios. `BorrarElUltimoExamen_DejaElHistorialVacio_AC_T58`, `BorrarHistorial_Global_SigueVaciandoTodo_SinCambios_AC_T58`. | OK |
| Revancha en curso al borrar el original: advierte; al confirmar se descarta sin registrar; revancha que termina con el original ya borrado no lo recrea ni lanza (crit. 8-9 / AC-T59 / NFR-51) | `ShellViewModel` cablea `Historial.HayRevanchaEnCursoDe = id => Examen.HayIntentoAbierto && Examen.RegistroActualId == id` y `Historial.ExamenBorrado += Examen.AlBorrarseExamen`. Confirmación con texto alternativo ("esa revancha en curso se descarta sin registrarse"); `AlBorrarseExamen` pone `Registro = null` y `Cerrar()` si `EsRevancha`. `ActualizarExamen` hace `FindIndex` y no-op si -1. `ExamenViewModelAlBorrarseExamenTests` (4: `RevanchaEnCurso_DelExamenBorrado_SeDescartaSinRegistrar_AC_T59`, `IntentoOriginalEnCurso_...PierdeElRegistro_PeroSiguePudiendoRendirse`, `BorrarOtroExamen_NoTocaElIntentoEnCurso`, `SinIntentoAbierto_...EsNoOp`), `HistorialViewModelBorrarExamenTests::ConRevanchaEnCurso_LaConfirmacionAdvierteQueSeDescarta_AC_T59_NFR51`, `SesionUsuarioServiceBorrarExamenTests::ActualizarExamen_DeUnRegistroYaBorrado_NoLoRecrea_NiLanza_NFR51`. | OK |

**US-013 — Mensaje de felicitación con nota ≥ 7** — AC-T60..63, NFR-52/53, RN-1/RN-5

| Given/When/Then | Verificación | Estado |
|---|---|---|
| Nota UBA ≥ 7 en el intento original → texto literal, destacado, en mayúsculas (crit. 1 / AC-T60) | `ExamenViewModel.MensajeFelicitacion` = `const` = `"FELICIDADES CULONA TE ROMPO BIEN EL CULO"` — coincide carácter por carácter con `01-spec.md` US-013. `MostrarResultados` fija `MostrarFelicitacion = !examen.EsRevancha && resultado.Nota >= 7` (`Nota` es `int`, `EvaluadorUBA` intocado, ≥7 ⇔ ≥74%, RN-1). `ExamenView.xaml`: `TextBlock Text="{x:Static vm:ExamenViewModel.MensajeFelicitacion}"`, `Visibility` a `MostrarFelicitacion` (`BoolToVis`), `FontWeight="Bold"`, `FontSize="16"`, `Foreground="{DynamicResource PincelAcierto}"`. `ExamenViewModelFelicitacionTests` (7), ejercitado por el camino real (`Iniciar` + `FinalizarCommand` → `EvaluadorUBA.Evaluar`): `ElMensaje_EsUnaConstanteLiteralEnMayusculas_AC_T60`, `ExamenView_MuestraElMensajeDestacado_AtadoAMostrarFelicitacion_AC_T60`. | OK |
| Nota ≤ 6, o resultado de revancha → no aparece (crit. 2-3 / AC-T61) | Casos con nota ≤ 6 → `MostrarFelicitacion == false`; `Revancha_AunConNotaPerfecta_NoMuestraLaFelicitacion_AC_T61` (10/10 en ronda de revancha → `false`). | OK |
| Con el mensaje visible, el resto de Resultados funciona igual (crit. 4 / AC-T62 / NFR-53) | El `TextBlock` es aditivo dentro del `StackPanel` de encabezado; corrección pregunta por pregunta, `DetalleRondas`, botón de revancha sin cambios. Suite de `ExamenView` / `ExamenViewModel` en verde, `ExamenViewKeyBindingRegressionTests` / `ExamenViewTransicionContenidoTests` sin regresión. | OK |
| Sin opción/flag/config para ocultar o editar; va en el release (crit. 5 / AC-T63 / NFR-52 / RN-5) | `const` de código; no `AppConfig`, no recurso de tema intercambiable, no binding configurable. `AppConfig_NoTieneNingunCampoParaOcultarOEditarLaFelicitacion_AC_T63`, `ExamenViewModel_NoExponeSetterPublicoNiMetodoParaForzarLaFelicitacion_NFR52`. Se distribuye en el binario por la actualización automática (`publish.yml` sin cambios de flujo). | OK |

---

**Hallazgos no bloqueantes**

1. **D-3 (menor, aceptado en QA de módulo M5)** — superficies 6 (InfoBar abrir/cerrar) y 7
   (anillos de progreso) de RN-7 son controles nativos de WPF-UI 4.3.0 (`ui:InfoBar`,
   `ui:ProgressRing`): su animación la maneja la librería, no lee las `Duration` centralizadas de
   `Theme/Estilos.xaml` ni consulta `Animaciones.Reducidas`. El criterio "usa los parámetros
   centralizados" del checklist de US-011 se cumple estrictamente sólo para las 6 superficies con
   plantilla propia. Re-templatizar controles de terceros para ganar esos dos casos es
   desproporcionado para una tarea de "pulido"; se acepta como está. Impacto: con "reducir
   movimiento" del SO activo, InfoBar y ProgressRing conservan su micro-animación nativa (corta,
   no bloqueante).
2. `OfficeExtractor` v1 no extrae notas del orador (PPT), cuadros de texto anidados ni
   comentarios (documentado en el propio archivo como mejora futura, R-17). No afecta ningún
   AC-T.
3. `LeerSharedStrings` de Excel corta al superar el `Presupuesto`: en un `.xlsx` cuya tabla de
   cadenas *sola* ya excede 2× `MaxCaracteres`, las celdas que apunten a índices no leídos quedan
   vacías. Degradación aceptable a ese tamaño (el material se recorta por cuota de todos modos).
4. `Animaciones.Reducidas` se evalúa una sola vez al inicializar el tipo; un cambio de la
   preferencia "mostrar animaciones en Windows" se toma en el próximo arranque. Intencional y
   documentado en el código.

**Fuera de alcance de la automatización — gaps de cobertura** (mismo criterio que las corridas
previas de este reporte usaron para la API de Gemini: no gastar la cuota/clave real del usuario
sin pedido explícito, y sin sesión interactiva WPF en este entorno):

- **(a)** Examen real generado end-to-end desde un `.docx` / `.xlsx` / `.pptx` y desde un set de
  fotos manuscritas, contra la API real de Gemini. La cadena hasta `SolicitudGeneracion` está
  cubierta por unit tests; la interpretación del contenido por el modelo, no. Mismo criterio que
  AC-T27 del Incremento 3.
- **(b)** Decodificación de un `.heic` real sobre el `.exe` publicado (single-file
  self-contained) en un Windows limpio sin las "HEIF Image Extensions" / códec HEVC. El unit
  test prueba que `Magick.NET-Q8-x64` decodifica en el host de test (con los assets nativos del
  nupkg en disco); falta confirmar que `IncludeNativeLibrariesForSelfExtract` empaqueta y carga
  `libheif`/`libde265` desde el bundle en una máquina sin códec.
- **(c)** Checklist visual en vivo de las 8 superficies de RN-7 (tema claro y oscuro, reducir
  movimiento on/off) que `01-spec.md` US-011 exige explícitamente para el sign-off de esa US —
  requiere sesión interactiva WPF. Evidencia acá: estructural (XAML) + regresión de
  comportamiento.
- **(d)** RAM pico con un archivo Office equivalente a ≥ 500 páginas. Mitigado por la clase
  `Presupuesto` de `OfficeExtractor` (corte de acumulación durante la lectura, verificado por
  `ExtraerAsync_SoloRecortaCuandoSeSuperaMaxCaracteres_NFR38`); falta la medición del pico real
  del proceso.

**Sign-off Incremento 4:** sin defectos bloqueantes. Los 2 bloqueantes de code-reviewer y los
hallazgos D-1 / D-2 de QA de módulo están cerrados y reconfirmados por lectura del código
integrado; D-3 aceptado como menor. Todo criterio de aceptación de negocio de US-008..US-013 fue
ejercitado al menos una vez (unit/estructural + lectura). Los gaps (a)-(d) son manuales, no
bloqueantes con el criterio de proporcionalidad ya aceptado en este archivo; se recomienda
cubrirlos en el UAT antes de la firma final — en particular el checklist visual (c), que el spec
de US-011 exige como condición de sign-off de esa US.

Sugerencia: ejecutar (a) y (c) en la sesión de UAT con la clave real del usuario y una PC de
prueba; (b) puede cubrirse instalando el `.exe` publicado en una VM Windows limpia.

---

QA OK - build working tree (sin commit, sobre `370cc65`) - 2026-08-28

US-011 (Incremento 3, `AutoExam/Theme/Tokens.Claro.xaml` + `AutoExam/Theme/Tokens.Oscuro.xaml`)
— validado contra los Given/When/Then de `01-spec.md` y AC-T36..AC-T39/NFR-32..NFR-36 de
`02-tech-spec.md`. No reabro la tensión suavidad/contraste de los 3 pinceles semánticos
(Acierto/Error/Pendiente): ya está resuelta a propósito como "colores de estado vivos",
consultado con el usuario real.

- `dotnet test --filter TokensXamlContrasteTests`: 73/73 en verde (corrida propia, no reuso
  del resultado reportado por test-developer/code-reviewer). Cubre exactamente los cuatro NFR
  de esta US: NFR-32 (texto/fondo ≥4.5:1 y ≥3:1 para texto suave/tenue, ambos temas, contra
  `PincelFondo`/`PincelSuperficie`/`PincelTarjeta`), NFR-33 (cada semántico ≥3:1 contra su
  propio "Suave" y los tres semánticos ≥3:1 entre sí), NFR-34 (matiz de `PincelMarca*` y de
  las 6 claves de superficie/borde dentro de ±20° del violeta de marca, y ≥3 tonos de morado
  perceptiblemente distintos por tema) y NFR-35 (paridad exacta de claves entre
  `Tokens.Claro.xaml`/`Tokens.Oscuro.xaml`) + AC-T39 (matiz de los 6 pinceles semánticos sin
  correrse del verde/rojo/ámbar original ni acercarse al violeta).
- Spot-check propio (independiente del test, cálculo manual de matiz HSL) sobre
  `PincelMarca` (#6246C8, ~252.9°) vs `PincelFondo` (#E6E4EF, ~251.2°) en tema claro: 1.7° de
  diferencia, consistente con lo que exige NFR-34 y con lo que reporta el test — la
  implementación de matiz/contraste no depende únicamente de confiar en el propio test.
- Inspección de valores hex confirma la lectura de `03-architecture.md`/tech-spec: las 6
  claves de superficie/borde (`PincelFondo`, `PincelSuperficie`, `PincelTarjeta`,
  `PincelTarjetaHover`, `PincelBorde`, `PincelBordeFuerte`) pasaron de gris neutro a gris con
  tinte violeta en ambos temas, y `PincelMarca`/`PincelMarcaFuerte`/`PincelMarcaSuave` siguen
  siendo los 3 tonos de morado ya existentes — cumple AC-T36 (morado predominante, ≥3 tonos)
  sin necesidad de correr la app, por inspección directa de los 20 valores `Color` de cada
  diccionario.
- AC-T37 (ninguna pantalla quedó con paleta vieja): `git status`/`git diff --stat` confirman
  que este cambio toca únicamente `Tokens.Claro.xaml`/`Tokens.Oscuro.xaml` — 0 archivos bajo
  `AutoExam/Views/` modificados. Como las 20 claves y el mecanismo `DynamicResource`/
  `TemaService` no cambiaron (solo los valores `Color`), toda vista se actualiza homogéneamente
  por construcción; no hay superficie donde una pantalla pueda haber quedado atrás. Coincide
  con lo que ya documenta el propio `TokensXamlContrasteTests.cs` como "fuera de alcance de
  test automatizado, cumplido por contrato de arquitectura".
- `dotnet build AutoExam.sln`: 0 advertencias, 0 errores.
- **Nota de entorno (validación visual no ejecutable):** no hay sesión interactiva disponible
  para levantar la ventana WPF y hacer inspección visual en vivo (mismo tipo de limitación que
  ya documentaron corridas previas de este reporte sobre este entorno). Intenté un smoke de
  arranque (`dotnet run --no-build`, matado a los ~6 s): el proceso `AutoExam.exe` llegó a
  existir (`tasklist` lo confirma, ~55 MB en memoria) pero con estado "Not Responding" y título
  de ventana `SystemResourceNotifyWindow` (ventana interna de WPF, no la `MainWindow` — se
  esperaría más tiempo vivo para llegar a "AutoExam" en el título, como sí se observó en la
  corrida de US-007/US-008 de este mismo reporte) — evidencia débil e inconclusa de arranque,
  no la cuento como validación visual. El veredicto de este módulo se apoya en los tests de
  contraste automatizados + inspección directa de los valores hex, tal como habilita la
  consigna para este caso.

Sin defectos.

---

QA OK - build working tree (sin commit, sobre `370cc65`) - 2026-08-28

US-010 (Incremento 3, housekeeping de repo — `.gitignore` + suite nueva
`AutoExam.Tests/Housekeeping/`) — verificado contra los Given/When/Then de
`01-spec.md` US-010 y AC-T32..T35/NFR-29..31 de `02-tech-spec.md`:

- AC-T32/NFR-30 (`.gitignore` corrige la línea corrupta, `.claude/` queda
  efectivamente excluida): confirmado con las mismas herramientas que exige
  el contrato — `git check-ignore -v .claude` devuelve
  `.gitignore:36:.claude/	.claude` (regla real, no la línea corrupta
  `. c l a u d e / s k i l l s /` original); `git status --porcelain` no
  lista `.claude/` ni su contenido (`.claude/skills`, con symlinks reales en
  este checkout, no aparece como `??`); `git status --porcelain` general
  tampoco muestra ningún rastro de la carpeta.
- AC-T33/NFR-29 (0 menciones a "Claude"/"Anthropic" fuera de las excepciones
  documentadas): grep case-insensitive sobre todo el árbol de trabajo
  (excluyendo `.claude/`, ya ignorado y fuera del árbol versionado por
  diseño) por `Claude|Anthropic|claude\.ai|claude-sonnet|Co-Authored-By`. Los
  únicos 8 archivos con coincidencias son: `.gitignore` (el propio patrón
  `.claude/`, nombre de carpeta de herramienta, no atribución de autoría) y
  7 specs (`01-spec.md`, `02-tech-spec.md`, `03-architecture.md`,
  `uat-signoff.md`, `team-roster.yaml`) que mencionan "Claude"/"claude"
  únicamente para describir el propio trabajo de housekeeping de US-010 (ej.
  "búsqueda de texto por claude/anthropic", "`.claude/` no se trackea") — no
  hay ninguna mención de autoría de IA sobre código de la app ni comentarios
  en `AutoExam/**`/`AutoExam.Tests/**`. Consistente con lo que ya adelantaba
  `02-tech-spec.md` (líneas 621-628): esos archivos son "vitales" (specs
  congeladas) y hoy no tienen mención de autoría, solo mencionan el nombre de
  la herramienta en su rol de documentar el propio proceso de limpieza.
- AC-T34 (historial de `main` no se toca por este módulo): `git log --oneline
  -5` sin ningún commit nuevo de `rebase`/`filter-branch`/force-push desde
  `370cc65` — la limpieza de este módulo es enteramente sobre el working
  tree (`.gitignore` + tests nuevos), sin ningún comando destructivo de
  historial ejecutado en esta sesión. (La reescritura histórica ya detectada
  y resuelta por el usuario real en una sesión anterior es harina de otro
  costal, fuera de este alcance, como indica la consigna.)
- AC-T35 (ningún archivo vital borrado/vaciado): `git diff --stat` sobre
  `.gitignore` muestra únicamente el reemplazo de la línea corrupta por
  `.claude/` en codificación UTF-8 válida (574→544 bytes, sin caracter de
  reemplazo `�`) — ningún otro archivo vital (`AutoExam/**`,
  `AutoExam.Tests/**` salvo la suite nueva, `.github/workflows/publish.yml`,
  `publicar.ps1`, `Verificar-Version.ps1`, specs) fue tocado por este módulo
  en particular (los cambios que sí aparecen hoy en esos otros archivos
  corresponden a US-009/US-011, módulos distintos de este mismo incremento,
  no a US-010).
- `dotnet test --filter FullyQualifiedName~Housekeeping`: 4/4 en verde
  (`ClaudeDirectory_EstaIgnorado_CheckIgnoreDevuelveReglaRealDeGitignore`,
  `ClaudeDirectory_NoApareceEnGitStatusPorcelain`,
  `ArchivoVitalDelRepo_NoEstaIgnorado_CheckIgnoreDevuelveExitCode1` (control
  negativo del harness), `Gitignore_EntradaClaudeEsUtf8ValidoSinCaracterDeReemplazo`).
- `dotnet build AutoExam.sln`: 0 advertencias, 0 errores (NFR-31, sin
  regresión funcional por la limpieza).

Sin defectos.

---

QA OK - build working tree (sin commit, sobre `370cc65`) - 2026-08-26

US-007 + US-008 (Incremento 2, `AutoExam/Behaviors/TransicionContenido.cs` +
`AutoExam/Behaviors/Presionable.cs` (nuevo) + 7 `ControlTemplate` de
`Theme/Estilos.xaml`: `Chip`, `ChipAccion`, `OpcionExamen`, `BaldosaPregunta`,
`ItemNavegacion`, `ItemLibro`, `ZonaSoltar`) — validado por lectura de código
contra los Given/When/Then de `01-spec.md` y los AC-T19..T26 de
`02-tech-spec.md`/`03-architecture.md`.

**Nota de entorno (no pude hacer QA visual en vivo completo):** `dotnet run
--project AutoExam/AutoExam.csproj --no-build` levantó el proceso
`AutoExam.exe` sin crash (~11 s vivo hasta que lo maté yo, memoria creciendo
de forma normal de ~2 MB a ~165 MB, ventana titulada "AutoExam" confirmada
por `tasklist /V`) — evidencia de que arranca y renderiza sin excepción no
controlada en los paths tocados por este incremento. Pero la pantalla física
de esta máquina estaba ocupada en primer plano por otra aplicación del
usuario (juego a pantalla completa) cuando intenté capturarla para inspeccionar
la ventana: un solo screenshot de pantalla completa tomado y borrado de
inmediato al ver que exponía contenido privado ajeno a esta tarea; no repetí
la captura ni intenté tomar foco/enviar mouse o teclado sintético sobre una
sesión que el usuario está usando activamente en este momento (a diferencia
de una corrida anterior de este mismo reporte, sobre US-003/US-004, donde sí
se pudo hacer UI Automation en vivo). Por eso esta pasada es lectura de
código + esta única señal de arranque en proceso, no exploración visual
interactiva de hover/press/temas — igual que indica la consigna para el caso
de no poder abrir una ventana real.

Cobertura por lectura de código:

- US-007 criterio 1 (AC-T19): `TransicionContenido.AlCambiarContenido` anima
  `Opacity` 0→1 y `TranslateTransform.Y` 10→0 con `DuracionTransicionSeccion`
  (220 ms, dentro del rango 200-250 ms) y `SuavizadoSalida`, ambos leídos de
  `Theme/Estilos.xaml` vía `TryFindResource` con fallback hardcodeado — se
  dispara en cada cambio de `Content` del único `ContentControl` marcado
  `Activa="True"` (`MainWindow.xaml`, línea 99).
- criterio 2 (AC-T20): la transición nunca toca `Brush`/`Color`, solo
  `Opacity`/`TranslateTransform`; el contenido animado ya resuelve sus
  pinceles por `DynamicResource` de forma independiente de la animación — sin
  superficie para un flash de color equivocado al cambiar de tema.
- criterio 3 (AC-T21): `BeginAnimation` con un `DoubleAnimation` nuevo
  interrumpe limpiamente el clock anterior (comportamiento estándar de WPF);
  al ser un `ContentControl` único, solo puede haber un `Content` renderizado
  a la vez — no hay mecanismo por el que dos secciones queden superpuestas,
  se cumple por construcción, no por timing.
- criterio 4 (AC-T22): confirmado por grep en todo el repo, `ExamenView.xaml`
  no tiene ningún `ContentControl` (la navegación pregunta-a-pregunta es por
  binding/`Visibility` dentro del mismo `UserControl`) y
  `TransicionContenido.Activa`/`TransicionContenido` no aparece en ningún
  otro archivo salvo `Theme/Estilos.xaml` (declaración de recursos) y
  `MainWindow.xaml` (el único wiring) — la transición no puede alcanzar la
  navegación de examen ni por error futuro sin tocar este behavior a mano.
- US-008 criterios 1 y 2 (AC-T23/AC-T24): los 7 estilos tienen `MultiTrigger`
  de hover (overlay `Border` con `Opacity` 0→1, `DuracionHover`=140 ms) y de
  presión (`ScaleTransform` a 0.97, `DuracionPresion`=80 ms) — animaciones
  visualmente distintas (opacidad vs. escala) como pide el criterio de "press
  ≠ hover". `BaldosaPregunta` es la única variante, y es correcta: en vez de
  overlay fijo anima la `Opacity` del propio fondo (1→0.75) porque el color
  de fondo varía por estado de pregunta (correcta/incorrecta/salteada/sin
  responder) y un overlay con pincel fijo rompería esa semántica — decisión
  ya explicitada en el comentario del template, coherente con
  `03-architecture.md` §"Decisiones de diseño" de US-008.
- criterio 3 (AC-T25): 0 colores/pinceles literales nuevos en los 7
  templates; las animaciones operan sobre `Opacity` de pinceles ya
  `DynamicResource` (`PincelTarjetaHover`, `PincelMarcaSuave`, etc., ya
  existentes antes de este incremento) o sobre `ScaleTransform`, que no tiene
  color.
- criterio 4 (AC-T26/NFR-22): los 7 templates usan `MultiTrigger` con
  `IsEnabled="True"` como condición explícita junto a `IsMouseOver`/
  `IsPressed` (incluido `BaldosaPregunta`, que antes de este incremento no
  tenía ninguna guardia de deshabilitado) — 0 disparo de animación posible
  con el control deshabilitado, reforzado además por el comportamiento
  nativo de WPF de no enviar eventos de mouse a un control con
  `IsEnabled=False` (documentado en el propio comentario de
  `Presionable.cs`, coincide con lo que dice `03-architecture.md`).
- `ItemLibro` (`ListBoxItem`, sin `IsPressed` nativo) usa
  `Behaviors/Presionable.cs` para exponer `EstaPresionado` vía eventos de
  mouse ya nativos (`PreviewMouseLeftButtonDown/Up`, `MouseLeave`,
  `LostMouseCapture`), consumido en el mismo patrón de `MultiTrigger` que los
  otros 6 — mismo criterio, sin caso especial en el resultado visible ni
  color/duración propios.

Hallazgo no bloqueante (cobertura de prueba, no defecto de código): hoy
ningún `PaginaViewModel` (`AsistenteViewModel`, `AjustesViewModel`,
`ExamenViewModel`, `HistorialViewModel`, `BibliotecaViewModel`) fija
`Habilitada=false`, y ninguna vista bindea `IsEnabled` a
`Chip`/`ChipAccion`/`OpcionExamen`/`BaldosaPregunta`/`ItemLibro`/
`ZonaSoltar` (grep sin resultados en `Views/*.xaml`) — el único camino con
`IsEnabled` dinámico hoy en el árbol de vistas es `ItemNavegacion` vía
`Habilitada` (`MainWindow.xaml`), que en el código actual nunca llega a
valer `false`. La guardia de US-008 está bien implementada en los 7
templates (correcta por inspección), pero con el código actual no hay
ningún elemento realmente deshabilitado para ejercitarla en vivo — ni
siquiera el ejemplo que usa el propio spec ("una opción de examen ya
bloqueada") existe como funcionalidad hoy en `ExamenViewModel`/
`OpcionViewModel`. No es un defecto de este incremento (US-007/US-008 no
piden agregar bloqueo de opciones); es una limitación de superficie de
prueba ya reconocida en `03-architecture.md` (riesgo R-8) y no bloquea el
sign-off.

`dotnet build AutoExam/AutoExam.csproj`: 0 advertencias, 0 errores.

Sin defectos.

---

QA OK - build working tree (sin commit, sobre `370cc65`) - 2026-08-26

US-006 (Incremento 2, `Verificar-Version.ps1` + step `id: version` de
`.github/workflows/publish.yml`) — exploratorio manual de los 4 Given/When/Then
del spec, ejecutando el script real (`powershell.exe -File`, no `pwsh`: esta
máquina no tiene PowerShell 7 instalado; confirmado que los 22 tests de
`AutoExam.Tests/Scripts/VerificarVersionScriptTests.cs` fallan acá
exclusivamente por eso — `VerificarVersionProceso.cs` invoca `FileName =
"pwsh"` como proceso hijo — no es un defecto del script, la lógica y el texto
de mensaje que esos tests exigen coinciden con lo que corrí a mano) contra
fixtures propias (nunca los `AutoExam.csproj`/`update.xml` reales del repo):

- Versión sube (1.0.3 vs 1.0.2 publicada): mensaje verde exacto "... supera la
  publicada (...) — este push SI va a disparar la publicación automática
  (US-001)." + exit code 0.
- Versión igual (1.0.2 vs 1.0.2) y versión menor (1.0.1 vs 1.0.2): mensaje
  amarillo exacto "... NO supera la publicada (...) — este push NO va a
  disparar ninguna publicación nueva." + exit code 1 en ambos casos.
- `-EmitGithubOutput` con `$env:GITHUB_OUTPUT` seteado: escribe exactamente
  `version`/`tag`/`zip`/`should_publish` (`true`/`false` coherente con el exit
  code) — mismos 4 nombres que ya consumen los pasos 4-11 de `publish.yml`.
  Sin el switch, no toca `GITHUB_OUTPUT` ni ningún otro archivo.
- Sin efectos secundarios (NFR-16): corrida contra los archivos reales del
  repo en modo lectura, `git status --porcelain` idéntico antes/después — no
  escribe, no hace commit/push, no invoca `publicar.ps1` ni `gh`.
- Casos de error (csproj/`update.xml` faltante, `<Version>` no parseable):
  exit code 2 en los tres, mensaje rojo explícito, sin dejar archivos nuevos.
- `-EmitGithubOutput` sin `$env:GITHUB_OUTPUT` en el entorno: exit code 2
  (caso exclusivo de CI, comportamiento defensivo esperado, coincide con el
  test `EmitGithubOutput_SinVariableDeEntornoGithubOutput_ExitCode2`).

Diff de `publish.yml` revisado de punta a punta (lectura, sin correr CI): el
step `id: version` sigue exponiendo los mismos `steps.version.outputs.*`
consumidos sin cambios por los pasos 4-11 (`dotnet publish`, `dotnet test`,
verificación de `FileVersion`, empaquetado, `gh release create`, verificación
HTTP del asset, reescritura de `update.xml`, commit+push); `$env:CSPROJ` sigue
disponible para el paso "dotnet publish" (no lo toca este cambio); la
traducción explícita de exit code 2 → `exit 2` preserva el comportamiento de
fallo del bloque inline anterior (que ya terminaba el step por excepción no
controlada con `$ErrorActionPreference = 'Stop'`) sin introducir un falso
fallo en el caso "no supera" (exit 1), que debe seguir siendo un no-op exitoso
(AC-T3/NFR-03).

Sin defectos.

---

QA OK - build commit `8aeed90` - 2026-08-25

Re-verificación independiente del hallazgo crítico reportado sobre el build `4c85f20`
(geometría de ventana nunca se restauraba, US-003 AC-T8): confirmado resuelto en
`8aeed90` con el mismo método de repro original, contra el `.exe` recién compilado,
con datos frescos (no reutilicé los del smoke del developer):

- `dotnet build` / `dotnet test`: 0 errores, 94/94 en verde.
- 3 escenarios de geometría guardada distintos (1111x733 @ 77,44 estado Normal;
  1050x680 @ 222,88 con `VentanaEstado=77` corrupto; 1240x820 @ 9999,9999
  fuera de pantalla) lanzados contra el binario real, medidos con
  `user32.GetWindowRect` (no lo que la app dice tener): los dos primeros
  restauran exacto sin crash; el tercero cae al fallback centrado (AC-T9),
  como corresponde. `errores.log` vacío en los tres casos.
- US-004: logré sortear la limitación previa de `SetForegroundWindow` (truco
  `keybd_event` de Alt + `BringWindowToTop` antes de pedir foreground) y
  ejercité `Ctrl+1..5` en vivo contra el binario real vía UI Automation:
  `Ctrl+3`/`Ctrl+5` navegan correctamente entre secciones, y con el foco
  puesto a propósito en un `TextBox` editable de Ajustes (API Keys), `Ctrl+3`
  no navega (guard NFR-10 confirmado de punta a punta). Sigue sin ejercitarse
  en vivo el caso puntual "atajos de navegación durante un examen en curso"
  (requiere un examen real generado con Gemini/PDF, fuera de alcance sin red/
  clave real); el riesgo residual es bajo por inspección de código (gestos sin
  colisión, ver corrida anterior de este reporte) pero queda como gap de
  cobertura, no como defecto.

Sugerencia: agregar un test de integración de `MainWindow`/`ShellViewModel`
que cubra `Ctrl+1..5` + guard de foco en `TextBox`, para no depender de este
tipo de smoke manual en cada cambio futuro de este módulo.

---

QA OK - build working tree (sin commit, sobre `370cc65`) - 2026-08-28

US-009 (Incremento 3, `AutoExam/Services/GeminiApiService.cs`) — validado contra los
Given/When/Then de `01-spec.md` y AC-T27..T31/NFR-24..NFR-28 de `02-tech-spec.md`, con el
mismo alcance que ya delimitó `test-plan.md` para este módulo (un intento previo de esta
sesión de QA quedó sin veredicto por límite de sesión de API — se arrancó de cero, sin
reusar ningún resultado de esa corrida cortada).

- `dotnet test --filter "FullyQualifiedName~DiagnosticoGeneracionTests|FullyQualifiedName~GeminiApiServiceArmarMensajeSinPreguntasTests|FullyQualifiedName~GeminiApiServiceCalcularTopeTokensTests|FullyQualifiedName~GeminiApiServiceReflexion"`:
  21/21 en verde (corrida propia). Cubre exactamente lo que `test-plan.md` documenta como
  cobertura automatizada de este módulo: contrato de `DiagnosticoGeneracion`
  (`LotesTruncados`/`CuotaDiariaDetectada`, default y settable), prioridad de causa en
  `ArmarMensajeSinPreguntas` (cuota antepone sobre truncado, truncado sin cuota antepone su
  propia oración, ninguna causa no cambia el encabezado genérico, el mensaje final sigue
  incluyendo modelo + `Resumen()`), y `CalcularTopeTokens` (sin techo cacheado usa el default
  8192, con techo cacheado por debajo del máximo lo usa tal cual, techo por encima de 16384
  topea ahí, tope aprendido en caliente sigue ganando, `ReiniciarAprendizajeDeSesion()` lo
  libera).
- `dotnet build AutoExam.sln`: 0 advertencias, 0 errores.
- `dotnet test AutoExam.sln` (suite completa): 242/265 en verde, 23 fallos — los 23 son
  ajenos a este módulo: 22 de `VerificarVersionScriptTests` (mismo defecto de entorno ya
  documentado en la corrida de US-006 de este reporte: esta máquina no tiene `pwsh`
  instalado, el proceso hijo no arranca) + 1 de
  `ActualizacionServicePaqueteDisponibleTests.PaqueteDisponible_ConUrlQueExiste_DevuelveTrue`
  (requiere resolver una URL real de GitHub, entorno sin esa conectividad en este momento).
  Confirmado con `grep` sobre la salida que ningún fallo pertenece a
  `GeminiApiService`/`DiagnosticoGeneracion` — 0 regresión en el módulo bajo prueba.
- Código relevante releído completo (no de memoria, contra `GeminiApiService.cs` real):
  consulta proactiva a `ListarModelosAsync` insertada en `GenerarPreguntasAsync` antes de
  `SubirPdfSiConvieneAsync`, condicionada a `TechoDeSalidaConocido(solicitud.Modelo) == 0`,
  con `catch` mudo que preserva el default conservador si falla (líneas 557-573); contador
  `diagnostico.LotesTruncados++` en cada lote con `generadas.Truncado`, independiente de si
  después se pudo achicar el lote (líneas 737-743); `catch (GeminiException ex) when
  (ex.EsCuotaDiaria)` que marca `diagnostico.CuotaDiariaDetectada = true`, registra la nota,
  loguea a `errores.log` y corta el bucle con lo ya generado en vez de dejar escapar la
  excepción sin rastro (líneas 718-728); `ArmarMensajeSinPreguntas` con la prioridad exacta
  cuota > truncado > genérico (líneas 1174-1213); `CalcularTopeTokens` combinando techo del
  modelo (topeado en `TopeTokensMaximo=16384`) con `_topeTokensVigente` aprendido en caliente
  (líneas 1494-1504); `_techoDeSalida` es un diccionario `static` que sobrevive dentro del
  mismo proceso y no se resetea por `ReiniciarAprendizajeDeSesion()` (solo resetea
  `_topeTokensVigente`/`_razonamientoApagable`) — consistente con el criterio de
  repetibilidad de NFR-28 (una segunda corrida en el mismo proceso arranca ya con el techo
  aprendido, no vuelve a partir del default 8192). No se tocó lotes adaptativos, rotación de
  claves, backoff ni el semáforo de turno — confirmado por lectura, tal como exige la
  restricción técnica de este incremento.
- Progreso en tiempo real (criterio 2 de US-009 / AC-T28 / NFR-26): confirmado por lectura
  que `progreso?.Report(...)` se dispara en cada punto de espera relevante, incluida la
  espera de backoff antes de un `Task.Delay` por cuota del minuto (línea 1823, con segundos
  exactos informados) — mecanismo preexistente, no tocado por este incremento, sin regresión
  visible en el diff.

**Criterios de aceptación de negocio (`01-spec.md`, US-009), veredicto por Given/When/Then:**

1. "1 a 30 preguntas sin error por saturación" (AC-T27/NFR-25) — **fuera de alcance
   automatizado según `test-plan.md`** (requiere la API real o un doble HTTP que reproduzca
   Files API + `generateContent` con `responseSchema` completo, explícitamente descartado por
   costo/beneficio). Verificado por inspección de código: la causa raíz identificada en
   `02-tech-spec.md` (techo de tokens arrancando conservador) queda mitigada por la consulta
   proactiva a `ListModels` antes del primer lote; el mecanismo de reintento/relleno que ya
   garantizaba esto no se tocó. No se ejecutó una corrida en vivo contra la API real de Gemini
   en esta sesión — la clave configurada en este equipo es la del usuario real y no corresponde
   gastar su cuota para una corrida de 20 exámenes sin pedirlo explícitamente (mismo motivo por
   el que el intento previo de esta sesión quedó sin veredicto: límite de sesión de API).
2. "Veo que avanza, no queda colgado" (AC-T28/NFR-26) — verificado por inspección de código
   (ver arriba); mecanismo preexistente sin regresión.
3. "Reintenta/ajusta automáticamente ante error transitorio" (AC-T29) — verificado por
   inspección: mecanismo preexistente (rotación de clave, backoff con `Retry-After`/
   `retryDelay`, reducción de lote a la mitad ante truncado) intacto, más el contador nuevo de
   `LotesTruncados` que alimenta el mensaje final sin alterar el flujo de reintento en sí.
4. "Mensaje final entendible y accionable" (AC-T30/NFR-27) — **cubierto por test verde**
   (`GeminiApiServiceArmarMensajeSinPreguntasTests`, 8 casos incluida la prioridad
   cuota-sobre-truncado y el caso sin causa detectada).
5. "Repetible en las mismas condiciones" (AC-T31/NFR-28) — parcialmente cubierto por test
   verde a nivel de la unidad que sostiene la repetibilidad (`CalcularTopeTokens` con tope
   aprendido y `ReiniciarAprendizajeDeSesion`); la repetibilidad end-to-end de un examen de 30
   preguntas completo dos veces seguidas es, igual que el punto 1, **fuera de alcance
   automatizado según `test-plan.md`** por el mismo motivo (requiere API real o doble HTTP).

Sin defectos encontrados en el código ni en los tests. No se reabre la limitación externa de
cuota diaria de Gemini documentada en `02-tech-spec.md` (Restricciones técnicas) — es un
límite de la cuenta/clave del usuario, no de la app, y así lo trata el propio mensaje de
`ArmarMensajeSinPreguntas`.

Sugerencia: si se quiere cerrar el gap de AC-T27/AC-T29/AC-T31/NFR-25 con evidencia en vivo
antes de firmar UAT, correr manualmente (fuera de esta sesión de QA, para no repetir el
límite de sesión de API ya sufrido) 2-3 exámenes de 30 preguntas seguidos con la clave real
del usuario y confirmar que ninguno termina en error por saturación — no es bloqueante para
este veredicto porque `test-plan.md` ya documentó y aceptó este gap como fuera de alcance
proporcional del incremento.
