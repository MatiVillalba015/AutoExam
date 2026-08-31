# 02 — Tech Spec: fuentes nuevas, pulido de UX y ajustes de historial/resultado (US-008 a US-013)

Traduce el `specs/01-spec.md` **vigente y congelado** (US-008 a US-013) a especificación
técnica. Sin decisión de stack (la toma arquitecto-tecnico); el stack y las convenciones del
repo son restricción dura, no punto a evaluar (ver "Restricciones técnicas").

> **Sobre la numeración.** Este archivo tenía antes tres incrementos que traducían un
> `01-spec.md` anterior (US-001 a US-011, features distintas: pipeline de publicación, diálogos
> temáticos, geometría de ventana, atajos, tamaño de texto, chequeo de versión, transición de
> sección, hover/press, confiabilidad Gemini, housekeeping, paleta morada). Ese trabajo ya está
> implementado y firmado hasta `v1.0.3` — su tech-spec vive en el historial de git y su
> arquitectura en `specs/03-architecture.md`. El `01-spec.md` vigente reusa los IDs
> US-008..US-011 para otras cosas. En este documento **US-XXX refiere siempre al `01-spec.md`
> vigente**. La numeración de NFR y AC-T continúa desde donde quedó (NFR-37+, AC-T40+) para no
> pisar referencias del histórico.

**Supuestos:**
- "Material"/"Libro": se conserva el modelo `Libro` y el archivo `libros.json` como nombre
  interno y se generaliza su contenido; el rótulo visible ("Libro" vs. "material") lo decide
  diseño (01-spec, "Supuestos").
- Un examen = una sola fuente; el set de varias imágenes cuenta como una fuente única; no se
  combinan tipos (01-spec, "Fuera de alcance").
- El texto literal de US-013 se toma textual de `01-spec.md` US-013 / RN-5; este documento no
  lo reproduce.

---

## Estado real del código (ancla de esta spec)

Relevado por lectura directa de los archivos, no de memoria.

- **`AutoExam/AutoExam.csproj`**: `net8.0-windows`, WPF, `SelfContained=true`,
  `PublishSingleFile=true`, `RuntimeIdentifier=win-x64`, `IncludeNativeLibrariesForSelfExtract=true`,
  `PublishTrimmed=false` (WPF no soporta trimming), `SatelliteResourceLanguages=es;en`,
  `<Version>1.0.3</Version>`. Paquetes: `PdfPig 0.1.15`, `WPF-UI 4.3.0`,
  `CommunityToolkit.Mvvm 8.4.0`, `AutoUpdater.NET.Official 1.9.3`. **No hay** paquete de
  parsing de Office ni de códecs de imagen.
- **`Services/PdfExtractorService.cs`** — hoy el **único** extractor. Clase concreta (sin
  interfaz), instanciada con `new` en `BibliotecaService`, en `AsistenteViewModel` /
  `BibliotecaViewModel` (por ctor) y dentro de `GeminiApiService.SubirPdfSiConvieneAsync`.
  Produce `ExtraccionResultado { List<FragmentoTexto> Fragmentos, List<ImagenExtraida> Imagenes
  (figuras), List<ImagenExtraida> PaginasEscaneadas (material que la IA lee), contadores,
  HuboMuestreo, HuboRecorte, TieneTexto, TienePaginasEscaneadas, TieneMaterial }`. Métodos:
  `ContarPaginasAsync`, `DetectarCapitulosAsync` (lee bookmarks/outline del PDF),
  `ExtraerAsync(rutaPdf, IReadOnlyList<RangoPaginas>, OpcionesExtraccion, IProgress<string>, ct)`,
  `RecortarAsync` (arma un PDF nuevo con el subconjunto de páginas para la Files API). Lee por
  bloques de páginas, **nunca materializa el documento entero**; muestrea si el alcance excede
  `MaxPaginasLeidas`.
- **`OpcionesExtraccion`**: `PaginasPorBloque`, `MaxPaginasLeidas=400`, `MaxCaracteres=90_000`,
  `ExtraerImagenes`, `MaxImagenes=12`, `MaxPaginasEscaneadas=10`, `LadoMaximoPaginaEscaneada=1600`,
  `MinLadoPaginaEscaneada=500`, `CarpetaImagenes`.
- **`ImagenExtraida`**: `{ Identificador, Ruta, MimeType, Pagina, Ancho, Alto, Etiqueta,
  YaPreparada }`.
- **`Services/ImagenUtil.cs`** — sólo WPF Imaging (`BitmapDecoder`, `TransformedBitmap`,
  `Jpeg/PngBitmapEncoder`), **sin `System.Drawing`, sin nativo extra**. `RedimensionarSiHaceFalta`
  (→ PNG), `PrepararParaLectura` (→ JPEG q85, escalado al lado máximo), `CargarDesdeArchivo`.
  `BitmapDecoder.Create` depende de los códecs WIC instalados en el SO: HEIC/HEIF **no** se
  decodifica sin la extensión HEIF del SO.
- **`Services/BibliotecaService.cs`**: `ObservableCollection<Libro> Libros`, `libros.json` +
  copia del PDF bajo `RutasApp.Biblioteca`. `AgregarLibroAsync(rutaOrigen, titulo, materia,
  modulos)` copia el archivo a `{Id}.pdf`, cuenta páginas; si el PDF no se puede leer
  (contraseña/dañado) borra la copia y lanza `InvalidOperationException` con la causa.
  `EliminarLibro` borra archivo + entrada. `RecuperarHuerfanosAsync` escanea `*.pdf`.
- **`Models/Libro.cs`**: `{ Id, Titulo, Materia, RutaArchivo, NombreArchivoOriginal,
  CantidadPaginas, FechaAgregado, List<Modulo> Modulos }`. `Modulo`: `{ Id, Nombre, DesdePagina,
  HastaPagina, [JsonIgnore] Seleccionado }`. Todo el modelo de alcance es **por páginas**.
- **`ViewModels/AsistenteViewModel.cs`** (página `"nuevo"` / "Nuevo examen"): asistente de 3
  pasos (Material / Alcance / Formato). `ConstruirRangos()` traduce módulos marcados + preset de
  rango + eje temático libre en `List<RangoPaginas>`. `DetectarCapitulosAsync` sólo tiene
  sentido para PDF con índice. En `GenerarAsync`: `string examenId = Guid.NewGuid()` se usa
  **sólo** para `RutasApp.CarpetaImagenesExamen(examenId)`; el `ExamenEnCurso` creado después
  recibe **su propio `Id`** distinto → hoy no queda persistido qué carpeta de imágenes
  corresponde a qué intento (hueco relevante para US-012).
- **`Services/GeminiApiService.cs`** + `SolicitudGeneracion`: la solicitud lleva `Fragmentos`,
  `Imagenes` (figuras) y `PaginasEscaneadas`. Las imágenes viajan como `inline_data
  { mime_type, data (base64) }`; topes por request: `MaxImagenesPorLote=5`, `MaxPaginasPorLote=4`,
  `MaxBytesImagen=3 MB`. El `foreach (var img in figuras.Concat(paginas))` preserva el orden.
  Sin texto extraíble, las páginas escaneadas son el material y el modelo les lee el texto.
  Endpoints: `generateContent`, Files API, `ListModels`. **Nada de Office en ninguna parte.**
- **`ViewModels/ExamenViewModel.cs`**: `Finalizar()` corrige con `EvaluadorUBA.Evaluar` y, sólo
  en `Ronda == 0`, registra un `ExamenRendido` vía `_sesion.RegistrarExamen`; las revanchas
  hacen `registro.Revanchas.Add(...)` + `_sesion.ActualizarExamen(registro)`.
  `MostrarResultados(ResultadoExamen)` fija `Nota` (int), `Aprobado`, `Condicion`,
  `ResumenResultado`, `DetalleRondas`, `Correccion`. `Examen.EsRevancha` = `Ronda > 0`.
  `ExamenEnCurso.Registro` apunta al `ExamenRendido` del intento original.
- **`Views/ExamenView.xaml`**: la vista Resultados es un `Grid` con `Visibility` atada a
  `EnResultados` — `Border` (estilo `Tarjeta`) con círculo de nota + `Condicion` +
  `ResumenResultado` + `DetalleRondas`, y debajo un `ScrollViewer` con la corrección pregunta
  por pregunta. El `UserControl` **no puede ser `Focusable=True`** (comentario en el XAML: rompe
  el `Click` por mouse de las opciones).
- **`Services/SesionUsuarioService.cs`**: `Config` (`AppConfig` / `config.json`), `Perfil`
  (`PerfilUsuario` / `perfil.json`), `ObservableCollection<ExamenRendido> Historial` (orden
  desc.), `MaxHistorial = 300`. Métodos: `RegistrarExamen`, `ActualizarExamen(examen)` — hace
  `FindIndex(e => e.Id == examen.Id)` y **no hace nada si es -1**, `BorrarHistorial()` (vacía
  todo). **No existe** un borrado individual. `RefrescarHistorial()` reconstruye la colección
  desde `Perfil.Historial`.
- **`Models/ExamenRendido.cs`**: `{ Id, Fecha, LibroId, LibroTitulo, Materia,
  AlcanceDescripcion, TotalPreguntas, Correctas, Incorrectas, Salteadas, PorcentajeAciertos,
  NotaUBA, Condicion, Aprobado, DuracionSegundos, List<RondaRevancha> Revanchas,
  CompletadoAl100 }`. **No** guarda datos de preguntas ni ruta/carpeta de imágenes.
- **`Models/PerfilUsuario.cs`**: las 6+ estadísticas agregadas (`TotalExamenes`, `PromedioNota`,
  `PromedioAciertos`, `Aprobados`, `Aplazos`, `MejorNota`, `TotalCorrectas`, `TotalPreguntas`,
  `TotalSalteadas`) son propiedades **calculadas sobre `Historial`** → se recalculan solas al
  cambiar la lista.
- **`Services/RutasApp.cs`**: raíz `%LOCALAPPDATA%\AppEstudioUBA` (o `AUTOEXAM_DATOS`).
  `Biblioteca`, `Imagenes`, `libros.json`, `perfil.json`, `config.json`, `errores.log`.
  `CarpetaImagenesExamen(examenId) → Imagenes\{examenId}`. `LimpiarImagenesAntiguas(7)` borra
  carpetas viejas. `RegistrarError` hace append best-effort, nunca lanza.
- **`Services/EvaluadorUBA.cs`**: escala 1–10, aprueba con 60%. `Nota 7 ⇔ ≥ 74%` de aciertos.
  `Nota` es `int`. Clase estática, intocable.
- **`Theme/Estilos.xaml`**: recursos de animación centralizados —
  `DuracionHover` (0.14 s), `DuracionPresion` (0.08 s), `DuracionTransicionSeccion` (0.22 s),
  `SuavizadoSalida` (`QuadraticEase EaseOut`). Estilos con `Storyboard` en
  `Trigger.EnterActions`/`ExitActions` sobre `Opacity` de una capa overlay ya tematizada +
  `ScaleTransform`, con guardia `MultiTrigger` `IsEnabled=True`: `Chip`, `ChipAccion`,
  `OpcionExamen`, `ItemNavegacion`, `ItemLibro`, `ZonaSoltar`, `BaldosaPregunta`. Regla del
  archivo: "ningún color literal vive en una vista".
- **`Behaviors/TransicionContenido.cs`**: propiedad adjunta `Activa` sobre el `ContentControl`
  del shell (`{Binding Pagina}` en `MainWindow.xaml`, y **sólo** ahí). Anima `Opacity` 0→1 +
  `TranslateTransform.Y` 10→0; lee `DuracionTransicionSeccion` / `SuavizadoSalida` por
  `TryFindResource` (fallback 220 ms). `BeginAnimation` reemplaza limpiamente el clock anterior.
- **`Behaviors/Presionable.cs`**: propiedad adjunta `EstaPresionado` para `ListBoxItem` (usada
  por `ItemLibro`).
- **`Services/DialogoService.cs` / `IDialogos`**: `Confirmar` / `Aviso` / `Error` (ventana
  propia temática), `ElegirPdf()` (`OpenFileDialog`, filtro `"*.pdf"`, `Multiselect=false`),
  `AbrirCarpeta`. Comando `SoltarAsync(string ruta)` en `BibliotecaViewModel` /
  `AsistenteViewModel` recibe **una sola** ruta.
- **No existe hoy** ningún manejo de "reducir movimiento" del SO, ni superficie de
  entrada animada en Resultados, ni animación de alta/baja en las listas de Historial/Libros.

---

## NFR (no funcionales aplicables)

| ID | Requisito | Umbral medible | US |
|---|---|---|---|
| NFR-37 | Rechazo de formato legacy / no soportado | Elegir o soltar `.doc` / `.xls` / `.ppt`, o cualquier extensión fuera de `{.pdf, .docx, .xlsx, .pptx, .jpg, .jpeg, .png, .heic, .heif}`, produce un mensaje con la causa en < 200 ms, sin red, y crea **0** fuentes/materiales | US-008, US-010 |
| NFR-38 | Sin límite propio de tamaño para Office | Un `.docx` / `.xlsx` / `.pptx` de cualquier cantidad de páginas / diapositivas / hojas / filas se procesa sin rechazo ni truncado impuesto por la app; el único recorte aplicable es `OpcionesExtraccion.MaxCaracteres` y la cuota de IA (RN-3, RN-8) — verificable con un archivo ≥ 500 páginas equivalentes | US-008 |
| NFR-39 | Extracción sin materializar el archivo entero | La extracción de cualquier formato procesa por unidad (bloque de páginas / hoja / lote de diapositivas) y **no** descomprime el contenedor OPC completo en memoria; el pico de RAM del proceso no crece de forma proporcional al tamaño del archivo — mismo criterio que ya cumple `PdfExtractorService` | US-008 |
| NFR-40 | Medida de tamaño por formato | Terminado el procesamiento, la fuente expone: Word → páginas (o "documento único" si el formato no la da), PowerPoint → diapositivas, Excel → hojas y filas, imágenes → cantidad; **0** fuentes sin ninguna medida | US-008 |
| NFR-41 | Aviso de fuente sin contenido | Office sin texto extraíble, o imágenes ilegibles/sin texto reconocible, produce un aviso explícito ("no se encontró contenido para generar preguntas") y se crean **0** exámenes o fuentes vacíos (RN-4) | US-008, US-010 |
| NFR-42 | Conversión HEIC/HEIF previa al envío | Toda imagen `.heic` / `.heif` se convierte a JPEG o PNG antes de construir el request; **0** bytes HEIC/HEIF viajan en `inline_data`; la conversión funciona **sin depender de un códec instalado en el SO destino** | US-010 |
| NFR-43 | Orden y límites del set de imágenes | El orden de envío de las imágenes es idéntico al orden en que se agregaron (100% de los casos); superado `MaxImagenesPorMaterial` o el lado/tamaño máximo por imagen, la app informa el límite concreto y no envía de más | US-010 |
| NFR-44 | Costo de las fuentes-imagen informado | En toda generación con fuente que viaja como imagen (fotos, Office sin texto), la UI informa antes o durante que consume más cuota y puede tardar más (RN-3) — mensaje presente en el 100% de esas generaciones | US-010 |
| NFR-45 | Duración de animación / transición | Cada transición de sección o de estado de las 8 superficies de RN-7 se completa en ≤ 250 ms; hover/press usan `DuracionHover` / `DuracionPresion`. Todo timing sale del recurso centralizado de `Theme/Estilos.xaml` — **0** valores hardcodeados fuera de esos recursos | US-011 |
| NFR-46 | Animación no bloqueante ni acumulable | Ninguna de las 8 superficies bloquea la interacción del usuario durante la animación; ante ≥ 5 disparos encadenados en < 2 s, cada animación reemplaza limpiamente a la anterior — **0** elementos a medio animar, **0** excepciones | US-011 |
| NFR-47 | Respeto de "reducir movimiento" del SO | Con "mostrar animaciones en Windows" desactivado (`SystemParameters.ClientAreaAnimation == false`), las animaciones no esenciales de las 8 superficies se acortan o desactivan, y el comportamiento **funcional** de cada superficie es idéntico con y sin animación | US-011 |
| NFR-48 | Paridad funcional del pulido | Cada superficie pulida conserva su comportamiento funcional (no visual) bit-idéntico al previo — suite de regresión, **0** desvíos (criterio "sólo cambia la animación" de US-011) | US-011 |
| NFR-49 | Borrado individual persistente y recalculado | Borrar un examen (con confirmación) lo quita de `perfil.json`, recalcula las 6 estadísticas agregadas sin él, y el examen sigue ausente tras reiniciar la app — 100% de los casos salvo error de escritura ya cubierto por el manejo best-effort existente | US-012 |
| NFR-50 | Limpieza de imágenes del examen borrado | Al borrar un examen con imágenes asociadas, su carpeta `Imagenes\{id}` se elimina best-effort (fallo → `RutasApp.RegistrarError`, nunca una excepción que corte el borrado); **0** carpetas huérfanas de ese examen | US-012 |
| NFR-51 | Revancha en curso al borrar el original | Si hay una revancha en curso del examen que se borra, la confirmación lo advierte; al confirmar, la revancha en curso se descarta sin registrarse, y ninguna ronda posterior recrea el registro borrado ni lanza error | US-012 |
| NFR-52 | Mensaje US-013: condición exacta y no ocultable | El texto literal aparece **si y sólo si** `Nota ≥ 7` **y** el resultado es del intento original (no revancha); existen **0** caminos (código, `AppConfig`, recurso de tema, UI) para ocultarlo o editarlo; forma parte del binario distribuido por la actualización automática (RN-5) | US-013 |
| NFR-53 | Mensaje US-013: sin regresión de Resultados | Con el mensaje visible, el resto de la pantalla de Resultados (nota, resumen, corrección pregunta por pregunta, botón de revancha) se comporta igual que sin él — suite de regresión sobre `ExamenView` Resultados | US-013 |

---

## Entidades de datos y relaciones

| Entidad | Campos relevantes (existentes + nuevos) | Persistencia | US origen |
|---|---|---|---|
| `Fuente` / `Material` (generalización de `Libro`) | + `Tipo` (`Pdf` / `Word` / `Excel` / `PowerPoint` / `SetImagenes`); + soporte de **1 archivo o N imágenes ordenadas** (`RutaArchivo` → lista); + `MedidaTamanio` (texto libre: "34 diapositivas" / "5 hojas · ~1.2k filas" / "8 imágenes" / "documento único"). `Modulos` se puebla **sólo** para PDF con índice | `libros.json` vía `JsonStore` + copia interna del/los archivo(s) bajo `RutasApp.Biblioteca` | US-008, US-009, US-010 |
| `ExtraccionResultado` (existe — se reutiliza tal cual) | `Fragmentos`, `Imagenes` (figuras), `PaginasEscaneadas` (material que la IA lee), `TieneMaterial` — ya es agnóstico de formato | efímero (en memoria durante la generación) | US-008, US-010 |
| `RecorteFuente` (generaliza `RangoPaginas`) | subconjunto de estructura (páginas / diapositivas / hojas / secciones de Word) + `TemaLibre`; vacío ⇒ material completo. Para PDF sigue degradando a `List<RangoPaginas>` (camino de la Files API sin cambios) | efímero | US-009 |
| `ExamenRendido` (existe — se extiende) | + enlace a su carpeta de imágenes (nuevo `CarpetaImagenesId`, **o** hacer que `Id` sea el nombre de la carpeta) — hoy no existe y sin él US-012 no puede limpiar las imágenes | `perfil.json` vía `JsonStore` | US-012 |
| `AppConfig` (existe — se extiende) | + `MaxImagenesPorMaterial` (límite de US-010). **Nada nuevo para US-013**: sin config por diseño (RN-5) | `config.json` vía `JsonStore` | US-010 |
| Literal de felicitación (US-013) | constante de código — **no** recurso de tema intercambiable, **no** `AppConfig` | binario / control de versiones | US-013 |

Sin cambios de esquema en `Pregunta`, `RondaRevancha`, `PerfilUsuario` (los agregados se
recalculan desde la lista). US-011 no introduce entidades (casilla vacía intencional).

---

## Contratos de integración de alto nivel

### US-008 / US-009 / US-010 — Pipeline de extracción multi-formato (contrato interno)

- **`IExtractorContenido`** (nuevo, interno):
  - `bool Soporta(string extension)`
  - `Task<MedidaFuente> MedirAsync(rutaOrRutas, ct)` → medida por formato (NFR-40)
  - `Task<ExtraccionResultado> ExtraerAsync(rutaOrRutas, RecorteFuente, OpcionesExtraccion, IProgress<string>, ct)`
- Una implementación por familia: `PdfExtractor` (envuelve el `PdfExtractorService` actual **sin
  reescribirlo**), `OfficeExtractor` (`.docx` / `.xlsx` / `.pptx`), `ImagenExtractor`
  (`.jpg` / `.jpeg` / `.png` / `.heic` / `.heif`).
- **Selección y rechazo**: un factory por extensión. Extensión desconocida o `.doc` / `.xls` /
  `.ppt` ⇒ `FormatoNoSoportadoException` con mensaje que nombra los formatos admitidos y sugiere
  reguardar en el formato actual. Archivo protegido con contraseña / dañado ⇒
  `FuenteIlegibleException` con la causa. El llamador traduce ambas a aviso y **no crea la
  fuente** — mismo patrón que hoy `BibliotecaService.AgregarLibroAsync` (try/catch, borra la
  copia, lanza con causa).
- `ExtraccionResultado` y `OpcionesExtraccion` se reutilizan. `RangoPaginas` sigue siendo el
  tipo interno del `PdfExtractor` (y de `SolicitudGeneracion.Rangos` para la Files API);
  `RecorteFuente` lo produce sólo para el camino PDF.
- **NFR-39**: `OfficeExtractor` lee por unidad (hoja / diapositiva / cuerpo de Word), no
  descomprime el OPC entero en RAM.

### US-010 — Contrato hacia el servicio de IA (visión)

- **Sin endpoint nuevo.** Las fotos de apuntes viajan por el canal que ya existe:
  `SolicitudGeneracion.PaginasEscaneadas` (`List<ImagenExtraida>`), que `GeminiApiService` ya
  adjunta como `inline_data { mime_type, data(base64) }` y sobre las que el modelo lee texto
  (hoy se usa para páginas de PDF escaneado). `ImagenExtractor` (fotos) y `OfficeExtractor` sin
  texto llenan **esa misma lista**.
- **Pre-proceso obligatorio antes de armar el request**: HEIC/HEIF → JPEG/PNG (ver
  Restricciones); reescalado al lado máximo (`LadoMaximoPaginaEscaneada`, ~1600 px) + JPEG q85
  (reusa `ImagenUtil.PrepararParaLectura`); `YaPreparada = true`. Topes ya existentes por
  request: `MaxPaginasPorLote = 4`, `MaxBytesImagen = 3 MB`.
- **Orden**: la lista se arma en el orden de alta del usuario y se respeta en el envío (el
  `foreach (... figuras.Concat(paginas))` ya preserva orden).
- **Resultado parcial**: si algunas fotos no aportan contenido, se genera con las que sí y se
  informa la limitación; si ninguna, no se crea examen (RN-4) — misma rama que hoy
  `!extraccion.TieneMaterial`.
- **Costo (NFR-44)**: reusar los `progreso?.Report(...)` que ya avisan "se mandan N páginas como
  imagen… puede tardar más y consumir más cuota".

### US-011 — Animaciones (contrato interno)

- Sin superficie nueva hacia otros módulos. Se pulen `ControlTemplate`/behaviors existentes;
  ninguna vista cambia sus bindings. Timing/easing: **únicamente** los recursos de
  `Theme/Estilos.xaml`. Si una superficie de RN-7 hoy no lee de ahí (p. ej. una entrada de
  Resultados nueva), se agrega el recurso, no un literal local.
- **Mapa superficie (RN-7) → punto de código**:

  | Superficie RN-7 | Dónde |
  |---|---|
  | Transición entre secciones de la navegación | `Behaviors/TransicionContenido.cs` (ContentControl del shell) |
  | Hover y pulsado de botones y chips | `Theme/Estilos.xaml`: `Chip`, `ChipAccion` (+ `ui:Button` de WPF-UI) |
  | Riel de pasos del asistente (línea de avance) | `AsistenteView.xaml` + `PasoAsistente` (`EsActual` / `Completado`) |
  | Baldosas del navegador de preguntas | `Theme/Estilos.xaml`: `BaldosaPregunta` + `NavegadorItem` (`EstadoAPincel`), cambio de estado al responder |
  | Entrada de la pantalla de Resultados | `Views/ExamenView.xaml`, `Grid` con `Visibility` = `EnResultados` — **hoy sin animación** |
  | Apertura/cierre de avisos (InfoBar) | `Mensaje` / `Severidad` de los ViewModels, renderizados como `ui:InfoBar` en las vistas |
  | Anillos de progreso | `ui:ProgressRing` (generación de examen) |
  | Alta/baja de ítems en Historial y Libros | `ListBox` con `ItemContainerStyle="ItemLibro"` — **hoy sin transición de add/remove** |

- **"Reducir movimiento"**: una única compuerta (helper `Animaciones.Reducidas`, lee
  `SystemParameters.ClientAreaAnimation` y opcionalmente `RenderCapability.Tier == 0`),
  consultada por `TransicionContenido` y por los estilos (propiedad adjunta usada en un
  `MultiTrigger`, o condicionando el `BeginStoryboard`). **No** se agrega opción en Ajustes (el
  spec dice "del sistema operativo").
- Restricción vigente: no se anima `Brush`/`Color` directo (mutaría el recurso de tema
  compartido); sólo `Opacity` de capa overlay ya tematizada o `ScaleTransform`.

### US-012 — Borrado individual del historial (contrato interno)

- **`SesionUsuarioService.BorrarExamen(string id)`**: `Perfil.Historial.RemoveAll(e => e.Id == id)`
  → `GuardarPerfil()` → `RefrescarHistorial()`. Las estadísticas de `PerfilUsuario` son
  calculadas → se recalculan solas.
- **`HistorialViewModel`**: nuevo `BorrarExamenCommand(ExamenRendido)` → `IDialogos.Confirmar(...)`
  → `_sesion.BorrarExamen(e.Id)` + limpieza de `Imagenes\{id}` (best-effort, NFR-50) →
  `Refrescar()`. El comando global `BorrarCommand` ("Borrar historial") **no cambia**. El estado
  vacío ("Todavía no rendiste ningún examen") ya está cubierto por `HayExamenes`.
- **Enlace examen ↔ imágenes**: hoy `AsistenteViewModel.GenerarAsync` usa un `examenId` propio
  para la carpeta de imágenes que **no** queda en `ExamenRendido`. Cerrar el hueco: setear
  `ExamenEnCurso.Id = examenId` (así `registro.Id` nombra la carpeta) **o** agregar
  `ExamenRendido.CarpetaImagenesId`. Decisión de arquitecto-tecnico; sin esto US-012 no cumple
  "se limpian sus archivos de imágenes".
- **Revancha en curso**: el shell ya cablea páginas por eventos (`ShellViewModel`).
  `HistorialViewModel` expone `Func<string,bool>? HayRevanchaEnCursoDe` y emite un evento
  `ExamenBorrado(id)`. Antes de borrar, si `HayRevanchaEnCursoDe(id)` la confirmación cambia el
  texto para advertir la revancha en curso. Al confirmar, `ExamenViewModel` responde al evento:
  si `HayIntentoAbierto && Examen.Registro?.Id == id`, descarta el intento en curso
  (`Examen.Registro = null` y `Cerrar()` si `EsRevancha`) **sin registrar**.
- **Revancha que termina con el original ya borrado**: `SesionUsuarioService.ActualizarExamen`
  ya hace `FindIndex(e => e.Id == examen.Id)` y **no hace nada si es -1** → no recrea el registro
  ni lanza error. Sólo hay que verificar que ninguna rama de `ExamenViewModel.Finalizar`
  asuma que el registro sigue existiendo.

### US-013 — Mensaje de felicitación (contrato interno)

- **`ExamenViewModel`**: `MostrarFelicitacion` (bool, fijado en `MostrarResultados`) =
  `EnResultados && !Examen.EsRevancha && Nota >= 7`. `MensajeFelicitacion` = **constante literal
  en mayúsculas** (el texto exacto está en `01-spec.md` US-013).
- **`Views/ExamenView.xaml`** (vista Resultados): `TextBlock`/`Border` destacado dentro o encima
  del `Border` de encabezado, `Visibility` atada a `MostrarFelicitacion` (`BoolToVis`),
  `FontWeight="Bold"`, texto en mayúsculas, color por `DynamicResource` ya tematizado
  (p. ej. `PincelMarca` / `PincelAcierto`).
- **Sin** `AppConfig`, **sin** recurso de tema que se pueda intercambiar para ocultarlo, **sin**
  binding a nada configurable (RN-5). Se distribuye en el release por el pipeline de
  actualización automática ya existente.

---

## Restricciones técnicas conocidas

- **Stack fijo, no evaluable**: .NET 8 / WPF / WPF-UI 4.3.0 / CommunityToolkit.Mvvm 8.4.0 /
  PdfPig 0.1.15 / Gemini (vía `HttpClient`) / AutoUpdater.NET.Official 1.9.3. Publicación
  self-contained single-file `win-x64`, `PublishTrimmed=false`,
  `IncludeNativeLibrariesForSelfExtract=true`, `SatelliteResourceLanguages=es;en`.
  arquitecto-tecnico decide *cómo* implementar dentro de este stack, no si cambiarlo.
- **Parsing de Office**: no hay parser hoy. Opciones (sin decisión):
  (a) `DocumentFormat.OpenXml` — Microsoft, 100% managed, sin nativo, canónica;
  (b) sin dependencia nueva: `.docx` / `.xlsx` / `.pptx` son contenedores OPC (ZIP);
  `System.IO.Compression` + `System.Xml` ya están en el framework → leer `word/document.xml`,
  `xl/sharedStrings.xml` + `xl/worksheets/sheet*.xml`, `ppt/slides/slide*.xml` — encaja con
  "código simple";
  (c) terceros (NPOI, etc.) — más peso/licencia.
  `.doc` / `.xls` / `.ppt` son OLE2 compound binary, formato distinto: **se rechazan, no
  requieren parser** (RN-8).
- **HEIC/HEIF** — principal incógnita técnica de US-010: WPF Imaging (WIC) sólo decodifica HEIC
  con las "HEIF Image Extensions" + códec HEVC instalados en el SO (Microsoft Store); **no** se
  puede asumir presente en las PCs destino ni empaquetar en el `.exe` self-contained. Opciones a
  evaluar (sin decisión): decodificador embebible que no dependa del códec del SO — `libheif` /
  `libde265` nativo vía `IncludeNativeLibrariesForSelfExtract`, o un wrapper managed
  (`LibHeifSharp`, `Magick.NET`, `SkiaSharp` según build). Requisitos duros: convertir **sin**
  códec del SO, viajar dentro del single-file exe, y **preferir no** reintroducir
  `System.Drawing` (el `.csproj` lo deshabilita a propósito).
- `ImagenUtil` usa sólo WPF Imaging (sin `System.Drawing`, sin nativo extra) — el pre-proceso de
  imágenes de US-010 sigue ese criterio salvo lo mínimo imprescindible para HEIC.
- `PdfExtractorService` es clase concreta instanciada con `new` en varios puntos
  (`BibliotecaService`, ViewModels por ctor, `GeminiApiService`). Introducir `IExtractorContenido`
  no puede romper esos puntos: el PDF conserva su implementación, sólo se la envuelve.
- `RangoPaginas` está acoplado a páginas y lo consumen `PdfExtractorService.ExtraerAsync` /
  `RecortarAsync` y `SolicitudGeneracion.Rangos` (recorte de la Files API). Para fuentes sin
  páginas, `RecorteFuente` degrada a "material completo + eje temático" sin romper el camino de
  Files API del PDF.
- **Persistencia**: reusar `JsonStore` + `RutasApp` (`libros.json`, `perfil.json`, `config.json`
  bajo `%LOCALAPPDATA%\AppEstudioUBA`, redirigible por `AUTOEXAM_DATOS` en tests). No un segundo
  mecanismo. Fallos de guardado: best-effort + `RutasApp.RegistrarError`, nunca una excepción
  que tumbe arranque/cierre (mismo patrón que `ShellViewModel.Cerrar`).
- **`IDialogos.ElegirPdf()`** (`OpenFileDialog`, filtro `"*.pdf"`, `Multiselect=false`) es la
  superficie que US-008/US-010 amplían: nuevo método (o cambio de firma) para multi-formato y
  multi-selección (set de imágenes). El resto de `IDialogos` no cambia. La zona de arrastre
  (`ZonaSoltar` + `SoltarAsync(string ruta)`) hoy recibe **una** ruta — debe aceptar varias
  para el set de imágenes.
- **`Theme/Estilos.xaml`**: "ningún color literal vive en una vista"; timing de animación
  centralizado. US-011 **no** agrega animaciones a superficies fuera de RN-7 (lo dice RN-7) y
  **no** anima `Brush`/`Color` directo — sólo `Opacity` de capa overlay ya tematizada o
  `ScaleTransform`.
- `TransicionContenido.Activa` está **sólo** en el `ContentControl` del shell, nunca en
  `ExamenView.xaml` (la navegación entre preguntas es por `Visibility`). US-011 no cambia ese
  alcance.
- `ExamenView.xaml`: el `UserControl` no puede ser `Focusable=True` (rompe el `Click` por mouse
  de las opciones) — restricción documentada en el XAML, a respetar si US-011/US-013 tocan ese
  árbol de foco.
- **US-013**: el literal se distribuye en el release por la actualización automática ya
  existente (pipeline / `update.xml` / `AutoUpdater.NET`) — no hay, ni se agrega, camino para
  ocultarlo (RN-5). `EvaluadorUBA` intocable: `Nota` es `int`, `Nota >= 7` es comparación exacta
  (= ≥ 74% de aciertos, RN-1).
- Corrección y flujo de examen no cambian: US-011/US-013 son visuales; US-012 sólo toca la
  persistencia del historial. No se edita ni reabre un examen ya rendido (fuera de alcance).

### Restricción transversal de estilo de código

Igual que en incrementos anteriores: extender lo que existe antes que sumar capas; sin
librerías de terceros nuevas salvo que HEIC lo obligue (única excepción candidata, y a
justificar); código imperativo directo con nombres y comentarios de "por qué"; nada de
abstracciones "por si en el futuro" (no un motor de extractores plugin-eable si alcanza con un
factory por extensión).

---

## Criterios de aceptación técnicos

**US-008**
- **AC-T40**: el selector y la zona de arrastre aceptan `.docx` / `.xlsx` / `.pptx` junto a
  `.pdf`; generar sobre una de esas fuentes produce preguntas con contenido de ese archivo y se
  corrige con `EvaluadorUBA` — 01-spec US-008, criterios 1-2.
- **AC-T41**: una fuente Office procesada muestra su medida por formato (páginas / diapositivas /
  hojas y filas) o "documento único"; sin medida = falla — criterio 3; ver NFR-40.
- **AC-T42**: un `.docx` / `.xlsx` / `.pptx` de cualquier tamaño no se rechaza ni se trunca por
  un límite propio de la app — criterio 4; ver NFR-38.
- **AC-T43**: `.doc` / `.xls` / `.ppt` → rechazo con mensaje que nombra los formatos admitidos y
  sugiere reguardar; archivo protegido / dañado / formato no soportado → rechazo con la causa y
  **0** fuentes vacías creadas — criterios 5-6; ver NFR-37, NFR-41.
- **AC-T44**: Office sin texto extraíble → aviso "no se encontró contenido para generar
  preguntas" y **0** exámenes vacíos — criterio 7; ver NFR-41.

**US-009**
- **AC-T45**: para fuente Office o de imágenes, el paso Alcance no ofrece detección de capítulos
  (o informa que esa fuente no tiene capítulos) y permite generar sobre todo el material —
  criterio 1.
- **AC-T46**: el campo de eje temático libre sigue disponible y acota las preguntas para
  cualquier tipo de fuente — criterio 2.
- **AC-T47**: sin ningún recorte marcado, el examen cubre el material completo de la fuente —
  criterio 4.

**US-010**
- **AC-T48**: el selector y la zona de arrastre aceptan `.jpg` / `.jpeg` / `.png` / `.heic` /
  `.heif` y **selección múltiple** para un mismo material — criterio 1.
- **AC-T49**: una imagen `.heic` / `.heif` se convierte a un formato soportado antes del envío a
  la IA y el examen se genera igual que con `.jpg` / `.png`; **0** bytes HEIC/HEIF en el request
  — criterio 2; ver NFR-42.
- **AC-T50**: fotos manuscritas legibles → las preguntas reflejan ese contenido; algunas fotos
  ilegibles → genera con el resto e informa la limitación; todas ilegibles → no crea examen —
  criterios 3-4; ver NFR-41, NFR-44.
- **AC-T51**: el orden de las imágenes en el material = orden de alta; superado el máximo de
  imágenes por material o el tamaño/resolución por imagen → aviso con el límite concreto —
  criterios 5-6; ver NFR-43.

**US-011**
- **AC-T52**: cada una de las 8 superficies de RN-7 pasa el checklist binario (usa los
  parámetros centralizados de duración/suavizado de `Theme/Estilos.xaml` + la transición se
  completa sin cortes ni parpadeo + no bloquea la interacción); el sign-off exige "pasa" en las
  8 — 01-spec US-011, criterio 1; ver NFR-45, NFR-48.
- **AC-T53**: con "reducir movimiento" del SO activo, las animaciones no esenciales de esas
  superficies se acortan o desactivan y el comportamiento funcional es idéntico — criterios 2
  y 5; ver NFR-47.
- **AC-T54**: ninguna transición de sección o de estado dura más de ~250 ms ni bloquea la
  interacción del usuario — criterio 3; ver NFR-45, NFR-46.
- **AC-T55**: navegando rápido entre secciones o entre preguntas, las transiciones encadenadas
  no se acumulan ni dejan elementos a medio animar — criterio 4; ver NFR-46.

**US-012**
- **AC-T56**: cada ítem del Historial tiene una acción visible para borrar ese examen; borrar
  pide confirmación y sólo elimina si se confirma (si se cancela, nada cambia) — criterios 1-2.
- **AC-T57**: tras borrar, el examen no aparece, las estadísticas agregadas (total rendidos,
  promedio, aciertos, mejor nota, aprobados, aplazos) se recalculan sin él, y sigue ausente tras
  reiniciar la app — criterios 3-4; ver NFR-49.
- **AC-T58**: borrar un examen con imágenes asociadas elimina su carpeta `Imagenes\{id}`; borrar
  el último deja el estado vacío ("Todavía no rendiste ningún examen"); la acción global "Borrar
  historial" sigue disponible y sin cambios — criterios 5-7; ver NFR-50.
- **AC-T59**: borrar el examen original de una revancha en curso → la confirmación advierte la
  revancha en curso; al confirmar, la revancha se descarta sin registrarse; una revancha que
  termina con su original ya borrado no recrea el registro ni muestra error — criterios 8-9;
  ver NFR-51.

**US-013**
- **AC-T60**: con nota UBA ≥ 7 en el intento original, Resultados muestra, destacado y en
  mayúsculas, el texto literal exacto de 01-spec US-013 — criterio 1; ver NFR-52.
- **AC-T61**: con nota ≤ 6, o cuando el resultado corresponde a una ronda de revancha, el
  mensaje no aparece — criterios 2-3; ver NFR-52.
- **AC-T62**: con el mensaje visible, el resto de la pantalla de Resultados (corrección pregunta
  por pregunta, revancha, resumen) funciona igual que hoy — criterio 4; ver NFR-53.
- **AC-T63**: no existe opción, flag ni configuración (código, `AppConfig` o UI) para ocultar o
  editar el mensaje; forma parte del release distribuido por la actualización automática —
  criterio 5, RN-5; ver NFR-52.

---

## Definition of Done de esta spec

- Todo NFR tiene umbral medible: cumplido (tabla NFR-37 a NFR-53, columna "Umbral medible").
- Toda entidad de datos tiene US-XXX de origen: cumplido (tabla de entidades, columna "US
  origen"); US-011 sin entidades, documentado explícitamente.

Sugerencia: antes de que arquitecto-tecnico cierre `03-architecture.md`, resolver tres puntos
que condicionan el diseño y pueden forzar revisar esta spec — (1) decodificación HEIC/HEIF
embebible sin códec del SO (incógnita bloqueante de US-010), (2) parser de Office
(`DocumentFormat.OpenXml` vs. lectura ZIP+XML sin dependencia nueva), (3) cómo se ancla cada
`ExamenRendido` a su carpeta de imágenes para poder cumplir US-012.
