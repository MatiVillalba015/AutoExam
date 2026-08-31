# 01 - Especificación: fuentes nuevas, pulido de UX y ajustes de historial/resultado

Estado: aprobado por usuario-final y congelado. Las dos preguntas que bloqueaban quedaron resueltas (ver US-008 y US-013). Refinamientos puntuales post-aprobación incorporados en US-008, US-010, US-011 y US-012.

## Contexto de negocio
AutoExam arma exámenes multiple choice con el material de estudio del propio alumno y los corrige con la escala UBA. Hoy la única fuente admitida es PDF (biblioteca de "libros", alcance por capítulos/páginas, corrección local y modo revancha).
Este documento cubre cinco pedidos del dueño de producto: ampliar las fuentes admitidas (Office e imágenes de apuntes manuscritos), pulir las animaciones existentes, poder borrar un examen puntual del historial y mostrar un mensaje de felicitación al aprobar con 7 o más.

## Historias de usuario

### US-008 — Subir archivos de Microsoft Office como fuente
Como alumno, quiero agregar archivos de Word, Excel y PowerPoint como material de un examen, para generar preguntas de apuntes que no tengo en PDF.

### US-009 — Definir el alcance en fuentes sin capítulos ni páginas
Como alumno, quiero poder acotar el examen aunque la fuente no tenga índice ni páginas como un PDF, para enfocarlo en una parte del material.

### US-010 — Generar un examen desde fotos de apuntes manuscritos
Como alumno, quiero subir una o varias fotos de mis apuntes escritos a mano y que la app interprete la letra, para generar un examen de ese contenido sin transcribirlo.

### US-011 — Pulir las animaciones y transiciones de la interfaz
Como usuario, quiero que las animaciones y transiciones existentes se sientan suaves y consistentes, para que la app se perciba más prolija.

### US-012 — Borrar un examen individual del historial
Como alumno, quiero borrar del historial un examen puntual, para sacar intentos que no me interesan sin borrar todo el historial.

### US-013 — Mensaje de felicitación al aprobar con 7 o más
Como alumno, quiero un mensaje de felicitación destacado cuando saco 7 o más, para celebrar el resultado.

## Criterios de aceptación (Given/When/Then)

### US-008
- Given estoy agregando material (Nuevo examen o Libros), when abro el selector de archivos, then puedo elegir `.docx`, `.xlsx`, `.pptx` además de `.pdf`, y también soltarlos en la zona de arrastre.
- Given agregué un archivo `.docx` / `.xlsx` / `.pptx` legible con contenido textual, when genero un examen sobre esa fuente, then el examen sale con preguntas cuyo contenido proviene de ese archivo, igual que hoy con un PDF, y se corrige con la escala UBA.
- Given agregué un archivo de Office, when termina de procesarse, then la app muestra su nombre y una medida de tamaño propia del formato (páginas de Word, diapositivas de PowerPoint, hojas/filas de Excel); si el formato no expone ninguna, lo trata como documento único.
- Given agrego un archivo `.docx` / `.xlsx` / `.pptx` de cualquier tamaño o cantidad de páginas / diapositivas / filas, when lo proceso, then la app no lo rechaza ni lo trunca por un límite propio del archivo: el único límite aplicable es la cuota general del proveedor de IA (RN-3).
- Given intento agregar un archivo `.doc`, `.xls` o `.ppt` (formato binario antiguo), when lo elijo, then la app lo rechaza con un mensaje que indica que solo se admiten `.docx` / `.xlsx` / `.pptx` y sugiere reguardarlo en el formato actual.
- Given un archivo protegido con contraseña, dañado o de un formato no soportado, when intento agregarlo, then la app lo rechaza con un mensaje que explica la causa y no crea una fuente vacía.
- Given un archivo de Office sin texto extraíble (por ejemplo solo imágenes), when lo agrego, then la app avisa que no encontró contenido para generar preguntas.

### US-009
- Given una fuente de Office o de imágenes que no expone capítulos, when abro el paso Alcance, then la detección de capítulos no se ofrece o informa que esa fuente no tiene capítulos, y puedo generar sobre todo el material.
- Given cualquier fuente, when estoy en el paso Alcance, then el campo de eje temático (texto libre) sigue disponible y acota las preguntas.
- Given una fuente de Office con estructura aprovechable (diapositivas, hojas de Excel, secciones de Word), when defino el alcance, then puedo limitarlo a un subconjunto de esa estructura. *(deseable, no bloqueante)*
- Given no marco ningún recorte, when genero, then el examen cubre el material completo de la fuente.

### US-010
- Given estoy agregando material, when abro el selector o suelto archivos, then puedo elegir imágenes `.jpg` / `.jpeg` / `.png` / `.heic` / `.heif` y seleccionar varias a la vez para un mismo material.
- Given agrego una imagen `.heic` / `.heif` (formato por defecto de la cámara de iPhone), when se procesa, then la app la convierte automáticamente a un formato soportado antes de enviarla a la IA, y el examen se genera igual que con una `.jpg` o `.png`.
- Given agregué fotos con escritura manuscrita legible, when genero el examen, then las preguntas reflejan el contenido manuscrito interpretado de esas imágenes.
- Given una o más fotos son ilegibles o no tienen texto reconocible, when genero, then la app avisa que no pudo interpretar contenido suficiente y no crea un examen vacío; si pudo derivar algunas preguntas, las genera e informa la limitación.
- Given un conjunto de varias fotos, when se procesan, then el orden en que las agregué se respeta como orden del material.
- Given una imagen supera el tamaño/resolución aceptado, o el conjunto supera el máximo de imágenes por material, when la agrego, then la app lo informa e indica el límite.

### US-011
- Given el UAT revisa US-011, when evalúa una por una las superficies de RN-7, then completa el siguiente checklist con resultado binario (pasa / no pasa) por superficie, y el sign-off requiere "pasa" en todas. Una superficie "pasa" cuando: usa los parámetros centralizados de duración y suavizado, la transición se completa sin cortes ni parpadeo, y no bloquea la interacción del usuario.
  - [ ] Transición entre secciones de la navegación principal
  - [ ] Hover y pulsado de botones y chips
  - [ ] Riel de pasos del asistente (línea de avance)
  - [ ] Baldosas del navegador de preguntas al cambiar de estado o de pregunta
  - [ ] Entrada de la pantalla de Resultados
  - [ ] Apertura y cierre de los avisos (InfoBar)
  - [ ] Anillos de progreso
  - [ ] Alta y baja de ítems en las listas de Historial y Libros
- Given el sistema operativo tiene activado "reducir movimiento", when uso la app, then las animaciones no esenciales se acortan o se desactivan.
- Given una transición de sección o de estado, when se dispara, then no bloquea la interacción del usuario ni dura más de ~250 ms.
- Given navego rápido entre secciones o entre preguntas, when las transiciones se encadenan, then no se acumulan ni dejan elementos a medio animar.
- Given se pule una superficie, when se compara con la versión previa, then no cambia su comportamiento funcional, solo su animación.

### US-012
- Given estoy en Historial con al menos un examen, when miro un ítem de la lista, then tiene una acción visible para borrar ese examen.
- Given toco borrar en un examen, when se me pide confirmación, then el examen se elimina solo si confirmo; si cancelo, nada cambia.
- Given borré un examen, when vuelvo a ver el Historial, then ese examen ya no aparece y las estadísticas agregadas (total rendidos, promedio, aciertos, mejor nota, aprobados, aplazos) se recalculan sin él.
- Given borré un examen, when reinicio la app, then el examen sigue sin aparecer.
- Given el examen borrado tenía imágenes asociadas, when se elimina, then también se limpian sus archivos de imágenes.
- Given borré el último examen, when el historial queda vacío, then se muestra el estado vacío ("Todavía no rendiste ningún examen").
- Given existe la acción "Borrar historial" (todo), when borro un examen individual, then esa acción global sigue disponible y sin cambios.
- Given estoy rindiendo una ronda de revancha de un examen y navego a Historial, when borro de la lista el examen original de ese intento, then la confirmación advierte que hay una revancha en curso de ese examen; si confirmo, el examen se borra y la revancha en curso se descarta sin registrarse.
- Given finalicé o cerré una ronda de revancha cuyo examen original ya fue borrado del historial, when la ronda termina, then la app no recrea el registro borrado y no muestra error. *(el registro original ya no existe: la revancha no puede reanclarse a nada)*

### US-013
- Given terminé y corregí un examen con nota UBA de 7 o más, when veo la pantalla de Resultados, then se muestra, destacado y en mayúsculas, el texto literal: `FELICIDADES CULONA TE ROMPO BIEN EL CULO`.
- Given saqué 6 o menos, when veo Resultados, then ese mensaje no aparece.
- Given el resultado corresponde a una ronda de revancha y no al intento original, when veo Resultados, then el mensaje no se muestra. *(supuesto: la revancha no modifica la nota)*
- Given aparece el mensaje, when reviso la corrección pregunta por pregunta, then el resto de la pantalla de Resultados funciona igual que hoy.
- Given la app se instala/actualiza en la computadora de cualquier usuario a través de la actualización automática, when saca 7 o más, then ve el mensaje: forma parte del release distribuido y no hay opción ni configuración para ocultarlo.

## Reglas de negocio
- **RN-1** — La escala de calificación no cambia: UBA 1 a 10, se aprueba con 4 (60% de aciertos). "7 o más" (US-013) significa nota ≥ 7, equivalente a ≥ 74% de aciertos.
- **RN-2** — Toda fuente nueva (Office, imágenes) usa el mismo flujo que un PDF: se suma al material, se elige alcance y formato, se genera con el mismo motor de preguntas y se corrige localmente.
- **RN-3** — El límite real de generación lo sigue poniendo la cuota del proveedor de IA. Las fuentes que viajan como imagen (fotos de apuntes, Office sin texto) consumen más cuota y pueden tardar más; la app debe informarlo antes o durante la generación.
- **RN-4** — Si una fuente no aporta material suficiente, no se crea un examen vacío: se explica el motivo.
- **RN-5** — El mensaje de US-013 es fijo y se distribuye en el release: no es configurable ni se puede desactivar desde la interfaz.
- **RN-6** — Cualquier borrado del historial (individual o total) exige confirmación explícita y recalcula las estadísticas del perfil.
- **RN-7** — Superficies existentes en revisión para US-011: transición entre secciones de la navegación principal; hover y pulsado de botones y chips; riel de pasos del asistente (línea de avance); baldosas del navegador de preguntas al cambiar de estado o de pregunta; entrada de la pantalla de Resultados; apertura y cierre de los avisos (InfoBar); anillos de progreso; alta y baja de ítems en las listas de Historial y Libros. No se agregan animaciones a superficies que hoy no animan salvo acuerdo previo.
- **RN-8** — En v1 las fuentes de Office admitidas son exclusivamente `.docx`, `.xlsx` y `.pptx`. No se les impone un límite propio de tamaño ni de páginas/diapositivas/filas: aplica únicamente la cuota general del proveedor de IA (RN-3).
- **RN-9** — Formatos de imagen admitidos (US-010): `.jpg` / `.jpeg` / `.png` de forma nativa, y `.heic` / `.heif` mediante conversión automática a un formato soportado antes del envío a la IA. Cualquier otro formato de imagen queda fuera de alcance.

## Fuera de alcance
- Formatos binarios antiguos de Office: `.doc`, `.xls`, `.ppt` (quedan fuera de v1; se podrán retomar en una etapa posterior).
- Formatos no-Microsoft: OpenDocument (`.odt` / `.ods` / `.odp`), iWork, Google Docs/Sheets/Slides nativos, `.rtf`, `.txt`, Markdown, ePub.
- Formatos de imagen distintos de `.jpg` / `.jpeg` / `.png` / `.heic` / `.heif` (por ejemplo `.webp`, `.tiff`, `.bmp`, `.gif`).
- Fuentes que no son documento ni imagen: audio, video, enlaces web, captura de cámara en vivo.
- Edición o corrección manual del texto reconocido (de Office o de manuscrito) antes de generar el examen.
- Traducción de material que está en otro idioma.
- Detección automática de "capítulos" en archivos de Office o en imágenes.
- Combinar en un mismo examen varias fuentes de distinto tipo (por ejemplo PDF + fotos). Sí se admite un set de varias imágenes como una única fuente.
- Papelera o deshacer para exámenes borrados; exportar el historial antes de borrarlo.
- Editar exámenes ya rendidos.
- Rediseño visual o animaciones nuevas fuera de las superficies de RN-7.
- Personalizar, traducir, condicionar, ocultar o mostrar en revancha el mensaje de US-013.
- Sincronización en la nube o entre dispositivos de las nuevas fuentes.

## Preguntas abiertas (bloquean)
Ninguna. Las dos que bloqueaban quedaron resueltas:
- US-013: el mensaje literal va en el release distribuido por actualización automática a terceros, sin flag ni configuración para ocultarlo (criterio de aceptación firme en US-013 y RN-5).
- US-008: v1 admite únicamente `.docx` / `.xlsx` / `.pptx`; los formatos legacy `.doc` / `.xls` / `.ppt` quedan fuera de alcance (RN-8 y Fuera de alcance).

## Supuestos
Un material puede tener varias imágenes pero una sola fuente por examen (salvo el set de imágenes); el mensaje de US-013 se muestra solo en el resultado del intento original; se respeta el "reducir movimiento" del sistema operativo; el ítem de interfaz hoy llamado "Libro" se generaliza a "material" o se mantiene el término, a decidir en diseño.

Sugerencia: definir con analista-técnico el contrato del pipeline de extracción multi-formato (texto e imágenes), la conversión de HEIC/HEIF y cómo se arma el alcance/recorte que se envía al servicio de IA cuando la fuente no es un PDF.
