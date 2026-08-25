# 04 — Pipeline CI/CD, infraestructura y runbook de deploy

Entrada: `specs/03-architecture.md` §1, §4.1, §6. Artefacto de pipeline:
`.github/workflows/publish.yml`.

## 1. Infraestructura como código

No hay infraestructura de cómputo propia que provisionar (sin VM/K8s/cloud): el
"ambiente" de este pipeline es 100% nativo de GitHub (runner `windows-latest`
efímero + GitHub Releases como storage de artefactos). Toda la infraestructura
reproducible vive en:

- `.github/workflows/publish.yml` — definición completa del pipeline (trigger,
  permisos, pasos), versionada en el repo.
- `AutoExam/AutoExam.csproj` — target de publish (`RuntimeIdentifier win-x64`,
  self-contained single-file) ya versionado, sin cambios de este rol.

Único punto que **no** es código versionable (limitación de la plataforma, no
decisión de diseño): la configuración de permisos del repo
(`Settings → Actions → General → Workflow permissions`) y el branch protection
de `main` viven en la configuración de GitHub, no en un archivo del repo salvo
que se instale la GitHub App "Settings" — fuera de alcance para este cambio
puntual. Es el único paso de consola inevitable y queda documentado como
prerequisito en el runbook (§2) y en el header del propio YAML.

## 2. Runbook de deploy

El pipeline (`publish.yml`) es 100% automático desde `git push` a `main` con un
`<Version>` nuevo en `AutoExam/AutoExam.csproj` — ver los 11 pasos comentados
en el propio archivo. Estos son los únicos pasos que **no** puede hacer el
pipeline por sí solo:

| # | Paso | Por qué no se automatiza | Frecuencia |
|---|---|---|---|
| 1 | Confirmar en GitHub → `Settings → Actions → General → Workflow permissions = Read and write permissions`, y que `main` no tenga branch protection que bloquee push directo de `github-actions[bot]`. | Configuración de plataforma a nivel repo (R-1), no expuesta como archivo versionable sin instalar tooling adicional (fuera de alcance). | Una sola vez, antes del primer run real. |
| 2 | Subir `<Version>` en `AutoExam/AutoExam.csproj` y hacer `git push` a `main`. | Es la decisión humana de "hay una versión lista para publicar" — el pipeline no decide subir versión por sí mismo. | Cada release. |
| 3 | Si el run falla, revisar la pestaña **Actions** del repo y decidir si se reintenta (re-run) o se corrige código y se vuelve a pushear. | Diagnóstico de causa raíz requiere criterio humano; no hay auto-retry por diseño (evita loops de Releases fallidos). | Solo ante fallo. |

Nada más requiere intervención manual: compilar, testear, verificar versión de
binario, empaquetar, crear el Release, verificar HTTP 200 del asset y
actualizar+commitear `update.xml` corren dentro del job (`publish.yml`,
pasos 4-11).

## 3. Rollback

En 3 pasos o menos, sin herramientas nuevas:

1. En GitHub → **Releases**, marcar la versión defectuosa como "pre-release"
   o borrarla (deja de listarse como release público, pero no rompe URLs de
   descarga existentes que ya haya distribuido el manifiesto viejo).
2. `git revert <commit-de-update.xml>` sobre `main` (o editar `update.xml` a
   mano con la versión/URL/changelog anteriores) y `git push` — esto es lo
   único que necesitan los clientes ya instalados para dejar de ver el aviso
   de actualización rota, porque `ActualizacionService` solo lee
   `update.xml`, no el Release en sí (contrato intocable, ver
   `specs/03-architecture.md` §2).
3. Si el binario en sí tiene un bug (no solo el manifiesto), repetir el flujo
   normal: subir `<Version>` con el fix y dejar correr el pipeline — no hay
   "downgrade" de versión porque `AutoUpdater.NET` no soporta ofrecer una
   versión menor a la ya instalada.

Nota: el paso 9 del pipeline (`update.xml` no se toca si el asset no
responde 200/206) ya previene la causa más común de rollback (Release roto
publicado antes que su asset) — este rollback cubre el caso residual de un
binario que sí descarga pero tiene un defecto funcional.

## 4. Bloqueos abiertos entregados por este rol

- **R-1** (`specs/03-architecture.md` §6): permisos de escritura del repo y
  branch protection sin confirmar. No verificable desde este entorno (sin
  `gh` autenticado contra el repo real). Ver §2, fila 1.
- **R-4**: resuelto. `AutoExam.sln`/`AutoExam.Tests` ya existen (entregados
  en paralelo por `test-dev-actualizacion`) y `dotnet test AutoExam.sln
  --configuration Release` corre en verde localmente (23/23) al momento de
  este commit.

## 5. Sanity final pre-push (2026-08-25, post sign-off UAT)

Validación de cierre sobre el estado actual de `main` (local, `uat-signoff.md`
aprobado, sin push todavía). Confirma que `publish.yml` está listo para correr
apenas el usuario haga push, sin cambios de código de este rol:

| Chequeo | Resultado |
|---|---|
| `dotnet build AutoExam.sln` | 0 Advertencias, 0 Errores |
| `dotnet test AutoExam.sln --configuration Release` | 94/94 correctas, 0 con error |
| `AutoExam/AutoExam.csproj` → `<Version>` | `1.0.2` |
| `update.xml` → `<version>` | `1.0.1` |
| `1.0.2 > 1.0.1` (gate `should_publish` del paso 2-3 de `publish.yml`) | Verdadero → el próximo push a `main` SÍ publica (no es un no-op) |
| `env.REPO` en `publish.yml` (`MatiVillalba015/AutoExam`) vs. `git remote -v` | Coinciden |
| `env.CSPROJ` / `env.PUBLISH_DIR` en `publish.yml` vs. `TargetFramework`/`RuntimeIdentifier` reales del `.csproj` (`net8.0-windows` / `win-x64`) | Coinciden |
| Proyectos referenciados por `AutoExam.sln` (`AutoExam`, `AutoExam.Tests`) | Existen y compilan/testean con el `.sln` |

No se modificó ningún archivo de código ni el workflow: no hizo falta, todo
seguía consistente. **No se hizo `git push`, no se creó ningún Release ni tag**
— eso queda para el usuario (checklist abajo).

## 6. Checklist final del usuario — único responsable de estos pasos

Nada de esto lo puede ejecutar un agente contra el remoto. Orden estricto:

1. [ ] **GitHub → Settings → Actions → General → Workflow permissions** →
   marcar **"Read and write permissions"** → Save. (R-1, una sola vez).
2. [ ] **GitHub → Settings → Branches** → si `main` tiene branch protection,
   confirmar que no bloquea el push directo de `github-actions[bot]` (o
   agregarlo a la lista de bypass). (R-1, una sola vez).
3. [ ] `git push origin main` desde este checkout (8 commits locales
   pendientes, incluye `AutoExam.csproj` en `1.0.2` y el propio `publish.yml`).
4. [ ] Pestaña **Actions** del repo → esperar a que corra el workflow
   **"Publicar release"** (se dispara solo por el push) → confirmar que
   termina en verde (✓), no en rojo (✗). Si falla, ver §2 fila 3 de este
   documento.
5. [ ] **GitHub → Releases** → confirmar que existe `v1.0.2` con el asset
   `AutoExam-v1.0.2.zip` adjunto y descargable.
6. [ ] Confirmar que `update.xml` en `main` quedó en `<version>1.0.2</version>`
   (commit automático de `github-actions[bot]`, mensaje
   `update.xml: 1.0.2 [skip ci]`).
7. [ ] En una segunda PC con una versión anterior de AutoExam instalada,
   abrir la app y confirmar que `AutoUpdater.NET` detecta `1.0.2` (lee
   `update.xml` vía raw.githubusercontent.com), descarga, y al aceptar la
   app se actualiza y reinicia mostrando la nueva versión.

Rollback si algo sale mal en producción: ver §3 de este documento (máximo
3 pasos, sin herramientas nuevas).
