# Test plan — casos no obvios por módulo

Solo se documentan acá los casos edge que no se desprenden directo de los criterios de
aceptación de `specs/02-tech-spec.md`. Cobertura obvia (ida y vuelta directa de un AC) vive
solo en el código de test, no se duplica acá.

## US-005 — Tamaño de texto en examen (`test-dev-tamanio-texto-examen`)

### Contrato real (verificado contra la implementación de `dev-tamanio-texto-examen`)

`specs/03-architecture.md` §4.5 fija: 5 niveles (`AppConfig.TamanioTextoExamen`, `int 0..4`,
default `2`), que `ExamenViewModel` expone `TamanioTextoPregunta`/`TamanioTextoOpciones`
(`double`, pt), y que nivel 2 = 17pt/14pt. No fijaba de antemano cómo se calcula ni cómo se
setea el nivel; se arrancó contract-first asumiendo métodos estáticos puros, pero
`dev-tamanio-texto-examen` avanzó en paralelo y quedó así (verificado por lectura directa del
código, no por documentación previa):

- `NivelTextoExamen` (`int`, `[ObservableProperty]`, default `2`) — instancia, setable.
- `TamanioTextoPregunta`/`TamanioTextoOpciones` (`double`, solo lectura) — indexan tablas
  privadas `{13,15,17,20,23}` / `{11,12,14,16,18}` por `NivelTextoExamen`.
- `OnNivelTextoExamenChanged` persiste de inmediato: escribe `_sesion.Config.TamanioTextoExamen`
  y llama `GuardarConfig()` en cada cambio, no solo al cerrar (auto-guardado, no obvio desde el
  AC — importa para el test de "persiste sin esperar otra acción").
- `AumentarTextoExamenCommand`/`DisminuirTextoExamenCommand` (`RelayCommand`) con `CanExecute`
  bloqueado en los extremos 0 y 4.
- `CargarDesdeConfig()` (público) trae el nivel guardado y lo clampea a 0..4 — se llama después
  de `SesionUsuarioService.Cargar()` (mismo patrón que `AjustesViewModel.CargarDesdeConfig`).

Los tests de `ExamenTamanioTextoMapeoTests.cs` quedaron ajustados a esta API real en vez de a la
asumida originalmente.

### Matriz de cobertura

| AC / criterio | Caso | Nivel de test | Archivo |
|---|---|---|---|
| Mapeo nivel→pt (5 niveles) | Cada nivel 0..4 da un pt positivo | unit | `ExamenTamanioTextoMapeoTests.cs` |
| Mapeo nivel→pt | Nivel 2 = 17pt/14pt exacto (no romper el look por defecto) | unit | `ExamenTamanioTextoMapeoTests.cs` |
| Mapeo nivel→pt (edge, no obvio) | Tabla estrictamente creciente 0→4 (si dos niveles dieran el mismo pt, el ajuste no tendría efecto perceptible en ese salto) | unit | `ExamenTamanioTextoMapeoTests.cs` |
| Mapeo nivel→pt (edge, no obvio) | `CargarDesdeConfig()` con un valor fuera de 0..4 en `Config.TamanioTextoExamen` (ej. `99`, escrito a mano en el JSON) clampea sin excepción — mismo patrón de saneamiento que `SesionUsuarioService.Cargar()` ya aplica a `PreguntasPorLote`/`PaginasPorBloque` | unit | `ExamenTamanioTextoMapeoTests.cs` |
| Comandos de ajuste (edge, no obvio) | `Aumentar`/`DisminuirTextoExamenCommand` bloqueados (`CanExecute=false`) en los extremos 0 y 4 | unit | `ExamenTamanioTextoMapeoTests.cs` |
| Auto-guardado (edge, no obvio) | Cambiar `NivelTextoExamen` persiste en `AppConfig` de inmediato, sin esperar a cerrar el examen ni la app | unit | `ExamenTamanioTextoMapeoTests.cs` |
| `AppConfig.TamanioTextoExamen` default | Config nuevo sin persistencia previa trae nivel 2 | unit | `TamanioTextoExamenPersistenciaTests.cs` |
| AC-T14 — persistencia | Round-trip `JsonStore.Guardar`/`Cargar` conserva el nivel para los 5 valores válidos | unit (serialización aislada) | `TamanioTextoExamenPersistenciaTests.cs` |
| AC-T14 — persistencia (edge, no obvio) | `config.json` legacy sin la clave `TamanioTextoExamen` (escrito antes de US-005) cae al default sin romper la carga del resto de los campos | unit | `TamanioTextoExamenPersistenciaTests.cs` |
| AC-T14 — persistencia | Reabrir la app (nueva instancia de `SesionUsuarioService`, no el mismo objeto en memoria) conserva un nivel distinto del default | integración | `TamanioTextoExamenPersistenciaTests.cs` |
| AC-T13 — sin scroll horizontal ni reflow perceptible | — | **fuera de alcance**: validación visual, responsabilidad de QA (ver `team-roster.yaml`) | — |

### Por qué el test de integración (`SesionUsuarioService`) no duplica el de unidad (`JsonStore`)

El test de `JsonStore` aísla el contrato de serialización del campo en sí (tipo/nombre/default),
sin pasar por rutas de disco reales ni por lógica de saneamiento. El test de
`SesionUsuarioService` reproduce el camino real que usa la app (`Cargar`/`GuardarConfig` contra
`RutasApp.ArchivoConfig`) para atrapar bugs de wiring que el de unidad no puede ver (por ejemplo,
que algún saneamiento nuevo en `Cargar()` pise el valor guardado, o que `GuardarConfig()` no
llegue a invocarse). Ambos existen porque cada uno prueba una capa distinta del mismo AC-T14.

### Riesgo conocido (no de este módulo)

Al momento de escribir estos tests, `dotnet test AutoExam.sln` no compila el conjunto de la
solución por trabajo en curso en paralelo de otros módulos (US-002/US-003, ver
`team-roster.yaml`: `dev-dialogos-tema`, `dev-ventana-navegacion`) — no por código de US-005.
Los dos archivos de este módulo se revisaron manualmente contra las firmas reales ya existentes
en el repo (`AppConfig`, `JsonStore`, `SesionUsuarioService`, `RutasApp`) y solo referencian
símbolos nuevos (`AppConfig.TamanioTextoExamen`, `ExamenViewModel.PuntosPregunta/PuntosOpciones`)
que son exactamente lo que falta implementar — ese es el "rojo" esperado de este módulo, no un
bug de este archivo.

## US-009 — Confiabilidad de generación con Gemini (`test-dev-generacion-gemini`)

### Contrato real (verificado contra la implementación de `dev-generacion-gemini`)

Contract-first sobre `specs/03-architecture.md` §4.1: al momento de escribir esta suite,
`dev-generacion-gemini` ya había aterrizado en paralelo los dos campos nuevos de
`DiagnosticoGeneracion` (`LotesTruncados`, `CuotaDiariaDetectada`), la consulta proactiva a
`ListarModelosAsync` insertada en `GenerarPreguntasAsync` y las dos oraciones de
`ArmarMensajeSinPreguntas` — verificado por lectura directa (`git diff`), no por suposición; los
nombres, tipos y el texto exacto de las oraciones coinciden con el contrato ya cerrado en
arquitectura, sin ajustes.

`ArmarMensajeSinPreguntas` y `CalcularTopeTokens` siguen siendo `private static` (a propósito, ver
"Restricción transversal de estilo" del tech-spec — no se sube su visibilidad solo para poder
testearlas). Se invocan por reflection desde `GeminiApiServiceReflexion` en vez de pedirle al
developer una interfaz/abstracción nueva.

### Matriz de cobertura

| AC / criterio | Caso | Nivel de test | Archivo |
|---|---|---|---|
| Contrato de `DiagnosticoGeneracion` | `Resumen()`/`Registrar()` (sin notas, una nota, varias, tope de 12, nota vacía/nula ignorada) — comportamiento ya existente, sin regresión | unit | `DiagnosticoGeneracionTests.cs` |
| Contrato de `DiagnosticoGeneracion` (campos nuevos) | `LotesTruncados`/`CuotaDiariaDetectada` — default (`0`/`false`) y settable público | unit | `DiagnosticoGeneracionTests.cs` |
| AC-T30 / NFR-27 | `CuotaDiariaDetectada=true` antepone la oración de cuota externa exacta | unit (reflection sobre método privado) | `GeminiApiServiceArmarMensajeSinPreguntasTests.cs` |
| AC-T30 / NFR-27 | `LotesTruncados>0` sin cuota antepone la oración de techo de tokens exacta | unit | `GeminiApiServiceArmarMensajeSinPreguntasTests.cs` |
| AC-T30 (edge, no obvio) | Cuota **y** truncado ocurren juntos → prioriza la oración de cuota, no concatena ni muestra la de truncado (arquitectura lo exige explícito: "cuota prioriza sobre truncado") | unit | `GeminiApiServiceArmarMensajeSinPreguntasTests.cs` |
| AC-T30 (edge, no obvio) | Ninguna causa detectada → el encabezado genérico actual no cambia (no se antepone ninguna oración nueva) — evita que un futuro refactor rompa el mensaje "de siempre" | unit | `GeminiApiServiceArmarMensajeSinPreguntasTests.cs` |
| — | El mensaje final sigue incluyendo modelo y `Resumen()` del diagnóstico tras el cambio | unit | `GeminiApiServiceArmarMensajeSinPreguntasTests.cs` |
| NFR-24 | Sin techo cacheado, `CalcularTopeTokens` usa el default (8192) | unit | `GeminiApiServiceCalcularTopeTokensTests.cs` |
| NFR-24 | Con techo cacheado (vía `ParsearListaModelos`, sin red) por debajo del máximo de la app, lo usa tal cual | unit | `GeminiApiServiceCalcularTopeTokensTests.cs` |
| NFR-24 (edge, no obvio) | Techo cacheado por encima de 16384 (`TopeTokensMaximo`) topea ahí — un modelo nuevo con techo de 65536 no debe pedir "de más" | unit | `GeminiApiServiceCalcularTopeTokensTests.cs` |
| NFR-28 (edge, no obvio) | Tope aprendido en caliente (`_topeTokensVigente`, más bajo que el techo del modelo) sigue ganando — simulado fijando el campo `static` por reflection, sin disparar el 400 real que lo produce | unit | `GeminiApiServiceCalcularTopeTokensTests.cs` |
| NFR-28 | `ReiniciarAprendizajeDeSesion()` vuelve a dejar que gane el techo del modelo (ya no el tope aprendido) | unit | `GeminiApiServiceCalcularTopeTokensTests.cs` |

### Fuera de alcance de esta suite (documentado, no forzado)

- **AC-T27 / AC-T29 / AC-T31** (tasa de éxito real de un examen de 1 a 30 preguntas contra la API
  real, reintento en vivo ante 429/truncado, repetibilidad entre corridas): el propio NFR-25 del
  tech-spec acepta como alternativa válida "la API real o un doble que simule truncado/429 según
  defina QA". `GeminiApiService.BaseUrl`/`SeparacionEntrePeticiones`/`EsperaBaseReintento` son
  `public static` justo para habilitar ese doble (comentario del propio código: "settable solo
  para que las pruebas..."), pero levantar un servidor local que reproduzca correctamente Files
  API + `generateContent` con el `responseSchema` completo (para no ser un "mock frágil" que se
  desincroniza del contrato real de Gemini) excede el alcance proporcional de este incremento.
  Queda como corrida manual/QA contra la API real antes de firmar US-009 (ver
  `specs/03-architecture.md`, Incremento 3, riesgo R-10, y sugerencia al final de ese documento).
- **Verificación end-to-end del punto de inserción de la consulta proactiva** (que
  `GenerarPreguntasAsync` efectivamente llame a `ListarModelosAsync` antes del primer lote, no
  solo que `CalcularTopeTokens` use bien lo que ya está cacheado): mismo motivo que el punto
  anterior — requiere el mismo doble HTTP. El wiring se revisó por lectura de código (`git diff`,
  15 líneas, sin ramas ocultas) en vez de por test automatizado.
- **NFR-25 (≥95% de éxito en 20 corridas)**: es una métrica estadística contra el modelo real de
  Gemini, no una propiedad determinística de una función pura — no tiene un test xUnit equivalente
  razonable; corresponde a QA con corridas reales documentadas.

### Por qué no se duplica ArmarMensajeSinPreguntas contra el flujo completo de `GenerarPreguntasAsync`

El método privado ya aísla toda la lógica de texto que exige AC-T30 (prioridad de causa, oraciones
exactas, encabezado sin cambios). Probarlo también end-to-end (vía un doble HTTP que fuerce cuota
o truncado real) solo repetiría el mismo aserto de texto pagando el costo de mantener un servidor
falso — motivo por el que se dejó fuera de alcance arriba en vez de duplicarlo "por las dudas".
