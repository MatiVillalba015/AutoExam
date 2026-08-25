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
