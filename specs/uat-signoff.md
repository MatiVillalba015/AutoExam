# UAT Sign-off — Incremento "Comodidad de interfaz + actualización automática"

## Incremento 3 (US-009 a US-011) — UAT final — 2026-08-28

Build: working tree sin commitear (sobre commit `370cc65`), sin push. Contra
`specs/01-spec.md` (sección Incremento 3) y `specs/qa-report.md` (las tres secciones más
recientes: US-009, US-010, US-011, las tres cerradas "QA OK" el 2026-08-28). Complementa la
validación inicial de spec ya registrada arriba en este mismo archivo (2026-08-27, APROBADO
sin cambios) — este es el sign-off sobre el build entregado, no una repetición de esa lectura.

### Veredicto: APROBADO

Los tres módulos cumplen los criterios de aceptación de negocio de `01-spec.md` tal como
fueron aprobados al inicio del ciclo. No se improvisó ningún criterio nuevo en este UAT.

### Decisiones de negocio ya cerradas en este ciclo — no se reabren

- **Historial de `main` reescrito** (filter-branch + force-push, sesión previa no relacionada a
  este incremento): decisión explícita ya tomada por el usuario de dejarlo como está. `qa-report.md`
  (US-010, AC-T34) confirma además que este incremento no ejecutó ningún comando destructivo de
  historial adicional — el housekeeping de US-010 es enteramente sobre el working tree.
- **Colores semánticos de estado (Acierto/Error/Pendiente) más vívidos/saturados de lo que
  "más suavidad" sugeriría**: excepción intencional ya decidida por el usuario, por no haber
  margen matemático para suavizarlos sin romper el contraste mínimo 3:1 (NFR-33). Confirmado por
  inspección directa de `Tokens.Claro.xaml`/`Tokens.Oscuro.xaml`: los 6 pinceles semánticos
  (`PincelAcierto(Suave)`, `PincelError(Suave)`, `PincelPendiente(Suave)`) mantienen su matiz
  original (verde/rojo/ámbar) sin correrse hacia el violeta, tal como exige AC-T39 — la excepción
  es de saturación/luminancia, no de identidad de color ni de alcance del rediseño.

### Verificación por US (criterios de aceptación de negocio, `01-spec.md`)

| US | Criterio de negocio | Veredicto | Evidencia |
|---|---|---|---|
| US-009 | Examen de 1 a 30 preguntas se genera completo sin fallar por saturación; progreso visible durante la generación; reintento/ajuste automático ante error transitorio; mensaje entendible y accionable si agotados los reintentos igual falla; comportamiento repetible, no parche de una sola corrida | Cumple | QA: 21/21 tests verdes sobre el contrato de `DiagnosticoGeneracion`, prioridad de causa en `ArmarMensajeSinPreguntas` (cuota > truncado > genérico) y `CalcularTopeTokens` con aprendizaje en caliente. Código releído: consulta proactiva a `ListModels` antes del primer lote, contador de lotes truncados, `catch` específico de cuota diaria que corta con lo ya generado en vez de perder el diagnóstico. Sin regresión en el módulo (0 fallos de `GeminiApiService`/`DiagnosticoGeneracion` en la suite completa) |
| US-010 | Repo sin archivos/carpetas/metadata que identifiquen participación de IA (salvo lo vital); código y config sin menciones a Claude en comentarios/metadata; historial de commits ya existente no se toca; archivo vital con rastro de IA no se borra, solo se le quita la mención | Cumple | QA: `.claude/` efectivamente ignorada (`git check-ignore -v` sobre la regla real, no la línea corrupta anterior); grep case-insensitive sobre todo el árbol de trabajo sin coincidencias de autoría de IA fuera de las specs que documentan el propio proceso de housekeeping; `git log` sin commits nuevos de reescritura de historial; `.gitignore` es el único archivo tocado por este módulo, sin borrar ni vaciar nada vital |
| US-011 | Morado predominante en ambos temas; consistencia en todas las pantallas; contraste suficiente pese a "más suave"; estados con significado (correcto/incorrecto, error/aviso) se siguen distinguiendo | Cumple | QA: 73/73 tests de contraste en verde cubriendo NFR-32 a NFR-35 y AC-T39; inspección propia de `Tokens.Claro.xaml`/`Tokens.Oscuro.xaml` confirma 6 claves de superficie/borde con tinte violeta + 3 tonos de morado en `PincelMarca*`, y 0 archivos de `AutoExam/Views/` tocados (mecanismo `DynamicResource` ya existente propaga el cambio a toda pantalla por construcción, sin superficie para parches sueltos) |

### Alcance de archivos tocados — confirmado por lectura directa

Revisión propia (no solo la de QA) de los tres archivos prometidos por el incremento:
`AutoExam/Services/GeminiApiService.cs` (US-009), `.gitignore` (US-010),
`AutoExam/Theme/Tokens.Claro.xaml` + `Tokens.Oscuro.xaml` (US-011). El contenido de cada uno
coincide con lo descrito en `01-spec.md` y `qa-report.md`: sin tocar lotes adaptativos,
rotación de claves ni backoff en `GeminiApiService.cs`; sin tocar código/tests/workflows en
`.gitignore` (solo la línea `.claude/`); sin tocar layout ni mecanismo de tematizado en los
tokens de tema.

### Hallazgos no bloqueantes (ya conocidos y aceptados, no requieren nueva US)

- US-009: gaps de cobertura automatizada contra la API real de Gemini en vivo (caso feliz
  1-30 preguntas, reintento ante error transitorio, repetibilidad end-to-end) — requieren gastar
  cuota real de la clave del usuario, explícitamente descartado por costo/beneficio en
  `test-plan.md`. Verificado por inspección de código y por los tests unitarios que sí cubren
  cada pieza del mecanismo por separado. Riesgo residual bajo: la causa raíz identificada
  (techo de tokens arrancando conservador) queda mitigada por la consulta proactiva a
  `ListModels`, y el resto del mecanismo de reintento/relleno no se tocó respecto a lo que ya
  funcionaba antes de este incremento.
- US-011: sin validación visual interactiva en vivo en esta corrida de QA (limitación de
  entorno ya documentada en corridas previas del proyecto) — el veredicto se apoya en tests de
  contraste automatizados más inspección directa de los 20 valores hex de cada tema, cobertura
  suficiente para un cambio acotado a valores de color sobre un mecanismo de tematizado que no
  cambió.

### Alcance no evaluado (correctamente fuera de `01-spec.md`, sin acción)

Sin cambio de stack, sin exámenes de más de 30 preguntas, sin cambio de proveedor de IA, sin
reescritura de historial de commits, sin borrado de archivos vitales, sin cambio de layout/
iconografía/tipografía, sin migración del mecanismo de tematizado, sin animaciones nuevas —
todo confirmado fuera de alcance según "Fuera de alcance" de `01-spec.md` y no tocado por el
código relevado.

Sugerencia: correr manualmente 2-3 exámenes de 30 preguntas seguidos con la clave real del
usuario antes de considerar cerrado el gap de evidencia en vivo de US-009 (ya sugerido por QA);
no es condición para este sign-off.

---

## Validación inicial de spec — Incremento 3 (US-009 a US-011) — 2026-08-27

Corrida en paralelo al relevamiento técnico (no bloquea a analista-técnico ni al equipo de
build). Contra `specs/01-spec.md` tal como quedó actualizado con este incremento.

### Veredicto: APROBADO — sin cambios

El spec refleja fielmente el pedido original en los tres frentes, son independientes entre sí
como indica el propio documento, y cada uno tiene criterios de aceptación testeables en
formato Given/When/Then.

**US-009** (saturación de Gemini al pedir hasta 30 preguntas): criterios cubren el caso feliz
(1-30 completo), progreso visible durante una generación larga, reintento/ajuste automático
ante error transitorio de cuota, mensaje entendible (no técnico) si agotados los reintentos
igual falla, y repetibilidad (no es un parche de una sola corrida). Cubre el síntoma real
reportado por el usuario sin prometer de más (techo explícito en 30, con nota abierta no
bloqueante para el caso de que el límite sea de cuota de la cuenta y no de la app).

**US-010** (repositorio sin rastro de IA antes de resubir): confirmo que el alcance documentado
coincide exactamente con lo que ya definí como stakeholder — limpieza de archivos/carpetas no
vitales (`.claude/`, comentarios, metadata) y comportamiento hacia adelante en los commits
nuevos (sin coautoría de IA), **sin tocar el historial de commits ya existente**. Esto queda
explícito y sin ambigüedad en tres lugares del documento (criterio de aceptación 3, regla de
negocio, y "Fuera de alcance"), más la distinción clara "vital vs. no vital" que evita que la
limpieza rompa código/tests/workflows que sí son necesarios para el funcionamiento del proyecto.
No hace falta ninguna corrección acá.

**US-011** (paleta morada/suave): criterios cubren predominancia del morado en ambos temas,
consistencia en todas las pantallas (no parches sueltos), legibilidad/contraste suficiente pese
a ser "más suave", y que los estados con significado (correcto/incorrecto, error/aviso) sigan
distinguiéndose. Correctamente acotado a color únicamente — "Fuera de alcance" excluye layout,
iconografía, tipografía y el mecanismo de tematizado en sí, que no fueron parte del pedido.

**Lineamiento transversal "código simple, estilo trainee":** está documentado como restricción
de calidad no funcional en "Reglas de negocio" (última viñeta) y ampliado en "Supuestos", con la
aclaración correcta de que simplicidad no es excusa para bajar la robustez que pide US-009
("confiable y simple no son contradictorios"). Aplica por igual a las tres historias, tal como
se pidió.

**Alcance:** no encuentro alcance de más ni de menos respecto al pedido real. Los tres frentes
están correctamente delimitados entre sí y respecto a los incrementos anteriores (US-001 a
US-008, que este documento no reabre).

### Pendiente

Falta el build de este incremento (US-009 a US-011) y el UAT final correspondiente, que se
agrega como nueva sección de este mismo archivo cuando el equipo entregue — igual que en los
incrementos 1 y 2.

---

## Incremento 2 (US-006 a US-008) — 2026-08-26

Build: working tree sin commitear (sobre commit `370cc65`), sin push — por instrucción explícita
del usuario para esta corrida. Contra `specs/01-spec.md` (US-006 a US-008).

**Nota de entorno:** no tengo herramienta para ejecutar `dotnet run` ni interactuar con la UI en
esta corrida — el sign-off se basa en `specs/01-spec.md` (criterios de negocio ya cerrados) y
`specs/qa-report.md` (las dos entradas más recientes, 2026-08-26), no en una revisión visual propia
de la app corriendo.

## Veredicto: APROBADO

Satisface el pedido original: "mantener el stack de C#" (sin cambios de stack, confirmado en
Fuera de alcance) + verificación de versión antes de pushear (US-006) + más animaciones en el
front-end para una experiencia más agradable (US-007 transición de sección, US-008 feedback de
hover/presión).

## Verificación por US (criterios de aceptación de negocio, 01-spec.md)

| US | Criterio de negocio | Veredicto | Evidencia |
|---|---|---|---|
| US-006 | Antes de pushear, confirmo simple y explícito si `<Version>` ya subió respecto a `update.xml`; si no subió, aviso claro de que ese push no publica; si subió, confirma que sí; no bloquea ni reemplaza a US-001 | Cumple | QA exploratorio manual contra los 4 Given/When/Then con el script real (`Verificar-Version.ps1`), fixtures propias: mensaje verde exit 0 cuando sube, mensaje amarillo exit 1 cuando es igual o menor, sin efectos secundarios (`git status` idéntico antes/después), `-EmitGithubOutput` produce los mismos 4 outputs que ya consume `publish.yml` sin duplicar lógica |
| US-007 | Cambio entre secciones principales con transición breve y suave, respeta tema desde el primer instante, no se traba con navegación rápida, no afecta navegación pregunta-a-pregunta de un examen | Cumple | QA por lectura de código: fade+slide 220 ms sobre el único `ContentControl` del shell; animación solo toca `Opacity`/`TranslateTransform` (nunca color); `BeginAnimation` reemplaza clock anterior por construcción (no hay superposición posible); confirmado por grep que `TransicionContenido` no llega a `ExamenView` |
| US-008 | Hover y presión con animación sutil y distinguible entre sí en chips/tarjetas/ítems/zona de soltar/chips de acción; respeta paleta del tema; elemento deshabilitado no anima | Cumple | QA por lectura de código: 7 templates con overlay de opacidad (hover, 140 ms) y escala (presión, 80 ms) — visualmente distintos; 0 colores/pinceles nuevos, solo `Opacity` sobre pinceles `DynamicResource` ya existentes; guardia `IsEnabled` explícita en los 7 templates |

## Hallazgos no bloqueantes (ya conocidos, no requieren nueva US)

- US-006: los 22 tests de `AutoExam.Tests/Scripts/VerificarVersionScriptTests.cs` fallan en la
  máquina de QA porque el proceso hijo apunta a `pwsh` y esa máquina no tiene PowerShell 7
  instalado — QA confirmó que la lógica y los mensajes coinciden exactamente con lo exigido por
  esos tests corriendo el script a mano. Es un gap de entorno de CI/máquina de QA, no un defecto
  de comportamiento; no afecta el criterio de negocio. Corre bajo criterio de devops confirmar que
  el runner que ejecute esos tests en CI tenga `pwsh` disponible.
- US-008: hoy ningún ViewModel deshabilita en vivo ninguno de los 7 elementos con animación nueva
  (`Chip`, `ChipAccion`, `OpcionExamen`, `BaldosaPregunta`, `ItemLibro`, `ZonaSoltar`,
  `ItemNavegacion` es la única excepción parcial) — el criterio "elemento deshabilitado no anima"
  quedó verificado por inspección de código, no ejercitado en vivo, porque no existe hoy un caso
  real de opción bloqueada en pantalla. No es un defecto de este incremento (no se pidió agregar
  bloqueo de opciones); riesgo residual bajo, mismo patrón ya aceptado en el ciclo anterior para
  US-004.
- QA no pudo hacer exploración visual interactiva en vivo de US-007/US-008 (hover/press/temas) en
  esta corrida por indisponibilidad de la pantalla física de la máquina; sí confirmó arranque sin
  excepción del `.exe`. Aceptado como riesgo residual bajo, mismo criterio de "cobertura por
  lectura de código + evidencia de arranque" ya usado en corridas previas de este proyecto.

## Alcance no evaluado (correctamente fuera de 01-spec, sin acción)

Sin rediseño de paleta/tipografía, sin animaciones fuera de transición de sección y hover/presión
(spinner, confeti, splash), sin decisión automática de qué número subir de versión, sin bloqueo
técnico del push — todo confirmado fuera de alcance según "Fuera de alcance" de `01-spec.md` y no
tocado por el código relevado.

Sugerencia: primer push real que incluya una suba de versión (ej. `1.0.2` → `1.0.3`) para validar
`Verificar-Version.ps1` contra el `.csproj`/`update.xml` reales del repo, ya que QA lo probó solo
contra fixtures propias.

---

## Incremento 1 (US-001 a US-005)

Build: commit `8aeed90` (local, sin push). Contra `specs/01-spec.md` (US-001 a US-005).

## Veredicto: APROBADO

Se autoriza a devops a dejar todo listo para que el usuario haga el push manual (deploy real
de este incremento).

## Verificación por US (criterios de aceptación de negocio, 01-spec.md)

| US | Criterio de negocio | Veredicto | Evidencia |
|---|---|---|---|
| US-001 | Push con `<Version>` mayor dispara compilar→test→Release→verificar descarga→`update.xml` (commit+push), en ese orden; sin cambio de `<Version>` no pasa nada; fallo en cualquier paso no publica ni toca `update.xml`, y queda visible | Cumple | `.github/workflows/publish.yml` implementa los 11 pasos en el orden exacto de los AC; `should_publish` como gate; `paths-ignore: update.xml` + `[skip ci]` evita auto-disparo; `ActualizacionService` no se tocó |
| US-002 | Confirmaciones/avisos/errores de la app usan tema propio, no MessageBox de Windows; acciones irreversibles siguen pidiendo confirmación explícita | Cumple | `DialogoService` reemplaza `MessageBox.Show` por `DialogoVentana` (Fluent/Mica) vía `DynamicResource` de `Theme/Tokens.*`; `IDialogos` sin cambio de firma |
| US-003 | Ventana recuerda tamaño/posición/estado; si queda fuera de pantalla, centra | Cumple | Re-verificado en vivo por QA contra el `.exe` real en 3 escenarios (incluido dato corrupto y fuera de pantalla), con `errores.log` vacío |
| US-004 | Atajo de teclado navega entre secciones sin interferir con atajos de examen ni dispararse en campos de texto | Cumple | Re-verificado en vivo por QA con UI Automation (`Ctrl+3`/`Ctrl+5`, guard de foco en `TextBox` confirmado). Gap de cobertura documentado por QA (atajos durante examen en curso real, sin red/clave) es riesgo residual bajo, no defecto — aceptado |
| US-005 | Tamaño de texto de pregunta/opciones ajustable y persistente | Cumple | 5 niveles (`PuntosPregunta`/`PuntosOpciones`), nivel 2 = 17/14pt (comportamiento actual sin cambios), `TamanioTextoExamen` persistido en `AppConfig` con clamp 0-4 |

## Hallazgos no bloqueantes (ya conocidos, no requieren nueva US)

- `MessageBox.Show` nativo sigue en `App.xaml.cs` (excepción no controlada) y en
  `MainWindow.Ventana_Loaded` (fallo catastrófico de inicialización): decisión de arquitectura
  documentada (`03-architecture.md` §3) y razonable — un diálogo que depende del mismo pipeline
  que acaba de fallar no sirve como red de seguridad. No es un incumplimiento de US-002 (esos
  casos no son "confirmación/aviso/error de la app" en operación normal, son fallback de arranque).
- Gap de cobertura de test en vivo para "atajos de navegación durante examen en curso" (US-004):
  aceptado como riesgo residual bajo por inspección de código, según QA.

## Condición pendiente para que US-001 funcione en producción (no bloquea este sign-off)

**R-1** (documentado en `specs/03-architecture.md` §6 y `specs/04-devops-runbook.md` §2): antes
del primer push real que deba disparar el pipeline, el usuario debe confirmar manualmente en
GitHub → `Settings → Actions → General → Workflow permissions` = "Read and write permissions", y
que `main` no tenga branch protection que bloquee el push directo de `github-actions[bot]`. Es un
paso de configuración de plataforma, no de código, y no lo puede resolver el pipeline por sí
mismo. Se aprueba el incremento con esta condición pendiente de acción del usuario, no como
defecto del build.

## Alcance no evaluado (correctamente fuera de 01-spec, sin acción)

Sin cambios a `GeminiApiService`, `EvaluadorUBA`, `PdfExtractorService`; sin MSI/MSIX; sin
sincronización entre PCs; sin multi-idioma — todos confirmados fuera de alcance según
"Fuera de alcance" de `01-spec.md` y no tocados por el código relevado.

Sugerencia: primer push real de prueba con una suba de versión de patch (ej. `1.0.2` → `1.0.3`),
según ya sugiere `specs/03-architecture.md`, para validar R-1 en un entorno real antes de confiar
el flujo a una publicación con contenido de negocio real.
</content>
