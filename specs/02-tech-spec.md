# 02 — Tech Spec: Publicación automática + comodidad de interfaz

Traduce `specs/01-spec.md` (US-001 a US-005) a especificación técnica. Sin decisión de stack
(la toma arquitecto-tecnico); el stack y las convenciones actuales del repo son restricción
dura, no punto de partida a evaluar (ver "Restricciones técnicas").

## Estado real del código (ancla de esta spec)

Relevado directo del repo, no de memoria:

- `AutoExam/AutoExam.csproj`: `net8.0-windows`, WPF, `SelfContained=true`,
  `PublishSingleFile=true`, `RuntimeIdentifier=win-x64`, `<Version>1.0.2</Version>`.
  Paquetes: `AutoUpdater.NET.Official 1.9.3`, `CommunityToolkit.Mvvm 8.4.0`, `PdfPig 0.1.15`,
  `WPF-UI 4.3.0`.
- `publicar.ps1` (raíz del repo): script de 2 fases —
  fase preparación (`.\publicar.ps1`): `dotnet publish` Release, compara `FileVersion` del
  `.exe` compilado contra `<Version>` del `.csproj` (Major/Minor/Build), empaqueta un ZIP
  (`AutoExam-v{version}.zip`, un único `.exe` adentro, nunca suelto), abre el navegador en
  `github.com/.../releases/new`;
  fase cierre (`.\publicar.ps1 -Publicar`): HEAD/GET a la URL del Release, si no da 200 aborta
  sin tocar nada, si da 200 reescribe `update.xml` (regex sobre `<version>`, URL de descarga,
  changelog) y hace `git add/commit/push` a `main` sobre `update.xml` únicamente.
- `update.xml` (raíz del repo, en `main`): manifiesto leído por `AutoUpdater.NET` desde
  `raw.githubusercontent.com/MatiVillalba015/AutoExam/main/update.xml`. Contiene
  `<version>`, `<url>` (Release asset), `<changelog>` (tag), `<mandatory>false</mandatory>`
  (a propósito — comentario en el archivo explica el riesgo de `true`).
- `Services/ActualizacionService.cs`: envuelve `AutoUpdaterDotNET`. Compara
  `AutoUpdater.InstalledVersion` (del ensamblado) contra `update.xml`. Detección de bucle:
  `MaxIntentosPorVersion = 2`, contador en `AppConfig.IntentosDeActualizacion` /
  `UltimaVersionIntentada`, persistido vía `SesionUsuarioService`. `PaqueteDisponible()` hace
  HEAD (fallback GET Range 0-0) contra la URL del manifiesto antes de ofrecer la actualización;
  ante fallo de red devuelve `true` (no bloquea por desconexión pasajera). Usa
  `MessageBox.Show` propio (`Avisar(...)`) para sus avisos — está fuera de alcance por 01-spec
  ("Fuera de alcance": ventana de AutoUpdater.NET es de terceros).
- `Services/DialogoService.cs`: implementa `IDialogos` (`Confirmar`, `Aviso`, `Error`,
  `ElegirPdf`, `AbrirCarpeta`) envolviendo `MessageBox.Show` nativo. Esta es la superficie
  exacta que US-002 tiene que reemplazar — la interfaz `IDialogos` ya existe y ya se inyecta en
  los ViewModels (ver `ShellViewModel`), por lo que el contrato consumido por el resto de la
  app no cambia; cambia la implementación.
- `MainWindow.xaml` / `MainWindow.xaml.cs`: `ui:FluentWindow` (WPF-UI), `Width=1240 Height=820`,
  `MinWidth=980 MinHeight=660`, `WindowStartupLocation=CenterScreen`. Eventos `Loaded` →
  `Ventana_Loaded` (llama `ShellViewModel.IniciarAsync()`) y `Closing` → `Ventana_Closing`
  (llama `Vm.PuedeCerrar()` / `Vm.Cerrar()`). No hay hoy ninguna lectura/escritura de
  tamaño/posición/estado de ventana — US-003 es funcionalidad nueva, no fix de una existente.
  `Ventana_Loaded` también usa `MessageBox.Show` directo (fuera del `IDialogos`) para el error
  de inicialización — inconsistencia preexistente a evaluar si entra en US-002.
- `ShellViewModel.cs`: `Paginas` es un `ObservableCollection<PaginaViewModel>` con orden fijo
  `Libros, Asistente, Examen, Historial, Ajustes` — "Asistente" es el ViewModel de la página
  "Nuevo examen" nombrada en US-004. Navegación ya centralizada en `IrA(string clave)`
  (`[RelayCommand]`), consumida hoy solo por el `RadioButton` del riel lateral
  (`CommandParameter="{Binding Clave}"`). No hay hoy ningún `KeyBinding`/atajo a nivel de
  `Window` para navegar entre páginas.
- `Views/ExamenView.xaml`: ya tiene `UserControl.InputBindings` con `KeyBinding` para
  `D1-D4`/`NumPad1-4`/`A-D` (responder), `Right`/`Enter` (siguiente), `Left` (anterior), `S`
  (saltear). El `UserControl` está deliberadamente `Focusable` implícito en `false` (comentario
  en el XAML: con `Focusable="True"` el foco rebota y `ButtonBase` deja de convertir `MouseUp`
  en `Click`) — restricción dura a respetar si US-004 toca este árbol de foco. Estos atajos son
  los que US-004 no puede interferir ni reemplazar.
- `Services/SesionUsuarioService.cs`: `Cargar()`/`GuardarConfig()`/`GuardarPerfil()` vía
  `JsonStore` sobre `AppConfig` (`config.json`) y `PerfilUsuario` (`perfil.json`, historial de
  `ExamenRendido`, tope 300 registros). `AppConfig` ya contiene, entre otros,
  `ApiKey`, `Modelo`, `PreguntasPorLote`, `PaginasPorBloque`, `MaxCaracteresContexto`,
  `MaxImagenesPorExamen`, `TemaOscuro`, `IntentosDeActualizacion`, `UltimaVersionIntentada`. Es
  el lugar natural para las preferencias nuevas de US-003 (ventana) y US-005 (tamaño de texto).
- `Services/RutasApp.cs`: raíz de datos `%LOCALAPPDATA%\AppEstudioUBA` (o `AUTOEXAM_DATOS` si
  está seteada, usado por tests). `ArchivoConfig = config.json`, `ArchivoLog = errores.log`
  (`RegistrarError` hace append best-effort, nunca lanza).
- `Services/TemaService.cs`: intercambia `Theme/Tokens.Oscuro.xaml` / `Theme/Tokens.Claro.xaml`
  como único `ResourceDictionary` con prefijo `Tokens.` en `Application.Resources
  .MergedDictionaries` (se remueve el viejo antes de insertar el nuevo, nunca se acumulan). Los
  dos diccionarios exponen exactamente las mismas claves (`PincelFondo`, `PincelTarjeta`,
  `PincelMarca`, `PincelError...`, `SombraTarjeta`, etc.), consumidas con `DynamicResource` —
  este es el mecanismo que cualquier diálogo nuevo de US-002 tiene que heredar para verse
  correcto en ambos temas sin código propio de tematizado.
- `Theme/Estilos.xaml`: define `Tarjeta`/`TarjetaPlana`, tipografía (`TxtTitulo`, `TxtSeccion`,
  `TxtCuerpo`, `TxtSuave`, `TxtTenue`, `TxtRotulo`, `TxtDato`), `Chip`/`ChipAccion`,
  `OpcionExamen`, `ItemNavegacion`, `ItemLibro`, `BaldosaPregunta`. Regla explícita del archivo:
  "ningún color literal vive en una vista" — un diálogo nuevo de US-002 reutiliza estos
  estilos/`DynamicResource`, no define paleta propia.
- No existe hoy carpeta `.github/workflows/` en el repo — US-001 la crea desde cero, no
  modifica un pipeline existente.

## NFR (no funcionales aplicables)

| ID | Requisito | Umbral medible | US |
|---|---|---|---|
| NFR-01 | Duración del pipeline de publicación automática | De push con `<Version>` subida a Release publicado y `update.xml` actualizado: p95 < 10 min | US-001 |
| NFR-02 | No apagar el pipeline sin visibilidad | Un fallo en cualquier paso (build, verificación de versión, verificación HTTP del paquete) deja el run marcado como fallido, consultable sin acceso a la PC del desarrollador, dentro de los 60 s de ocurrido | US-001 |
| NFR-03 | Ausencia de falso positivo de publicación | 0 Releases creados ni `update.xml` modificado en pushes donde `<Version>` no sube — verificable por conteo exacto en un set de pushes de prueba (no es "debe ser confiable", es condición binaria por push) | US-001 |
| NFR-04 | Orden de publicación no negociable | `update.xml` nunca se escribe si el Release/paquete no devolvió HTTP 200 en la verificación previa — 0 excepciones, mismo criterio que ya aplica `publicar.ps1 -Publicar` | US-001 |
| NFR-05 | Apertura de diálogo temático | Confirmación/aviso/error propio abre en < 150 ms percibidos desde el disparo del evento (sin esperar red) | US-002 |
| NFR-06 | Restauración de geometría de ventana | Al abrir con preferencia guardada y monitor disponible, la ventana queda con tamaño/posición/estado idénticos a los guardados en el 100% de los casos (no "aproximado") | US-003 |
| NFR-07 | Recuperación ante monitor ausente | Si la posición guardada cae fuera de todas las áreas de trabajo visibles, la ventana aparece centrada y 100% dentro del área de trabajo de la pantalla principal, sin excepción no controlada | US-003 |
| NFR-08 | Latencia de navegación por atajo | De la pulsación del atajo al cambio de `Pagina` visible: < 100 ms (sin llamadas de red de por medio; `Onboarding`/verificaciones asíncronas quedan fuera de esta medición) | US-004 |
| NFR-09 | No interferencia con atajos de examen | Los `KeyBinding` de `ExamenView.xaml` (D1-D4, NumPad1-4, A-D, Right, Enter, Left, S) producen exactamente el mismo resultado antes y después de sumar atajos de navegación global — verificable como suite de regresión, 0 desvíos | US-004 |
| NFR-10 | Sin disparo accidental en campo de texto | Con foco en cualquier `TextBox`/campo editable (eje temático, API Key, etc.), tipear las teclas usadas como atajo de navegación no cambia de página — 0 falsos positivos en los campos editables existentes | US-004 |
| NFR-11 | Aplicación de tamaño de texto | Cambiar el tamaño de texto en el examen se refleja en pregunta y opciones en el mismo frame de interacción (sin flicker perceptible ni reflow que dispare scroll horizontal) | US-005 |
| NFR-12 | Persistencia de preferencias nuevas | Preferencia de tamaño de texto (US-005) y de geometría de ventana (US-003) sobreviven a cierre/apertura de la app en el 100% de los casos salvo error de escritura a disco ya cubierto por el manejo best-effort existente (`RutasApp.RegistrarError`) | US-003, US-005 |
| NFR-13 | No regresión del cliente de actualización | `ActualizacionService` (detección de bucle, `MaxIntentosPorVersion=2`, comprobación silenciosa al iniciar) funciona sin cambios de comportamiento después de introducir US-001 — verificable contra los tests que ya cubren `EsBucleDeActualizacion`/`AnotarIntento` | US-001 |

## Entidades de datos y relaciones

Todas viven hoy (o se extienden) en `config.json` vía `AppConfig` / `SesionUsuarioService`,
salvo `update.xml` que es manifiesto público en el repo, no dato de usuario.

| Entidad | Campos relevantes (existentes + nuevos) | Persistencia | US origen |
|---|---|---|---|
| `AppConfig` (existente, se extiende) | + `VentanaAncho`, `VentanaAlto`, `VentanaX`, `VentanaY`, `VentanaEstado` (Normal/Maximizada) — nuevos; ya tiene `IntentosDeActualizacion`, `UltimaVersionIntentada`, `TemaOscuro` | `config.json` (`RutasApp.ArchivoConfig`), vía `JsonStore` | US-003 |
| `AppConfig` (existente, se extiende) | + `TamanioTextoExamen` (escala o nivel discreto) — nuevo | `config.json` | US-005 |
| Manifiesto de actualización (`update.xml`) | `version`, `url`, `changelog`, `mandatory` — sin cambio de esquema | Archivo versionado en rama `main` del repo, escrito por el pipeline en vez de por `publicar.ps1 -Publicar` | US-001 |
| Run/registro de publicación | estado (éxito/fallido), paso donde falló, versión intentada | Externo a la app — vive en la plataforma de automatización elegida por arquitecto-tecnico, no en `config.json` | US-001 |
| `IDialogos` (contrato existente, cambia implementación) | `Confirmar`, `Aviso`, `Error` — misma firma, backend deja de ser `MessageBox` | N/A (servicio, no dato) | US-002 |

No se modifica el esquema de `PerfilUsuario` / `ExamenRendido` (US-002 a US-005 no tocan
historial ni corrección).

## Contratos de integración de alto nivel

**US-001 — Pipeline de publicación**
- Disparador: evento de push a `main` del repositorio `MatiVillalba015/AutoExam`.
- Entrada que el pipeline debe leer: `<Version>` de `AutoExam/AutoExam.csproj`; `<version>`
  actual de `update.xml` en `main` (para decidir si hay algo que publicar).
- Pasos esperados, en este orden (mismo orden que hoy exige `publicar.ps1` en dos invocaciones
  manuales, ahora en un solo flujo): compilar Release → verificar `FileVersion` del binario
  contra `<Version>` → empaquetar ZIP → publicar Release en GitHub con el ZIP como asset →
  verificar HTTP 200 de la URL de descarga del asset recién publicado → reescribir `update.xml`
  (version/url/changelog) → commit + push de `update.xml` a `main`.
- Salida esperada: un GitHub Release nuevo (tag `v{version}`) con el ZIP adjunto; un commit
  en `main` que solo toca `update.xml`; en caso de fallo en cualquier paso, ningún artefacto
  público nuevo y una señal de fallo visible sin acceder a la PC del desarrollador.
- No se define aquí la tecnología del pipeline (Actions, u otra) ni el mecanismo exacto de
  publicación de Release — eso es decisión de arquitecto-tecnico, condicionada por el punto
  abierto de permisos (ver "Restricciones").

**US-002 — Diálogos**
- No hay integración externa. Contrato interno: la interfaz `IDialogos` ya existente
  (`Confirmar(mensaje, titulo)`, `Aviso(titulo, mensaje)`, `Error(titulo, mensaje)`,
  `ElegirPdf()`, `AbrirCarpeta(ruta)`) se mantiene sin cambios de firma; solo cambia la
  implementación de `Confirmar`/`Aviso`/`Error` (las que hoy usan `MessageBox.Show`).
  `ElegirPdf` (diálogo de archivo nativo) y `AbrirCarpeta` (Explorador) quedan fuera de
  alcance de 01-spec (no son "confirmaciones, avisos o errores de la app").

**US-003 — Geometría de ventana**
- Contrato interno: `MainWindow` lee geometría de `AppConfig` en `Ventana_Loaded` (antes de
  `WindowStartupLocation` tener efecto) y la escribe en `Ventana_Closing` (mismo punto donde
  hoy se decide `PuedeCerrar()`), reutilizando el guardado ya existente de `AppConfig`
  (`SesionUsuarioService.GuardarConfig`).

**US-004 — Navegación por teclado**
- Contrato interno: atajos nuevos se resuelven contra `ShellViewModel.IrA(clave)` /
  `IrACommand`, ya existente y ya usado por el riel lateral — no se crea un segundo camino de
  navegación. La combinación de teclas exacta es decisión de arquitectura técnica (así lo deja
  el supuesto de 01-spec), con la restricción dura de no colisionar con los `KeyBinding` de
  `ExamenView.xaml`.

**US-005 — Tamaño de texto**
- Contrato interno: nueva preferencia en `AppConfig`, consumida por los `TextBox`/`TextBlock`
  de pregunta y opciones en `Views/ExamenView.xaml` (hoy con `FontSize` fijo o atado a
  `TamEnunciado`/`TamCuerpo` de `Theme/Estilos.xaml`). No se toca `Theme/Tokens.*` (esos tokens
  son de color/tema, no de tamaño de texto de examen).

## Restricciones técnicas conocidas

- **Stack fijo, no evaluable en esta iniciativa**: .NET 8 / WPF / WPF-UI 4.3.0 /
  CommunityToolkit.Mvvm 8.4.0 / AutoUpdater.NET.Official 1.9.3 / PdfPig — confirmado por
  `AutoExam.csproj`, sin margen de reemplazo (01-spec, "Fuera de alcance").
  arquitecto-tecnico decide *cómo* implementar dentro de este stack, no si cambiarlo.
- Distribución: `.exe` self-contained single-file dentro de un ZIP, publicado como asset de
  GitHub Release — confirmado por `PublishSingleFile=true` en el `.csproj` y por el comentario
  en `ActualizacionService.cs` sobre por qué no puede ser un `.exe` suelto (AutoUpdater lo
  trataría como instalador). No cambia con US-001.
- Mecanismo cliente de actualización (`AutoUpdater.NET`, `ActualizacionService.cs`,
  `update.xml`, detección de bucle con `MaxIntentosPorVersion=2`) es intocable por 01-spec;
  US-001 solo automatiza quién y cuándo escribe `update.xml`, no cómo el cliente lo consume.
- **Punto abierto sin resolver, bloqueante para el diseño de US-001**: falta validar si
  `MatiVillalba015/AutoExam` tiene habilitado permiso de escritura para automatización propia
  del repo (crear Releases, hacer commit/push a `main`). Es exactamente la pregunta abierta que
  01-spec deja pendiente. No se puede cerrar el diseño técnico de US-001 (qué corre el pipeline
  y con qué credenciales) sin esta validación — analista-tecnico o arquitecto-tecnico deben
  confirmarla antes de comprometerse a una solución.
- `Theme/Tokens.Claro.xaml` / `Theme/Tokens.Oscuro.xaml` exponen las mismas claves de pincel;
  todo diálogo nuevo debe consumirlas con `DynamicResource` para no romper el intercambio en
  caliente de `TemaService.Aplicar`.
- `Views/ExamenView.xaml` tiene una restricción de foco documentada en el propio XAML (el
  `UserControl` no puede ser `Focusable=True` sin romper el `Click` de las opciones por mouse) —
  cualquier solución de US-004 que toque el árbol de foco de esa vista debe respetarla.
- Todo lo nuevo de persistencia (US-003, US-005) usa el mecanismo ya existente
  (`AppConfig` + `JsonStore` + `RutasApp.ArchivoConfig` bajo `%LOCALAPPDATA%\AppEstudioUBA`,
  redirigible vía `AUTOEXAM_DATOS` para tests) — no se introduce un segundo mecanismo de
  guardado de preferencias.
- Fallos de guardado de preferencias deben degradar igual que el resto de la app: best-effort +
  `RutasApp.RegistrarError`, nunca una excepción que tumbe el cierre o el arranque (mismo patrón
  que `ActualizacionService.Guardar()` y `ShellViewModel.Cerrar()`).

## Criterios de aceptación técnicos

**US-001**
- AC-T1 (US-001): dado un push a `main` con `<Version>` mayor a la de `update.xml`, el pipeline
  ejecuta los 6 pasos del contrato en el orden definido y termina en Release + commit de
  `update.xml`, sin intervención manual — trazable a los Given/When/Then de 01-spec US-001,
  párrafo 1.
- AC-T2 (US-001): dado que la verificación HTTP del paquete no da 200, el pipeline no ejecuta el
  paso de reescritura de `update.xml` y termina en estado fallido visible — trazable a
  01-spec US-001, párrafo 2; ver NFR-04.
- AC-T3 (US-001): dado un push sin cambio de `<Version>`, el pipeline no ejecuta ningún paso
  posterior a la comparación de versión (no compila para publicar, no crea Release) — trazable
  a 01-spec US-001, párrafo 3; ver NFR-03.
- AC-T4 (US-001): dado que build falla o `FileVersion` del binario no coincide con `<Version>`,
  el pipeline no publica Release ni toca `update.xml` — trazable a 01-spec US-001, párrafo 4.
- AC-T5 (US-001): la suite de tests existente sobre `ActualizacionService`
  (`EsBucleDeActualizacion`, `AnotarIntento`) sigue pasando sin modificación después de
  introducir el pipeline — trazable a 01-spec US-001, párrafo 5; ver NFR-13.

**US-002**
- AC-T6 (US-002): con tema claro y con tema oscuro activos, cada `MessageBox.Show` reemplazado
  en `DialogoService` (Confirmar/Aviso/Error) abre un diálogo que consume
  `DynamicResource` de `Theme/Tokens.*` — trazable a 01-spec US-002, criterio 1.
- AC-T7 (US-002): las tres acciones irreversibles listadas en 01-spec (salir con examen sin
  terminar, borrar historial, quitar un libro) siguen pasando por `IDialogos.Confirmar` con
  respuesta explícita del usuario — trazable a 01-spec US-002, criterio 2.

**US-003**
- AC-T8 (US-003): cerrar la app con geometría distinta a la default y reabrirla reproduce
  tamaño, posición y estado exactos, leídos de `AppConfig` — trazable a 01-spec US-003,
  criterio 1; ver NFR-06.
- AC-T9 (US-003): con la posición guardada fuera de toda área de trabajo visible, la ventana
  abre centrada y completamente visible en la pantalla principal — trazable a 01-spec US-003,
  criterio 2; ver NFR-07.

**US-004**
- AC-T10 (US-004): con foco fuera de un campo de texto, cada atajo definido navega a la sección
  correspondiente en el orden Libros/Nuevo examen/Examen/Historial/Ajustes de
  `ShellViewModel.Paginas` — trazable a 01-spec US-004, criterio 1.
- AC-T11 (US-004): la suite de regresión sobre los `KeyBinding` de `ExamenView.xaml` (1-4, A-D,
  flechas, Enter, S) no cambia de resultado tras sumar los atajos de navegación — trazable a
  01-spec US-004, criterio 2; ver NFR-09.
- AC-T12 (US-004): con foco en un campo de texto editable, tipear las teclas de atajo no
  dispara navegación — trazable a 01-spec US-004, criterio 3; ver NFR-10.

**US-005**
- AC-T13 (US-005): al ajustar el tamaño de texto durante un examen, pregunta y opciones cambian
  de tamaño sin recorte ni scroll horizontal — trazable a 01-spec US-005, criterio 1.
- AC-T14 (US-005): la preferencia de tamaño ajustada se lee de `AppConfig` al reabrir la app y
  no vuelve al valor por defecto — trazable a 01-spec US-005, criterio 2; ver NFR-12.

## Definition of Done de esta spec

- Todo NFR tiene umbral medible: cumplido (tabla NFR, columna "Umbral medible").
- Toda entidad de datos tiene US-XXX de origen: cumplido (tabla de entidades, columna "US
  origen").

Sugerencia: antes de que arquitecto-tecnico cierre el diseño de US-001, resolver el punto
abierto de permisos de automatización del repo (bloqueante, señalado en "Restricciones
técnicas") — es el único ítem que puede forzar revisar esta spec.
