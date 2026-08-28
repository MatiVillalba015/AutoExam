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

## Definition of Done de esta spec (increment 1)

- Todo NFR tiene umbral medible: cumplido (tabla NFR, columna "Umbral medible").
- Toda entidad de datos tiene US-XXX de origen: cumplido (tabla de entidades, columna "US
  origen").

Sugerencia: antes de que arquitecto-tecnico cierre el diseño de US-001, resolver el punto
abierto de permisos de automatización del repo (bloqueante, señalado en "Restricciones
técnicas") — es el único ítem que puede forzar revisar esta spec.

---

# Incremento 2 — Tech Spec: Disciplina de versión + animaciones de shell (US-006 a US-008)

Traduce `specs/01-spec.md` (sección "Incremento 2", US-006 a US-008) a especificación técnica.
US-001 a US-005 ya están implementadas, firmadas (`specs/uat-signoff.md`) y no se reabren. Stack
sin cambios (ver "Restricciones técnicas" de arriba, sigue vigente sin excepción); este
incremento es aditivo sobre el pipeline de US-001 y sobre `Theme/Estilos.xaml` de US-002/US-005,
no un reemplazo de ninguno de los dos.

## Estado real del código (ancla de este incremento)

- `.github/workflows/publish.yml` (ya implementado, US-001 cerrado): el paso "Comparar version
  del proyecto vs. update.xml" ya contiene, inline en PowerShell, exactamente la lógica que
  US-006 necesita reproducir del lado local (leer `<Version>` de `AutoExam/AutoExam.csproj`,
  leer `<version>` de `update.xml` con regex, comparar como `[version]`). Esta lógica hoy solo
  corre después del push, dentro del run de Actions — no sirve tal cual para el caso de uso de
  US-006 ("antes de pushear"), pero es la fuente de verdad a no duplicar.
- `AutoExam/AutoExam.csproj`: `<Version>1.0.2</Version>` — mismo campo que ya lee el pipeline.
- Intento previo, incompleto, ya en el working tree (evaluado línea por línea para este
  documento, no de memoria):
  - `AutoExam/Behaviors/TransicionContenido.cs`: `DependencyProperty` adjunta `Activa` sobre
    `ContentControl`, con `DependencyPropertyDescriptor.FromProperty(ContentControl
    .ContentProperty, ...)` para engancharse a cada cambio de `Content` (necesario porque
    `ContentControl` no expone un evento de "contenido cambiado" utilizable como
    `EventTrigger`). Al cambiar `Content` anima `Opacity` (0→1) y `TranslateTransform.Y`
    (10→0) en 220 ms con `QuadraticEase EaseOut`, vía `BeginAnimation` directo (no
    `Storyboard`/XAML). **Veredicto: retomar sin reescribir el mecanismo** — es la solución
    correcta al problema real (no hay evento de cambio de contenido en `ContentControl`), no
    introduce dependencias nuevas, reemplaza limpiamente la animación anterior en cada cambio
    (`BeginAnimation` con un nuevo `DoubleAnimation` interrumpe el clock previo sin dejar
    contenido a medio animar), y ya está cableado en `MainWindow.xaml` sobre el único
    `ContentControl` correcto (el de `{Binding Pagina}` del shell, no el de `ExamenView.xaml`,
    que no usa este mecanismo — confirmado leyendo `Views/ExamenView.xaml`: la navegación entre
    preguntas es por `Visibility`/binding dentro del mismo `UserControl`, nunca reemplaza
    `Content` de un `ContentControl`). Ver §"Decisiones de diseño" para los dos ajustes que sí
    hacen falta antes de darlo por cerrado.
  - `MainWindow.xaml`: namespace `comportamientos` importado y
    `comportamientos:TransicionContenido.Activa="True"` ya puesto en el `ContentControl` de
    `Grid.Column="1"` (línea ~99). No requiere más cambios de wiring para US-007.
  - `Theme/Estilos.xaml`: `DuracionHover` (`Duration`, 140 ms), `DuracionPresion` (`Duration`,
    80 ms), `SuavizadoSalida` (`QuadraticEase EaseOut`) ya declarados como recursos
    compartidos, siguiendo el mismo patrón documentado en el propio archivo ("una sola
    instancia alcanza para todos los templates" porque `Duration`/`EasingFunction` no son
    Freezables con estado propio). **Veredicto: retomar, no están usados todavía en ningún
    `ControlTemplate`** — son la base correcta para US-008, faltan los `Trigger.EnterActions`/
    `ExitActions` que los consuman en los 7 estilos alcanzados (`Chip`, `ChipAccion`,
    `OpcionExamen`, `BaldosaPregunta`, `ItemNavegacion`, `ItemLibro`, `ZonaSoltar`), ninguno
    tocado todavía.

## NFR (no funcionales aplicables de este incremento)

| ID | Requisito | Umbral medible | US |
|---|---|---|---|
| NFR-14 | Latencia del chequeo local de versión | Desde invocar el script hasta el mensaje de resultado: < 3 s (lectura de 2 archivos locales, sin red) | US-006 |
| NFR-15 | Coherencia entre chequeo local y pipeline | El resultado (`esMayor`/`should_publish`) que informa el chequeo local coincide con el que produce `publish.yml` para el mismo par `<Version>`/`<version>` en el 100% de los casos — se cumple por construcción si ambos consumen la misma lógica compartida, no por coincidencia | US-006 |
| NFR-16 | No intrusión del chequeo local | El chequeo no escribe ningún archivo, no hace commit/push, no bloquea el `git push` (código de salida no está cableado a ningún hook por defecto) — 0 efectos secundarios verificables por diff de working tree antes/después de correrlo | US-006 |
| NFR-17 | Duración de la transición de sección | 200–250 ms por cambio de sección (valor ya fijado en `TransicionContenido.cs`, 220 ms) — no "se ve bien", es el rango que separa "abrupto" (< 150 ms) de "lento" (> 300 ms) | US-007 |
| NFR-18 | No demora la navegación real | El cambio de `ShellViewModel.Pagina` (dato) ocurre en el mismo frame que el click/atajo; la animación es feedback visual posterior, nunca una espera antes de que el comando de navegación se ejecute — verificable: `IrACommand` no depende de que la animación anterior termine | US-007 |
| NFR-19 | Sin superposición en navegación rápida | Ante ≥ 5 cambios de sección en < 2 s, cada `BeginAnimation` reemplaza limpiamente el clock anterior — 0 excepciones, 0 frames con contenido de dos secciones mezclado, verificable como prueba exploratoria repetible | US-007 |
| NFR-20 | Duración de animación de hover | Igual al recurso `DuracionHover` (140 ms) en los 7 estilos alcanzados por US-008 — 0 valores hardcodeados fuera de ese recurso compartido | US-008 |
| NFR-21 | Duración de animación de presión | Igual al recurso `DuracionPresion` (80 ms) en los mismos 7 estilos — 0 valores hardcodeados fuera de ese recurso | US-008 |
| NFR-22 | Sin animación en elemento deshabilitado | 0 disparos de Storyboard de hover/press en cualquiera de los 7 estilos cuando `IsEnabled=False` — verificable por inspección de cada `ControlTemplate.Triggers` (guardia `IsEnabled=True` explícita) | US-008 |
| NFR-23 | Sin color literal nuevo | 0 colores/pinceles nuevos introducidos en los 7 `ControlTemplate` tocados; toda animación opera sobre `Opacity` de una capa ya tematizada con `DynamicResource` o sobre un `ScaleTransform` (que no tiene color) — mismo criterio que ya exige el encabezado de `Theme/Estilos.xaml` | US-008 |

## Entidades de datos y relaciones

Este incremento no persiste datos nuevos (US-007/US-008 son puramente visuales, sin estado
guardado; US-006 es un chequeo efímero, sin escritura a disco).

| Entidad | Campos relevantes | Persistencia | US origen |
|---|---|---|---|
| Resultado de comparación de versión | `versionCsproj`, `versionPublicada`, `esMayor` (bool) | No persiste — valor calculado en cada corrida del script, se descarta al terminar | US-006 |

No hay entidades nuevas para US-007 ni US-008 — casilla vacía intencional, no un olvido: no
hay ningún dato de negocio ni preferencia de usuario involucrado en transición de sección o en
feedback de hover/press (a diferencia de US-005, que si guarda una preferencia).

## Contratos de integración de alto nivel

**US-006 — Chequeo local de versión**
- No hay integración externa (sin red, sin GitHub API). Contrato: un script local que lee dos
  archivos ya existentes del propio checkout (`AutoExam/AutoExam.csproj`, `update.xml`) y
  expone el resultado como salida de consola (para uso interactivo) y como código de salida
  (para uso encadenado en otra herramienta, sin que eso implique que algo lo bloquee hoy).
- Restricción de diseño no negociable (regla de negocio de 01-spec): el chequeo es
  informativo — no escribe archivos, no hace `git commit`/`push`, no invoca a
  `publicar.ps1` ni al pipeline de US-001.
- Relación con el pipeline existente: la comparación que hoy vive inline en el paso 2-3 de
  `publish.yml` (§4.1 de `03-architecture.md`) pasa a ser la misma lógica que consume el
  chequeo local — un único lugar define "qué significa que la versión subió", consumido desde
  dos puntos (local, antes del push; CI, después del push). Ver "Decisiones de diseño" para el
  mecanismo concreto de cómo se comparte sin duplicar código entre PowerShell local y el step
  de Actions.

**US-007 — Transición de sección**
- Contrato interno, sin integración externa: comportamiento adjunto sobre un único
  `ContentControl` (`Behaviors/TransicionContenido.cs` + `Activa="True"` en `MainWindow.xaml`,
  ya cableado). No expone ninguna superficie nueva a otros módulos — `ShellViewModel.Pagina`
  sigue siendo la única fuente de qué vista se muestra; la animación es un efecto secundario de
  ese cambio, no un nuevo canal de navegación.

**US-008 — Feedback de hover/press**
- Contrato interno: los 7 `ControlTemplate` de `Theme/Estilos.xaml` (`Chip`, `ChipAccion`,
  `OpcionExamen`, `BaldosaPregunta`, `ItemNavegacion`, `ItemLibro`, `ZonaSoltar`) ganan
  animación sin cambiar su contrato visible hacia las vistas que los consumen — ninguna vista
  (`Views/*.xaml`) cambia; solo cambian los templates en `Theme/Estilos.xaml`. Ningún
  `DataTrigger`/binding externo depende de la estructura interna de estos templates, así que no
  hay ruptura de contrato hacia arriba.

## Restricciones técnicas conocidas (específicas de este incremento)

- Sin librería de comportamientos de terceros (`Microsoft.Xaml.Behaviors` u otra): el proyecto
  no la referencia hoy y no hace falta agregarla — el patrón de `DependencyProperty` adjunta
  con `DependencyPropertyDescriptor` (ya usado en el intento previo) resuelve US-007 sin
  dependencias nuevas, consistente con la restricción dura de no tocar el stack.
- `ContentControl` no expone un evento de cambio de contenido utilizable como `EventTrigger`
  declarativo — es la razón técnica real por la que US-007 no se resuelve con
  `Trigger.EnterActions` (a diferencia de US-008, donde `IsMouseOver`/`IsPressed` sí son
  triggers de propiedad normales). No cambiar de mecanismo a mitad de implementación sin
  volver a evaluar esta restricción.
- Animar directamente el `Background` de un `Border` cuyo valor es un `DynamicResource` (p.ej.
  `PincelTarjetaHover`) con una `ColorAnimation`/`BrushAnimation` mutaría el recurso compartido
  del diccionario de tema, afectando a todas las instancias del estilo y rompiendo el
  intercambio en caliente de `TemaService.Aplicar`. Restricción dura: **ninguna animación de
  hover/press de US-008 anima un `Brush`/`Color` directamente**; solo `Opacity` de una capa
  overlay ya pintada con el pincel de destino, o un `ScaleTransform` (sin color). Esto es lo
  que garantiza NFR-23 sin necesidad de revisión manual exhaustiva.
- `Theme/Estilos.xaml` ya fija el patrón "una sola instancia de `Duration`/`EasingFunction`
  compartida" — los 7 templates de US-008 deben consumir `{DynamicResource DuracionHover}` /
  `{DynamicResource DuracionPresion}` / `{DynamicResource SuavizadoSalida}` ya declarados, no
  declarar sus propios valores locales (evita divergencia de timing entre estilos, y es lo que
  hace verificable NFR-20/NFR-21 por inspección simple).
- El `ContentControl` alcanzado por US-007 es exclusivamente el de `MainWindow.xaml` que
  bindea `{Binding Pagina}` — ningún otro `ContentControl`/`ContentPresenter` de la app (en
  particular, ninguno dentro de `Views/ExamenView.xaml`) recibe `TransicionContenido.Activa`.
  Esto no es una preferencia de implementación: es lo que hace que el criterio 4 de US-007 (no
  afectar la navegación entre preguntas) se cumpla por construcción, no por disciplina de
  código.
- US-006 no puede depender del pipeline de Actions para funcionar (tiene que correr sin red,
  antes de pushear) — el script debe leer los dos archivos directamente del working tree local,
  nunca contra `raw.githubusercontent.com` ni la API de GitHub.

## Decisiones de diseño que cierran ambigüedad

- **US-006 — mecanismo elegido: script local, no git hook, no step adicional únicamente en
  CI.** Evaluado contra los tres caminos que dejaba abiertos 01-spec:
  - *Step informativo adicional en `publish.yml`*: descartado como mecanismo único — corre
    después del push, y el criterio de aceptación de US-006 es explícitamente "antes de
    pushear" (evita la sorpresa post-push, no la explica después).
  - *Git hook (`pre-push`)*: descartado — un hook no versionado requiere instalación manual por
    clon (mismo costo de fricción que acordarse de correr un script), y estar "en modo
    informativo" en un hook (siempre `exit 0`) no aporta nada que un script invocado a mano no
    dé ya, a cambio de una pieza más para mantener. Queda como mejora opcional futura, no como
    parte de este incremento (ver "Sugerencia" al final).
  - *Script local (elegido)*: `Verificar-Version.ps1` en la raíz del repo, junto a
    `publicar.ps1` (mismo lugar donde el desarrollador ya busca herramientas de publicación).
    Se invoca a mano antes de pushear. No requiere instalación, no requiere tocar el `.git` del
    clon, y es trivialmente reutilizable como fuente de la lógica compartida con el paso 2-3 de
    `publish.yml`.
  - Para cumplir el supuesto de 01-spec de "no duplicar lógica": la comparación
    (`[version]csproj -gt [version]update.xml`) se extrae a un único bloque de lógica que hoy
    vive inline en `publish.yml`; el script local reimplementa esa misma comparación en
    PowerShell (mismo lenguaje que ya usa `publish.yml`, sin necesidad de un tercer runtime) —
    ya sea que se factorice como un `.ps1` compartido invocado por ambos, o que el paso de
    `publish.yml` se reescriba para invocar al mismo `Verificar-Version.ps1` con un flag que
    emita `GITHUB_OUTPUT` además del mensaje de consola. La decisión de cuál de las dos formas
    de compartir el archivo (script único parametrizado vs. función común importada) queda a
    criterio de implementación — el contrato no negociable es que exista un solo lugar que
    defina la comparación, consumido por los dos caminos.
  - Formato de salida esperado (para que el criterio de aceptación sea verificable sin
    ambigüedad): mensaje explícito de una de dos formas —
    `"<Version> ({csproj}) NO supera la publicada ({update.xml}) — este push NO va a disparar
    ninguna publicación nueva."` o
    `"<Version> ({csproj}) supera la publicada ({update.xml}) — este push SI va a disparar la
    publicación automática (US-001)."`

- **US-007 — ajustes puntuales sobre el intento existente antes de darlo por cerrado** (no se
  reescribe el mecanismo, se completa):
  1. Primer `Content` (de `null` a la primera página tras salir del `Onboarding`) también
     anima hoy — es aceptable y deseable (evita un "salto" al entrar al shell por primera vez),
     no requiere cambio.
  2. El valor `220 ms`/`QuadraticEase` está hardcodeado en el `.cs`, no toma
     `SuavizadoSalida`/una duración compartida de `Theme/Estilos.xaml` (a diferencia de
     US-008, que sí depende de esos recursos). No es un defecto bloqueante — es un mecanismo
     de código, no de template, y la duración de transición de sección es conceptualmente
     distinta a la de hover/press — pero se recomienda extraer un recurso propio
     (`DuracionTransicionSeccion`, 220 ms) en `Theme/Estilos.xaml` y leerlo con
     `Application.Current.TryFindResource(...)` desde el comportamiento, para que toda
     duración de animación de la app viva en el mismo archivo (mismo principio que ya declara
     el encabezado de `Theme/Estilos.xaml` para color/tipografía). Queda a criterio del
     developer si lo resuelve en este incremento o lo deja como mejora menor — no es criterio
     de aceptación de US-007.
  3. No se requiere ningún cambio en `MainWindow.xaml` — el wiring (`Activa="True"` sobre el
     `ContentControl` correcto) ya está completo.

- **US-008 — mecanismo elegido: `Trigger.EnterActions`/`ExitActions` con `Storyboard`, no
  `VisualStateManager`.** Los 7 estilos ya usan `ControlTemplate.Triggers` con `Setter`s planos
  para `IsChecked`/`IsSelected`/`IsKeyboardFocused`/`IsEnabled` (estados instantáneos, sin
  animar, fuera de alcance de US-008). Agregar `VisualStateManager` implicaría reestructurar
  esos 7 templates a un modelo de estados completo (grupos de estado, transiciones) solo para
  sumar dos animaciones puntuales — costo de reescritura no justificado por el alcance pedido.
  `Trigger.EnterActions`/`ExitActions` conviven en el mismo `Trigger` que ya declara la
  condición (`IsMouseOver`, `IsPressed`), agregando un `BeginStoryboard`/`StopStoryboard` sin
  tocar los `Setter`s existentes de otros estados — cambio incremental, no reescritura.
  - Mecanismo de hover: cada template gana un elemento overlay adicional (`Border` del mismo
    `CornerRadius`/forma que el fondo base, `IsHitTestVisible="False"`, pintado con el mismo
    pincel de hover que hoy ya se asigna instantáneo por `Setter` — p.ej. `PincelTarjetaHover`,
    `PincelMarcaSuave` según el estilo — y `Opacity="0"` en reposo). El `Trigger` de
    `IsMouseOver="True"` pasa de `<Setter Property="Background" .../>` a
    `EnterActions`/`ExitActions` con un `Storyboard` que anima `Opacity` de ese overlay entre 0
    y 1 usando `{DynamicResource DuracionHover}` y `{DynamicResource SuavizadoSalida}`. Se
    conserva el pincel de hover ya definido por estilo, no se inventa uno nuevo.
  - Mecanismo de presión: `RenderTransform="{ScaleTransform}"` con
    `RenderTransformOrigin="0.5,0.5"` sobre el elemento de fondo (o el `ContentPresenter`,
    según lo que se necesite escalar visualmente sin recortar bordes/sombra). Un `Trigger` de
    `IsPressed="True"` anima `ScaleX`/`ScaleY` a un valor sutil (≈0.97) con
    `{DynamicResource DuracionPresion}` y el mismo `SuavizadoSalida`; `ExitActions` vuelve a
    1.0. Esta animación es deliberadamente distinta a la de hover (escala vs. opacidad) para
    cumplir el criterio de 01-spec de que press se sienta diferente a hover, no solo "más de lo
    mismo".
  - Guardia de deshabilitado (NFR-22): cada `Trigger` de hover/press pasa a `MultiTrigger` con
    dos condiciones (`IsMouseOver="True"`/`IsPressed="True"` **y** `IsEnabled="True"`), en vez
    de depender de que el control deje de recibir eventos de mouse al deshabilitarse (WPF no
    lo garantiza de forma uniforme entre todos los tipos de control usados aquí — `ToggleButton`,
    `Button`, `RadioButton`, `ListBoxItem`). Es una guardia explícita, no una suposición sobre
    el framework.
  - `BaldosaPregunta` es el único de los 7 que hoy no tiene `Trigger` de `IsEnabled="False"` —
    agregar la guardia de `MultiTrigger` en este template igual, aunque hoy nada lo deshabilite,
    para que la regla sea uniforme entre los 7 estilos y no dependa de si alguna vista futura
    empieza a deshabilitarlo.

## Criterios de aceptación técnicos

**US-006**
- AC-T15 (US-006): dado `<Version>` sin superar la publicada en `update.xml`, al correr el
  chequeo local el mensaje indica explícitamente que ese push no va a disparar publicación —
  trazable a 01-spec US-006, criterio 2.
- AC-T16 (US-006): dado `<Version>` superando la publicada, al correr el chequeo local el
  mensaje confirma que el push va a disparar la publicación de US-001 — trazable a criterio 3.
- AC-T17 (US-006): correr el chequeo, en cualquier estado, no modifica `update.xml` ni
  `AutoExam.csproj`, no genera commits ni pushes — trazable a criterio 4; ver NFR-16.
- AC-T18 (US-006): el resultado del chequeo local y el `should_publish` que calcula
  `publish.yml` para el mismo par de versiones coinciden siempre, por compartir la misma lógica
  — trazable a criterio 1; ver NFR-15.

**US-007**
- AC-T19 (US-007): al cambiar de sección por cualquier medio (mouse, atajo `Ctrl+1..5` de
  US-004), el `ContentControl` de la página activa anima `Opacity` 0→1 y `TranslateTransform.Y`
  10→0 en 200–250 ms antes de asentarse — trazable a criterio 1; ver NFR-17.
- AC-T20 (US-007): con tema claro y con tema oscuro activos, la transición no introduce ningún
  color propio — solo anima opacidad/posición de contenido ya tematizado con
  `DynamicResource` — trazable a criterio 2.
- AC-T21 (US-007): ante ≥ 5 cambios de sección en < 2 s, cada animación reemplaza limpiamente a
  la anterior sin excepción ni contenido de dos secciones superpuesto — trazable a criterio 3;
  ver NFR-19.
- AC-T22 (US-007): ningún `ContentControl` de `Views/ExamenView.xaml` usa
  `TransicionContenido.Activa` — verificación estructural (no de comportamiento en runtime) —
  trazable a criterio 4.

**US-008**
- AC-T23 (US-008): con mouse sobre cada uno de los 7 elementos alcanzados (`Chip`,
  `OpcionExamen`, `BaldosaPregunta`, `ItemNavegacion`, `ItemLibro`, `ZonaSoltar`,
  `ChipAccion`), la capa overlay anima su `Opacity` con `DuracionHover`/`SuavizadoSalida` —
  trazable a criterio 1; ver NFR-20.
- AC-T24 (US-008): al presionar y soltar cada uno de los 7 elementos, se observa una animación
  de `ScaleTransform` con `DuracionPresion`, visualmente distinta a la de hover — trazable a
  criterio 2; ver NFR-21.
- AC-T25 (US-008): en tema claro y en tema oscuro, ningún overlay/transform de hover o presión
  introduce un color propio ajeno a los pinceles `DynamicResource` ya usados por cada estilo —
  trazable a criterio 3; ver NFR-23.
- AC-T26 (US-008): con un elemento en `IsEnabled="False"` (p.ej. una `OpcionExamen` bloqueada),
  pasar el mouse o presionar no dispara ninguna animación de hover/press — trazable a
  criterio 4; ver NFR-22.

## Definition of Done de este incremento

- Todo NFR tiene umbral medible: cumplido (tabla NFR de este incremento, columna "Umbral
  medible").
- Toda entidad de datos tiene US-XXX de origen: cumplido — la única entidad de este incremento
  (resultado de comparación de versión, no persistido) está trazada a US-006; US-007/US-008 no
  introducen entidades, documentado explícitamente en vez de omitido.

Sugerencia: una vez el developer confirme que `Verificar-Version.ps1` y el paso 2-3 de
`publish.yml` comparten la misma lógica (AC-T18), evaluar si conviene un `pre-push` hook
opcional (no versionado por defecto, instalación manual documentada en el README) que solo
imprima el mismo mensaje sin bloquear — quedó descartado para este incremento por costo/
beneficio, no por incompatibilidad técnica.

---

# Incremento 3 — Tech Spec: Confiabilidad de generación con Gemini + housekeeping de repo +
# rediseño visual morado/suave (US-009 a US-011)

Traduce `specs/01-spec.md` (sección "Incremento 3", US-009 a US-011) a especificación técnica.
US-001 a US-008 ya están implementadas y firmadas (`specs/uat-signoff.md`); este documento no
las reabre. Stack sin cambios (sigue vigente la sección "Restricciones técnicas" del
incremento 1, sin excepción — 01-spec confirma "Fuera de alcance": no se cambia .NET/WPF/
WPF-UI ni el proveedor Gemini). Las tres US de este incremento son independientes entre sí
(mismo criterio que ya usa 01-spec) y se documentan en secciones separadas donde corresponde.

## Estado real del código (ancla de este incremento)

**US-009 — `AutoExam/Services/GeminiApiService.cs` (relevado completo, 2762 líneas, no de
memoria).** El mecanismo que pide el síntoma ya existe y funciona: lotes adaptativos
(`MaxPreguntasPorLote=15`, `MaxLotesPorExamen=4`, `MinPreguntasPorLote=3`), presupuesto de
relleno (`MaxLotesDeRelleno=10`, tope duro `MaxPeticionesPorExamen=16`,
`MaxLotesEsteriles=3`), rotación de claves ante 429 (`AnilloDeClaves.Rotar`), backoff con
`Retry-After`/`retryDelay` del propio Google y tope de 90 s (`CalcularEspera`), ritmo mínimo
entre peticiones por clave (`SeparacionEntrePeticiones=2.5s`, semáforo `Turno` de una sola
petición en vuelo), reducción a la mitad del lote cuando la respuesta viene truncada
(`generadas.Truncado`, líneas 706-714), y progreso rico vía `IProgress<string>` (plan, lote en
curso, rotación de clave, espera de backoff, lote de relleno). Nada de esto es la causa raíz;
reemplazarlo sería ir contra el lineamiento "código simple, estilo trainee" sin necesidad.

Causa raíz identificada (con cita textual del propio código, no interpretación):

1. **El techo de tokens de salida (`maxOutputTokens`) arranca conservador y solo se corrige
   de forma reactiva, nunca antes del primer lote de un examen.** `CalcularTopeTokens` usa
   `TechoDeSalidaConocido(modelo)` (poblado por `_techoDeSalida`, que solo se llena cuando se
   llama `ListarModelosAsync` — hoy eso pasa únicamente ante un 404 del modelo configurado
   (`BuscarModeloVigenteAsync`) o cuando el usuario toca "Detectar modelos" a mano en Ajustes).
   Mientras no se haya consultado, se asume `TopeTokensPorDefecto = 8192` — el propio código lo
   documenta: *"El techo real lo informa ListModels (outputTokenLimit). Mientras no se haya
   consultado se asume el minimo comun, que es preferible a pasarse."* (líneas 1419-1429). Para
   un modelo cuyo techo real es mayor (arquitecturas nuevas superan 16k-65k), el primer lote de
   cualquier examen arranca pidiendo menos espacio de salida del que el propio tope
   auto-impuesto de la app permitiría (`TopeTokensMaximo = 16384`), lo que hace más probable
   el truncado (`finishReason: MAX_TOKENS`) en lotes de 15 preguntas con esquema completo
   (enunciado + 4 opciones + `AnalisisPorOpcion` + justificación).
2. **El truncado dispara una corrección reactiva, no preventiva**: recién cuando un lote
   vuelve truncado se reduce `porLote` a la mitad (línea 706-714) para los lotes siguientes —
   el primer intento (y a veces el segundo) paga el costo de una petición gastada con poco o
   ningún rendimiento antes de que el sistema aprenda el tamaño correcto. Con 30 preguntas
   planificadas en 2 lotes de 15 (`CalcularPreguntasPorLote`), un solo lote truncado ya empuja
   al bucle a pedir lotes de relleno adicionales, acercándose al tope de 16 peticiones.
3. **El aprendizaje no persiste entre sesiones.** `_topeTokensVigente`, `_razonamientoApagable`
   y `_techoDeSalida` son campos `static` a nivel de proceso (comentario propio, líneas
   246-270: "Existe para las pruebas, porque son campos estaticos"). Cada reinicio de la app
   vuelve a arrancar en modo pesimista (8192, razonamiento asumido apagable) hasta que el
   primer 400/truncado de esa sesión lo corrija — la corrección de la sesión anterior no se
   reutiliza.
4. **El presupuesto de peticiones choca con un límite externo real, no de la app.** El propio
   código documenta la cuota gratuita de Gemini como *"del orden de 10-15 peticiones"* por
   minuto y *"limit: 20"* (`generate_content_free_tier_requests`) por día y por clave (líneas
   118-136). Un examen de 30 preguntas que, por el punto 1-2, necesite varios lotes de relleno
   puede consumir gran parte o toda la cuota diaria de una única clave en un solo intento —
   esto es una limitación externa de la cuenta/clave de Google del usuario (ver "Preguntas
   abiertas" de 01-spec), que la rotación de claves ya implementada mitiga pero no elimina para
   quien solo cargó una clave.

En síntesis: el mecanismo de reintento/backoff/rotación **no está roto**; lo que falta es que
el primer lote de cada examen arranque con la mejor información posible sobre cuánto espacio
de salida admite el modelo, para minimizar cuántos de los 16 disparos disponibles se gastan en
lotes truncados. Ver "Decisiones de diseño" para el enfoque de solución.

**US-010 — housekeeping de repo (relevado por lectura directa, sin herramienta de búsqueda de
texto masiva disponible en esta corrida; ver limitación al final de esta sección).**
- `.gitignore` (raíz): tiene una entrada corrupta en la última línea —
  `. c l a u d e / s k i l l s /` (caracteres separados por espacios, patrón típico de un
  archivo guardado con una codificación equivocada, ej. UTF-16 leído como texto plano) — esa
  regla **no excluye nada** en la práctica. Esto explica por qué `.claude/` aparece como
  no trackeada en `git status` en vez de simplemente ignorada: no hay ninguna entrada
  funcional que la cubra hoy, ni siquiera la parcial `.claude/skills/` que el archivo parece
  haber intentado escribir.
- `specs/team-roster.yaml`, `specs/qa-report.md`, `specs/uat-signoff.md`,
  `.github/workflows/publish.yml`, `specs/03-architecture.md`: relevados de punta a punta para
  este documento — **no contienen** menciones textuales a "Claude" ni a un asistente de IA por
  nombre. Sí describen, con nombres de rol genéricos (`analista-tecnico`, `arquitecto-tecnico`,
  `developer`, `test-developer`, `devops`, `QA`), un proceso de desarrollo con instancias
  paralelas — eso es información de proceso del proyecto, no una mención a una herramienta de
  IA puntual, y son specs/reportes ya congelados (US-010 los trata como "vital": no se borran,
  solo se les quitaría una mención a IA si la tuvieran, y hoy no la tienen).
- No existe `CLAUDE.md` en la raíz del repo (confirmado: intento de lectura falla por archivo
  inexistente).
- `git status` (provisto al inicio de esta sesión) confirma no trackeados: `.claude/` (carpeta
  completa), más archivos de código/tests nuevos (`AutoExam/Behaviors/Presionable.cs`,
  `AutoExam/Behaviors/TransicionContenido.cs`, `AutoExam.Tests/Infraestructura/
  ArchivoFuenteHelper.cs`, `AutoExam.Tests/Scripts/`, `AutoExam.Tests/Views/
  ExamenViewTransicionContenidoTests.cs`, `Verificar-Version.ps1`) — estos últimos son código
  y tests de incrementos anteriores ya funcionales (US-006/US-007/US-008): candidatos a "vital"
  por defecto, solo se auditan por comentarios con mención a IA, nunca se borran por US-010.
- **Limitación de esta corrida**: esta spec no tuvo acceso a una herramienta de búsqueda de
  texto (grep/glob) sobre el árbol completo del repo, solo lectura de archivo por archivo. Los
  archivos citados arriba fueron revisados de punta a punta; el resto del árbol (en particular
  comentarios dentro de `AutoExam/**/*.cs` y `AutoExam/**/*.xaml`) **no fue auditado
  exhaustivamente palabra por palabra** en esta pasada. La sección "Criterios de aceptación
  técnicos" deja esto como un paso explícito a ejecutar con una búsqueda de texto real antes de
  dar por cerrada la limpieza (ver AC-T33).

**US-011 — `AutoExam/Theme/Tokens.Claro.xaml`, `AutoExam/Theme/Tokens.Oscuro.xaml`,
`AutoExam/Theme/Estilos.xaml`, `AutoExam/MainWindow.xaml` (relevados completos).**
- Ambos diccionarios de tokens exponen exactamente 20 claves (6 de superficie, 3 de texto, 4
  de marca, 6 semánticas de corrección, 1 sombra) — mismo set en los dos, tal como exige el
  mecanismo de `TemaService` (ver incremento 1). `MainWindow.xaml` no tiene un solo color
  literal (0 `Color="#..."`/`Background="#..."` fuera de `DynamicResource`), cumpliendo ya hoy
  la regla del encabezado de `Estilos.xaml`.
- El morado **ya existe** como color de marca: `PincelMarca` (`#6246C8` claro / `#9B87F5`
  oscuro), `PincelMarcaFuerte` (`#4F38AA` / `#B3A3FF`), `PincelMarcaSuave` (`#EEEAFB` /
  `#2A2440`) — 3 tonos de morado por tema, usados hoy solo como acento (bordes de selección,
  chips activos, ítem de navegación activo). El propio comentario de `Tokens.Oscuro.xaml`
  explica por qué es violeta y no otro color: *"verde, rojo y ambar ya estan tomados por la
  correccion... un acento en esos tonos competiria con el significado de la nota"* — esa razón
  de diseño sigue vigente y no se puede violar sumando morado a los pinceles semánticos de
  acierto/error/pendiente.
- El resto de la paleta (superficie, borde, texto) es neutra gris/azulada, no tiene tinte de
  marca — es la superficie principal de "predominancia" que falta ampliar para cumplir US-011.
- Contraste actual (verificado a ojo por luminancia relativa, no con herramienta): texto sobre
  fondo ya es alto en ambos temas (`#1A1D26` sobre `#F4F5F8` en claro, `#E4E7EE` sobre
  `#12141A` en oscuro) — hay margen para "suavizar" (bajar contraste) sin cruzar el piso de
  legibilidad WCAG AA, que es el criterio medible que fija esta spec (ver NFR-32/NFR-33).
- `Theme/Estilos.xaml` (854 líneas, ya extendido por el incremento 2 con overlays de
  hover/press): los 7 `ControlTemplate` de la sección de animación consumen pinceles
  `DynamicResource` por nombre (`PincelTarjetaHover`, `PincelMarcaSuave`, etc.) — un cambio de
  valores en `Tokens.*.xaml` los actualiza automáticamente sin tocar `Estilos.xaml`, siempre
  que se mantengan los mismos nombres de clave.

## NFR (no funcionales aplicables de este incremento)

| ID | Requisito | Umbral medible | US |
|---|---|---|---|
| NFR-24 | Techo de tokens informado antes del primer lote | Cuando `ListModels` es consultable con la clave configurada, el primer lote de generación de un examen usa el `outputTokenLimit` real del modelo (no el default 8192) en el 100% de las corridas donde esa consulta se resuelve sin error — verificable comparando el `maxOutputTokens` del primer request contra el valor cacheado en `_techoDeSalida` para ese modelo | US-009 |
| NFR-25 | Tasa de éxito de examen de 30 preguntas | Con al menos una clave con cuota diaria disponible, un examen de 1 a 30 preguntas se completa con la cantidad exacta pedida en ≥ 95% de un set de al menos 20 corridas de prueba (contra la API real o un doble que simule truncado/429 según defina QA) — no 100%: el rendimiento por lote del modelo no es 100% controlable por la app | US-009 |
| NFR-26 | Progreso visible en examen largo | Durante la generación de un examen de 30 preguntas, no hay ningún tramo sin un mensaje de `IProgress<string>` mayor a 15 s reales de espera — verificable con timestamps de los `progreso?.Report` de una corrida | US-009 |
| NFR-27 | Mensaje accionable ante fallo agotado | Cuando la generación no completa la cantidad pedida tras agotar `MaxPeticionesPorExamen`/`MaxLotesEsteriles`, el mensaje final distingue explícitamente causa externa (cuota diaria de la clave) de causa app-controlable (truncado repetido por techo de tokens, formato inválido) y sugiere una acción distinta para cada una — verificable por inspección de texto en cada escenario simulado | US-009 |
| NFR-28 | Repetibilidad dentro de una sesión | Repetir la generación de un examen de 30 preguntas ya exitoso, mismo material/modelo/clave, en el mismo proceso en ejecución, usa una cantidad de peticiones igual o menor a la primera corrida (el techo de tokens ya aprendido no se vuelve a "perder") — verificable comparando el conteo de peticiones entre 2 corridas consecutivas del mismo proceso | US-009 |
| NFR-29 | Ausencia de rastro de IA en el árbol versionado | 0 archivos trackeados o por trackear (tras la limpieza) con menciones a "Claude"/nombres de herramientas de asistencia de IA, salvo el historial de commits ya excluido por 01-spec — verificable con una búsqueda de texto sobre el árbol completo, 0 resultados fuera de las excepciones documentadas | US-010 |
| NFR-30 | `.gitignore` efectivo para `.claude/` | Tras la corrección, `.claude/` (la carpeta completa) queda excluida de forma verificable (`git status --porcelain` no la lista; `git check-ignore -v .claude` devuelve una regla real, no la línea corrupta actual) | US-010 |
| NFR-31 | Cero regresión funcional por la limpieza | `dotnet build`/`dotnet test` quedan en el mismo estado (mismo conteo de éxitos) antes y después de la limpieza; `publish.yml`/`publicar.ps1`/`Verificar-Version.ps1` no cambian de comportamiento observable | US-010 |
| NFR-32 | Contraste texto/fondo | Proporción de contraste ≥ 4.5:1 (WCAG 2.1 AA, texto normal) entre `PincelTexto` y cada uno de `PincelFondo`/`PincelSuperficie`/`PincelTarjeta`, y ≥ 3:1 entre `PincelTextoSuave`/`PincelTextoTenue` y esos mismos fondos, en ambos temas — medible con cálculo estándar de luminancia relativa WCAG | US-011 |
| NFR-33 | Contraste de estados semánticos | Proporción de contraste ≥ 3:1 entre cada uno de `PincelAcierto`/`PincelError`/`PincelPendiente` y su fondo "Suave" correspondiente, y ≥ 3:1 entre esos tres colores fuertes entre sí (para distinguirse uno junto a otro, ej. en `BaldosaPregunta`) | US-011 |
| NFR-34 | Predominancia de morado | Al menos 3 tonos de morado perceptiblemente distintos (matiz dentro de ±20° de violeta/púrpura) presentes en la paleta activa de cada tema — hoy ya cumplido por `PincelMarca`/`PincelMarcaFuerte`/`PincelMarcaSuave`; si se suman tokens de superficie/borde con tinte morado, también cuentan — verificable por inspección de los valores `Color` de `Tokens.Claro.xaml`/`Tokens.Oscuro.xaml` | US-011 |
| NFR-35 | Paridad de claves entre temas | `Tokens.Claro.xaml` y `Tokens.Oscuro.xaml` exponen exactamente el mismo conjunto de claves después del cambio, ni una de más ni de menos en ninguno de los dos — verificable por diff de claves (mismo criterio que ya cumple la versión actual) | US-011 |
| NFR-36 | Cero color literal fuera de Tokens | 0 valores `Color="#..."`/`Background="#..."` nuevos en `Theme/Estilos.xaml` o en cualquier `Views/*.xaml` como consecuencia de este rediseño; todo color nuevo vive en `Tokens.Claro.xaml`/`Tokens.Oscuro.xaml` y se consume por `DynamicResource` — verificable por búsqueda de texto en el árbol de vistas | US-011 |

## Entidades de datos y relaciones

| Entidad | Campos relevantes | Persistencia | US origen |
|---|---|---|---|
| Cache de techo de tokens / estado de razonamiento por modelo (`_techoDeSalida`, `_topeTokensVigente`, `_razonamientoApagable` — ya existen en `GeminiApiService`) | modelo → `outputTokenLimit`; techo vigente global; flag de razonamiento apagable | Hoy: memoria del proceso (campos `static`), se pierde al reiniciar la app. Persistirlo en `AppConfig`/`config.json` (mecanismo `JsonStore` ya existente) es una mejora opcional para este incremento — no es obligatoria para cumplir los AC de US-009, queda a criterio de arquitecto-tecnico si entra ahora o en un incremento futuro | US-009 |
| Diagnóstico de generación (`DiagnosticoGeneracion`, ya existe) | hasta 12 notas de texto libre por examen | En memoria durante la generación; si el examen queda incompleto, se vuelca a `errores.log` vía `RutasApp.RegistrarError` (sin cambio) | US-009 |

No hay entidades nuevas para US-010 (housekeeping de archivos, no dato de negocio) ni para
US-011 (mismas 20 claves de `Tokens.*.xaml`, sin esquema nuevo) — casillas vacías
intencionales, mismo criterio que ya usó el incremento 2 para US-007/US-008.

## Contratos de integración de alto nivel

**US-009 — Generación con Gemini**
- Sin cambio de contrato HTTP externo: mismos endpoints `generateContent`/`ListModels` de
  Gemini, misma cabecera `x-goog-api-key`, mismo `responseSchema`. La única extensión posible
  de contrato es *cuándo* se llama `ListarModelosAsync` (ya existente, sin cambio de firma):
  hoy es reactivo (404) o manual (botón "Detectar modelos" en Ajustes); evaluar sumar una
  llamada proactiva antes del primer lote de una generación, cuando el modelo configurado no
  tiene techo cacheado todavía, reutilizando el mismo método — no se agrega un endpoint nuevo.
- Contrato interno con la capa de UI (sin cambio de firma, a confirmar que sigue cableado):
  `IProgress<string>` ya expone mensajes de plan/lote/rotación/espera en tiempo real — el
  ViewModel que invoca `GenerarPreguntasAsync` debe seguir consumiéndolos según van llegando
  (no solo mostrar el mensaje final) para cumplir el criterio "veo que avanza" de 01-spec.

**US-010 — Housekeeping de repo**
- Sin integración externa. Contrato: un checklist de limpieza reproducible (ver
  "Restricciones") aplicado sobre el árbol de trabajo antes de cada push; no altera ningún
  contrato de build/CI existente — `publish.yml`, `Verificar-Version.ps1`, `publicar.ps1` no
  cambian de comportamiento como consecuencia de esta limpieza.

**US-011 — Paleta morada/suave**
- Sin integración externa. Contrato interno: mismas claves de pincel + `SombraTarjeta` en
  `Tokens.Claro.xaml`/`Tokens.Oscuro.xaml` (agregar claves nuevas es una opción de diseño, no
  una obligación de este contrato — si se agregan, deben existir en ambos diccionarios, ver
  NFR-35), consumidas por `TemaService.Aplicar` sin cambios de mecanismo; `Theme/Estilos.xaml`
  sigue resolviendo esas claves por nombre vía `DynamicResource`, sin tocar su estructura de
  `ControlTemplate` más allá de los valores de color que ya referencia.

## Restricciones técnicas conocidas (específicas de este incremento)

- **Cuota gratuita de Gemini es un límite externo, no un defecto de la app**: ~10-15
  peticiones/minuto y ~20 peticiones/día por clave (cifras del propio comentario de
  `GeminiApiService.cs`, líneas 118-136) — la app ya respeta el ritmo por minuto
  (`SeparacionEntrePeticiones`, semáforo `Turno`) y mitiga la cuota diaria con rotación de
  claves (`AnilloDeClaves`), pero con una sola clave configurada un examen de 30 preguntas que
  necesite muchos lotes de relleno puede agotar la cuota diaria de esa clave en un solo
  intento. Esto se documenta como limitación externa en el mensaje de error (ver NFR-27), no
  se "arregla" en el código de la app.
- El aprendizaje en caliente (`_topeTokensVigente`, `_razonamientoApagable`, `_techoDeSalida`)
  es de campos `static` a nivel de proceso — cualquier cambio que lo persista entre sesiones
  debe reusar `AppConfig`/`JsonStore` (mecanismo ya existente, mismo criterio que US-003/US-005
  del incremento 1), no introducir un mecanismo de cache nuevo.
- `InstruccionDeSistema` y `EsquemaPreguntas()` ya están diseñados para respuestas compactas
  (12 palabras por opción, una oración por análisis/justificación, `propertyOrdering` para que
  un truncado igual rescate preguntas completas) — cualquier ajuste de prompt para "ahorrar
  tokens" debe mantener esa disciplina, no reintroducir texto largo.
- No tocar el mecanismo de lotes adaptativos, rotación de claves, backoff ni el semáforo de
  turno — ya cumplen su función y no son la causa raíz identificada (ver "Estado real del
  código"); reemplazarlos sería sobre-ingeniería contraria al lineamiento transversal de
  estilo (ver más abajo).
- US-010: no se reescribe el historial de commits ya existente en `main` (repetido de 01-spec
  a propósito, es una restricción dura, no una preferencia). `.gitignore` tiene hoy una línea
  corrupta que no excluye `.claude/` de forma efectiva (ver "Estado real del código") — corregir
  esa línea es parte de la limpieza, no un cambio de alcance.
- US-011: el mecanismo de tematizado (`TemaService`, intercambio de `ResourceDictionary` con
  prefijo `Tokens.`) es intocable, igual que fijó el incremento 1 — esta US solo cambia
  *valores* dentro de las claves existentes (y opcionalmente agrega claves nuevas si hacen
  falta para la variedad de morado, siempre en ambos diccionarios). La regla de
  `Theme/Estilos.xaml` ("ningún color literal vive en una vista") sigue vigente sin excepción.
  El violeta de marca no puede invadir los 6 pinceles semánticos de corrección
  (`PincelAcierto*`/`PincelError*`/`PincelPendiente*`) — el propio comentario de
  `Tokens.Oscuro.xaml` explica por qué esos tres significados no pueden competir con el acento
  de marca.

### Restricción transversal de estilo de código ("simple, estilo trainee")

Traducción concreta del lineamiento de 01-spec (Reglas de negocio, última viñeta) a las tres US
de este incremento:

- Preferir extender funciones/métodos ya existentes (ej. reusar `ListarModelosAsync` desde un
  punto de llamada nuevo) antes que introducir capas nuevas (interfaces, factories, inyección
  de dependencias) para resolver US-009.
- No sumar librerías de terceros nuevas para ninguna de las tres US — mismo criterio que ya
  aplicó el incremento 2 al descartar `Microsoft.Xaml.Behaviors` para US-007.
- Preferir código imperativo directo, con nombres explícitos y comentarios de "por qué" (mismo
  estilo que ya tiene `GeminiApiService.cs`/`Theme/Estilos.xaml`) antes que patrones de diseño
  formales (Strategy, Factory, Repository, motor de reglas) que no sean estrictamente
  necesarios para el alcance pedido.
- Evitar abstracciones genéricas "por si en el futuro" — ej. no construir un motor de limpieza
  configurable/extensible para US-010 cuando alcanza con un checklist puntual sobre archivos
  conocidos; no construir un sistema de theming paramétrico nuevo para US-011 cuando el
  mecanismo de tokens ya existente alcanza.
- Este lineamiento es una restricción de calidad, no un criterio de aceptación propio (así lo
  fija 01-spec): no genera un AC-T dedicado, pero condiciona cómo se leen todos los AC-T de
  esta sección — "cumple el AC" y "lo resuelve con el mecanismo más simple disponible" no son
  intercambiables entre sí.

## Decisiones de diseño que cierran ambigüedad

- **US-009 — enfoque de solución (diagnóstico → mitigación, sin implementar):**
  1. Antes de enviar el primer lote de un examen (no solo ante un 404), si el modelo
     configurado no tiene techo cacheado en `_techoDeSalida`, consultar `ListarModelosAsync`
     una vez y cachear el resultado — reusa el método existente, no agrega un endpoint nuevo.
     Si la consulta falla (red, clave sin permiso para `ListModels`), seguir con el default
     actual (8192) sin bloquear la generación: es una mejora del caso feliz, no un requisito
     que pueda hacer fallar un examen que hoy funciona.
  2. Evaluar persistir el aprendizaje (techo por modelo + flag de razonamiento) en
     `AppConfig`/`config.json` para que sobreviva entre sesiones — mejora opcional, no
     bloqueante para el DoD de este incremento (ver tabla de entidades).
  3. Cerrar `ArmarMensajeSinPreguntas`/`DescribirError` para que, cuando la causa dominante
     haya sido truncado repetido por techo de tokens (no cuota), lo diga con la misma
     claridad que ya tiene hoy el caso de cuota diaria vs. cuota por minuto, y sugiera
     "Ajustes → Detectar modelos de mi clave" como primera acción concreta.
  4. No tocar lotes adaptativos, rotación de claves, backoff ni semáforo de turno (ver
     "Restricciones técnicas") — la solución es hacer que el primer intento arranque mejor
     informado, no rehacer un mecanismo que ya funciona.
  5. Mantener (verificar que no se pierda) la distinción ya existente en `DescribirError` entre
     cuota diaria (límite externo de la cuenta de Google, "no se arregla esperando") y cuota
     por minuto (transitoria, sí se arregla esperando/reintentando) — es exactamente el tipo de
     mensaje accionable que pide el criterio 4 de US-009.

- **US-010 — checklist vital vs. no vital (aplicando la regla de negocio de 01-spec al estado
  real relevado):**

  | Candidato | Vital / No vital | Acción |
  |---|---|---|
  | `.claude/` (carpeta completa, hoy sin trackear) | No vital | No se trackea; corregir `.gitignore` para que quede efectivamente excluida |
  | Línea corrupta de `.gitignore` (`. c l a u d e / s k i l l s /`) | No vital (la línea en sí no cumple función) | Reemplazar por una entrada `.claude/` funcional, con codificación correcta |
  | `specs/*.md`, `specs/team-roster.yaml` | Vital (trazabilidad histórica del proyecto, exigido por 01-spec) | No se borran; solo se les quitaría mención a IA si la tuvieran — hoy no se encontró ninguna en los archivos relevados |
  | `.github/workflows/publish.yml`, `publicar.ps1`, `Verificar-Version.ps1` | Vital (necesarios para compilar/publicar) | No se tocan salvo mención de IA encontrada — hoy no se encontró ninguna |
  | Código de la app y tests (`AutoExam/**`, `AutoExam.Tests/**`) | Vital | Auditar comentarios por mención a IA (búsqueda de texto pendiente, ver AC-T33); nunca borrar/vaciar un archivo por esto |
  | Historial de commits ya existente en `main` | Fuera de alcance (no se toca) | Sin acción — confirmado tres veces en 01-spec (criterio, regla de negocio, "Fuera de alcance") |

- **US-011 — cómo tocar los tokens sin romper el mecanismo (dirección técnica, valores finales
  a cargo de quien implemente, con el piso de contraste de NFR-32/NFR-33 como condición de
  cierre):**
  1. Mantener las 20 claves actuales con su rol semántico intacto: no mover `PincelMarca*` a
     los pinceles de superficie, no tocar `PincelAcierto*`/`PincelError*`/`PincelPendiente*`
     más allá de un ajuste de contraste si hiciera falta (nunca de matiz — deben seguir
     leyéndose como verde/rojo/ámbar, no correrse hacia el morado).
  2. Para "más variedad de morado" (criterio de 01-spec), la vía más simple sin romper el
     contrato es sumar 1-2 claves nuevas de superficie/borde con un tinte violeta sutil (ej.
     `PincelFondo`/`PincelBorde` dejan de ser gris neutro y ganan un matiz apenas violáceo) —
     si se agregan, van en ambos diccionarios con los mismos nombres (NFR-35); si no se
     agregan claves nuevas, la alternativa es re-tonalizar las 6 claves de superficie/borde
     existentes hacia un gris con temperatura violeta, sin sumar claves.
  3. Para "más suave" (menos contraste duro), el margen ya existe: el contraste texto/fondo
     actual excede holgadamente el piso de WCAG AA (ver "Estado real del código") — bajar la
     luminancia diferencial entre `PincelTexto` y `PincelFondo`/`PincelTarjeta` tiene margen
     antes de cruzar el umbral de NFR-32, igual que suavizar la diferencia entre
     `PincelFondo`/`PincelTarjeta`/`PincelTarjetaHover`.
  4. Cerrar los valores finales validando contraste par por par (NFR-32/NFR-33) antes de
     considerar la US terminada — no es un paso posterior de QA únicamente, es parte del
     criterio de "cierre" de esta decisión de diseño.
  5. `SombraTarjeta` puede tintarse levemente hacia el morado en vez de gris/negro puro para
     reforzar la identidad (opcional, no forma parte de las 20 claves de contraste texto/fondo
     y no tiene requisito de accesibilidad asociado).

## Criterios de aceptación técnicos

**US-009**
- AC-T27 (US-009): dado un pedido de 1 a 30 preguntas con al menos una clave con cuota
  disponible, la generación entrega exactamente la cantidad pedida sin terminar en error por
  saturación, dentro del presupuesto `MaxPeticionesPorExamen` — trazable a 01-spec US-009,
  párrafo 1; ver NFR-25.
- AC-T28 (US-009): durante un examen de 30 preguntas, el progreso reportado llega a la UI en
  tiempo real sin tramos silenciosos mayores al umbral de NFR-26 — trazable a párrafo 2.
- AC-T29 (US-009): ante un 429 transitorio o una respuesta truncada durante un lote, el sistema
  reintenta/ajusta automáticamente (rotación de clave, backoff, reducción de lote) antes de
  fallar, y solo termina en error tras agotar `MaxPeticionesPorExamen`/`MaxLotesEsteriles` —
  trazable a párrafo 3 (mecanismo ya existente, sin regresión exigida).
- AC-T30 (US-009): agotados los reintentos, el mensaje final distingue causa externa (cuota
  diaria) de causa app-controlable (truncado por techo de tokens) y sugiere una acción concreta
  distinta por cada una — trazable a párrafo 4; ver NFR-27.
- AC-T31 (US-009): repetir la generación de un examen de 30 preguntas ya exitoso, mismo
  material y clave, vuelve a completarse sin fallar por el mismo motivo — trazable a párrafo 5;
  ver NFR-28.

**US-010**
- AC-T32 (US-010): al revisar el árbol de trabajo antes de resubir, ningún archivo trackeado
  (incluido `.claude/` una vez corregido `.gitignore`) identifica participación de un asistente
  de IA, salvo las excepciones ya autorizadas por 01-spec — trazable a criterio 1; ver
  NFR-29/NFR-30.
- AC-T33 (US-010): una búsqueda de texto (grep) sobre el árbol completo del repo por
  "Claude"/"Anthropic"/marcas equivalentes de asistencia de IA, ejecutada con una herramienta
  que sí liste/recorra directorios (no disponible en esta corrida — ver limitación en "Estado
  real del código"), da 0 resultados fuera de las excepciones documentadas antes de cerrar la
  limpieza — trazable a criterio 2; pendiente de ejecución, no de diseño.
- AC-T34 (US-010): el historial de commits ya existente en `main` no se toca (0
  `rebase`/`filter-branch`/force-push como parte de esta limpieza) — trazable a criterio 3.
- AC-T35 (US-010): ningún archivo vital (código, tests, workflows, scripts de publicación,
  specs congeladas) se borra ni se vacía; si tenía mención a IA, solo se le quita esa mención
  sin romper su función — trazable a criterio 4; ver checklist de "Decisiones de diseño".

**US-011**
- AC-T36 (US-011): en tema claro y en tema oscuro, el morado es el color predominante de
  acentos/marca/estados activos, con al menos 3 tonos distintos — trazable a criterio 1; ver
  NFR-34.
- AC-T37 (US-011): las pantallas principales (Libros, Nuevo examen, Examen, Historial,
  Ajustes, diálogos) se ven con la misma paleta tras el cambio, sin vistas que quedaron con un
  `DynamicResource` o clave de pincel distinta a la vigente — trazable a criterio 2.
- AC-T38 (US-011): el contraste texto/fondo y de estados semánticos cumple los umbrales de
  NFR-32/NFR-33 en ambos temas — trazable a criterio 3.
- AC-T39 (US-011): `PincelAcierto`/`PincelError`/`PincelPendiente` (y sus variantes "Suave")
  conservan su identidad semántica (siguen siendo distinguibles entre sí y no se corren hacia
  el morado) tras el rediseño — trazable a criterio 4.

## Definition of Done de este incremento

- Todo NFR tiene umbral medible: cumplido (tabla NFR de este incremento, columna "Umbral
  medible").
- Toda entidad de datos tiene US-XXX de origen: cumplido — las dos entidades de US-009 (cache
  de techo de tokens, diagnóstico de generación) están trazadas; US-010/US-011 no introducen
  entidades, documentado explícitamente en vez de omitido.

Sugerencia: antes de que arquitecto-tecnico/developer cierren US-010, correr una búsqueda de
texto real (grep/ripgrep) sobre todo el árbol versionado y no versionado por "Claude" y
equivalentes — esta spec relevó los archivos de proceso más probables (specs, workflow,
roster) sin encontrar menciones, pero no pudo auditar palabra por palabra los ~30 archivos de
código fuente por falta de una herramienta de búsqueda masiva en esta corrida (ver AC-T33).
