# 01 — Spec: Comodidad de interfaz + actualización automática por push

## Contexto de negocio

AutoExam es una app de escritorio WPF (.NET 8, WPF-UI/Fluent) que ya tiene un sistema de
auto-actualización funcionando (AutoUpdater.NET + `update.xml` + GitHub Releases) y un sistema
visual propio (`Theme/Tokens.*`, `Theme/Estilos.xaml`) cuidado en detalle. El gap real no es que
la actualización no exista, sino que publicarla hoy exige pasos manuales (compilar, subir el
Release a mano en GitHub, correr `publicar.ps1 -Publicar`); y la interfaz, aunque prolija, tiene
puntos de fricción concretos e identificables en el código actual. Este spec cubre ambos frentes
sin tocar el stack.

## Historias de usuario

**US-001 — Publicación automática de una versión nueva al pushear**
Como desarrollador único de AutoExam, quiero que al pushear a `main` un cambio que suba
`<Version>` en `AutoExam.csproj` se dispare solo todo lo que hoy hago a mano con `publicar.ps1`
(compilar, empaquetar, publicar el Release en GitHub, verificar que el paquete descargue y
actualizar `update.xml`), para que cualquier PC con AutoExam instalado reciba la versión nueva
sin que yo suba nada a mano.

**US-002 — Diálogos y avisos visualmente coherentes con el tema**
Como usuario de AutoExam, quiero que las confirmaciones, avisos y errores de la app (salir con un
examen sin terminar, error al iniciar, "ya estás al día", etc.) se vean con los mismos colores,
tipografía y bordes redondeados que el resto de las pantallas, para no toparme con una ventana
gris de Windows genérica en medio de una app con un tema claro/oscuro cuidado.

**US-003 — La ventana recuerda tamaño, posición y si estaba maximizada**
Como usuario que abre AutoExam seguido, quiero que la ventana vuelva a aparecer con el mismo
tamaño, posición y estado (maximizada o no) que tenía la última vez que la cerré, para no tener
que reacomodarla cada vez que abro la app.

**US-004 — Navegación por teclado entre las secciones principales**
Como usuario, quiero poder moverme entre Libros, Nuevo examen, Examen, Historial y Ajustes con un
atajo de teclado, para no depender solo del mouse en el riel lateral — igual que ya puedo navegar
un examen en curso sin tocar el mouse.

**US-005 — Tamaño de texto ajustable al rendir un examen**
Como usuario que estudia con material de cientos de páginas y exámenes largos, quiero poder
agrandar o achicar el texto de la pregunta y las opciones, para leer cómodo en sesiones largas sin
depender del zoom general de Windows.

## Criterios de aceptación

**US-001**
- Given un push a `main` donde `<Version>` del `.csproj` es mayor a la versión publicada en
  `update.xml`, When el proceso de publicación corre, Then se compila en modo Release, se verifica
  que la versión del binario compilado coincide con `<Version>`, se genera el paquete, se publica
  un Release en GitHub con ese paquete y recién después se actualiza `update.xml` (versión, URL de
  descarga y changelog) con commit y push automáticos a `main`.
- Given que el paquete recién publicado no responde con descarga exitosa al verificarlo, When el
  proceso de publicación lo detecta, Then no se toca `update.xml` y el proceso queda marcado como
  fallido y visible para el desarrollador, exactamente igual que hoy hace `publicar.ps1 -Publicar`.
- Given un push a `main` que no cambia `<Version>` (o la deja igual a la ya publicada), When el
  proceso corre, Then no se crea ningún Release nuevo ni se modifica `update.xml`.
- Given que la compilación falla o el binario compilado no coincide con `<Version>`, When el
  proceso corre, Then no se publica ningún Release ni se toca `update.xml`, y el fallo queda
  registrado de forma visible.
- Given que el Release y `update.xml` ya se publicaron para una versión, When una PC con una
  versión anterior abre AutoExam, Then el comportamiento de detección y descarga ya existente
  (silenciosa al iniciar, con corte de bucle si el paquete resulta no coincidir con lo anunciado)
  sigue funcionando sin cambios.

**US-002**
- Given el tema oscuro o claro activo, When aparece cualquier confirmación, aviso o error de la
  aplicación, Then el diálogo usa la paleta y tipografía del tema activo, igual que el resto de
  las vistas, y no una ventana de sistema operativo con estilo distinto al de la app.
- Given una acción irreversible (salir con examen sin terminar, borrar historial, quitar un libro),
  When se pide confirmación, Then el usuario sigue teniendo que responder explícitamente antes de
  que la acción se ejecute (no se automatiza ni se vuelve una notificación pasiva).

**US-003**
- Given que cierro AutoExam con la ventana en un tamaño, posición o estado (maximizada o no)
  distinto al que trae por defecto, When vuelvo a abrir la app, Then la ventana aparece con ese
  mismo tamaño, posición y estado.
- Given que la posición guardada queda fuera del área visible actual (por ejemplo, se desconectó
  un monitor), When abro la app, Then la ventana aparece centrada y completamente visible en la
  pantalla principal en vez de fuera de vista.

**US-004**
- Given que estoy en cualquier sección de la app y el foco no está en un campo de texto, When
  presiono el atajo de navegación correspondiente a una sección, Then la app cambia a esa sección
  en el mismo orden en que aparece en el riel lateral (Libros, Nuevo examen, Examen, Historial,
  Ajustes).
- Given que estoy rindiendo un examen, When uso los atajos ya existentes del examen (1-4, A-D,
  flechas, Enter, S), Then los nuevos atajos de navegación entre secciones no los interfieren ni
  los reemplazan.
- Given que el foco está en un campo de texto editable (por ejemplo el eje temático o una API Key),
  When escribo, Then los atajos de navegación entre secciones no se disparan por error.

**US-005**
- Given que estoy rindiendo un examen, When elijo agrandar o achicar el texto, Then el tamaño de
  la pregunta y de las opciones cambia en consecuencia sin romper el recorte ni obligar a scroll
  horizontal.
- Given que ajusté el tamaño de texto, When cierro y vuelvo a abrir AutoExam, Then la preferencia
  se mantiene (no vuelve al tamaño por defecto en cada sesión).

## Reglas de negocio

- La versión que dispara una publicación es `<Version>` del `.csproj`; solo se publica si es mayor
  a la ya anunciada en `update.xml` (nunca igual ni menor).
- `update.xml` no se modifica hasta confirmar que el paquete recién publicado se puede descargar
  (mismo criterio HTTP 200 que ya aplica `publicar.ps1 -Publicar` hoy).
- El paquete publicado debe contener un binario cuya versión coincida exactamente con
  `<Version>` — condición ya exigida a mano y que ahora pasa a exigirse en cada publicación
  automática, sin excepción.
- Un push que no sube `<Version>` es tratado como cambio de código normal: no genera Release ni
  aviso de actualización para nadie.
- El mecanismo de detección de bucle de actualización que ya existe en el cliente (dos intentos
  antes de dejar de insistir con la misma versión) no se modifica ni se debilita.

## Fuera de alcance

- Cambiar el stack tecnológico actual (.NET 8, WPF, WPF-UI, CommunityToolkit.Mvvm,
  AutoUpdater.NET, PdfPig) o migrar a otro mecanismo de actualización.
- Rediseñar la ventana propia de progreso/descarga que muestra AutoUpdater.NET: es interfaz de un
  componente de terceros, no de WPF-UI; evaluar reemplazarla es una decisión de arquitectura aparte.
- Publicar una versión nueva en cada push sin importar si `<Version>` cambió (rompería la
  protección anti-bucle que la app ya tiene a propósito).
- Empaquetar como instalador MSI/MSIX ni publicar en otros canales (Microsoft Store, Winget); se
  mantiene la distribución actual como `.exe` portable dentro de un ZIP en GitHub Releases.
- Sincronizar historial, configuración o progreso entre distintas PCs del mismo usuario: cada
  instalación se actualiza sola, pero sigue siendo independiente en sus datos.
- Traducción o soporte multi-idioma de la interfaz.
- Cualquier cambio a la lógica de generación de exámenes, corrección UBA o extracción de PDF
  (`GeminiApiService`, `EvaluadorUBA`, `PdfExtractorService`): no forman parte de este pedido.

## Supuestos

- "Cada vez que hagas commit y push" se interpreta como el flujo normal de trabajo sobre `main`;
  la publicación de una versión descargable solo se dispara cuando ese push incluye una suba real
  de `<Version>`, igual que exige hoy el proceso manual — evita generar releases fantasma en cada
  commit de código suelto.
- El repositorio de GitHub (`MatiVillalba015/AutoExam`) admite ejecutar automatización propia del
  repo (tipo GitHub Actions) con permiso para crear Releases en sí mismo; si esto no fuera así,
  bloquea US-001 y hay que resolverlo antes de que analista-tecnico diseñe la solución.
- Los atajos de teclado de navegación (US-004) no chocan con los que WPF-UI o Windows ya reservan
  a nivel de sistema; la combinación exacta de teclas queda a criterio de diseño técnico.

## Preguntas abiertas

Ninguna bloquea el arranque del trabajo técnico. Si el supuesto sobre permisos de automatización
en el repo de GitHub resultara falso, esa sí sería la única pregunta que bloquearía US-001.
