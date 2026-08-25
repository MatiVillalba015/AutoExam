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
