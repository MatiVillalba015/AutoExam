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
