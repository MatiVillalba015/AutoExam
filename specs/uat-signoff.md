# UAT Sign-off — Incremento "Comodidad de interfaz + actualización automática"

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
