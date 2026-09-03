# 01 - Especificación: fuentes nuevas, pulido de UX y ajustes de historial/resultado

Estado: aprobado por usuario-final y congelado. Las dos preguntas que bloqueaban quedaron resueltas (ver US-008 y US-013). Refinamientos puntuales post-aprobación incorporados en US-008, US-010, US-011 y US-012.

Pedidos nuevos agregados post-congelamiento, pendientes de aprobación:
- **US-014** (OCR de respaldo para documentos con solo imágenes). No modifica ninguna historia ya aprobada; extiende el caso de US-008 línea "sin texto extraíble" y reutiliza el motor de interpretación de imágenes de US-010.
- **US-015** (emojis contextuales en textos de la interfaz), **US-016** (más animaciones, ampliando el alcance cerrado de RN-7) y **US-017** (centrar mejor el contenido en pantalla completa).
- **US-018** (preguntas con imagen de referencia, tomada del propio material o buscada como apoyo visual).
- **US-019** (feedback visual de éxito/error al probar la conexión de una clave de Gemini en Ajustes), **US-020** (botón para ver el contenido/de qué trata un libro ya subido) y **US-021** (animación de hover en los botones principales del menú: Libros, Nuevo examen, Historial y Ajustes).
- **US-022** (un mismo archivo .docx/.pdf con contenido mezclado: texto real, capturas de pantalla y fotos de celular a papeles, todo junto, y examen generado combinando todo). Formaliza y une lo que ya cubrían por separado US-008, US-010 y US-014.
- **US-023** (organizar el material en Materias: Bioquímica, Fisiología, etc., y que cada libro/documento subido quede agrupado dentro de la materia a la que pertenece) y **US-024** (elegir uno o más documentos ya subidos de una materia para generar un único examen combinando esos documentos).
- **US-025** (guardar el detalle de cada examen rendido — no solo el resumen — y poder entrar a un examen del historial a revisar pregunta por pregunta qué salió bien y qué mal) y **US-026** (generar un examen nuevo combinando preguntas de varios exámenes anteriores del historial, al azar, con la cantidad total que el alumno elija).
- **US-027** (paleta de colores más moderna y color propio por Materia, elegido por el alumno, que se refleja en el examen generado de esa materia), **US-028** (tipografía más moderna, con tamaño un poco más chico específicamente en la pantalla de examen) y **US-029** (más microinteracciones y más prolijas en transiciones entre pantallas y en botones, incluyendo corregir el "salto"/zoom actual del hover de botones).
- **US-030** (mejoras de layout en las 4 pantallas principales: menú, examen, historial y biblioteca, incluyendo jerarquía visual de tarjetas y de la pregunta/opciones en examen).
- **US-031** (menú principal más completo, con accesos directos a las acciones más usadas: generar examen, ver exámenes anteriores, subir material nuevo y ajustes, no solo los 4 botones de navegación actuales).

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

### US-014 — OCR de respaldo cuando un documento solo tiene imágenes
Como alumno, quiero que si subo un Word, PDF u otro documento cuyo "texto" en realidad son fotos pegadas (por ejemplo fotos sacadas con el celular a las páginas de un libro), la app intente leer ese contenido igual, en vez de decirme directamente que no encontró contenido.

### US-015 — Emojis contextuales según el texto
Como usuario, quiero que ciertos textos de la interfaz (por ejemplo "Examen", secciones de Historial, mensajes de resultado) se acompañen de un emoji relacionado con su significado (ej. 📝 para "Examen", 📚 para "Libros/material"), para que la interfaz se sienta más expresiva y fácil de reconocer de un vistazo.

### US-016 — Más animaciones en la interfaz
Como usuario, quiero que además de pulir las animaciones que ya existen (US-011), se agreguen animaciones nuevas en superficies que hoy no animan, para que la app se sienta más viva.

### US-017 — Centrar mejor el contenido en pantalla completa
Como usuario, quiero que cuando maximizo la ventana o la pongo en pantalla completa, el contenido se centre y aproveche el espacio en vez de quedar pegado a un costado o estirado sin criterio, para que se vea prolijo en cualquier tamaño de ventana.

### US-018 — Preguntas con imagen de referencia
Como alumno, quiero que al azar algunas preguntas del examen muestren una imagen de referencia relacionada con lo que se pregunta (un gráfico o figura sacada del propio material, o una imagen de apoyo cuando el material no tiene una figura propia para ese tema), para practicar también el reconocimiento visual, no solo el texto.

### US-019 — Resultado visible al probar la conexión de una clave de Gemini
Como usuario, quiero que al cargar una clave nueva de Gemini en Ajustes y darle a "Probar conexión", la app me muestre claramente si la prueba fue exitosa o falló (y por qué), para saber si la clave sirve antes de usarla para generar exámenes.

### US-020 — Ver de qué trata un libro/material ya subido
Como alumno, quiero un botón para ver el contenido o un resumen de un libro/material que ya subí, para acordarme de qué se trata sin tener que abrir el archivo original ni generar un examen para descubrirlo.

### US-021 — Animación de hover en los botones principales del menú
Como usuario, quiero una animación o suavizado sutil al pasar el mouse por los botones importantes del menú (Libros, Nuevo examen, Historial, Ajustes), para que la interfaz se sienta más pulida al navegar.

### US-022 — Un mismo archivo con contenido mezclado (texto, capturas y fotos de papel)
Como alumno, quiero subir un único .docx o .pdf que tenga de todo mezclado —párrafos de texto real, capturas de pantalla, y fotos sacadas con el celular a hojas o páginas de un libro—, y que la app arme el examen usando todo ese contenido junto, con o sin imágenes de referencia en las preguntas (US-018) según corresponda.

### US-023 — Organizar el material por Materias
Como alumno, quiero crear secciones de Materias (por ejemplo Bioquímica, Fisiología) y que cada libro/documento que suba quede agrupado dentro de la materia correspondiente, para tener mi biblioteca ordenada en vez de una sola lista mezclada de todo lo que subí.

### US-024 — Generar un examen combinando varios documentos de una materia
Como alumno, quiero poder elegir uno o más documentos ya subidos dentro de una misma materia y generar un único examen que combine el contenido de todos los que marqué, para poder repasar de varias fuentes a la vez sin tener que elegir una sola.

### US-025 — Ver el detalle de un examen del historial
Como alumno, quiero entrar a cualquier examen que ya rendí desde el Historial y ver pregunta por pregunta qué contesté, cuál era la correcta y el análisis de cada opción, para poder repasar mis errores tiempo después, no solo en el momento de corregirlo.

### US-026 — Generar un examen nuevo combinando exámenes anteriores
Como alumno, quiero elegir dos o más exámenes que ya rendí antes y generar un examen nuevo que combine preguntas de esos exámenes al azar, eligiendo cuántas preguntas en total quiero (por ejemplo 10, 30 o 60), para repasar de forma mezclada sin tener que rendir cada examen viejo por separado.

### US-027 — Paleta de colores más moderna y color por Materia
Como alumno, quiero que la app tenga una paleta de colores más moderna, y poder elegir un color propio para cada Materia que voy creando, para que después el examen generado sobre esa materia se vea con ese color como identidad (en vez de que todo se vea igual sin importar la materia).

### US-028 — Tipografía más moderna
Como alumno, quiero que la app use una tipografía más moderna, y que en la pantalla de examen el tamaño de letra sea un poco más chico que el actual, para que la interfaz se vea más prolija y quepa más contenido sin sentirse apretada.

### US-029 — Microinteracciones más prolijas en pantallas y botones
Como alumno, quiero que las transiciones entre pantallas y las animaciones de los botones sean más suaves y prolijas, y que se corrija el pequeño salto/zoom que hoy pasa al pasar el mouse por un botón, para que la app se sienta más pulida en el día a día.

### US-030 — Mejoras de layout en las pantallas principales
Como usuario, quiero que el menú principal, la pantalla de examen, el historial y la biblioteca tengan un layout más prolijo y aprovechen mejor el espacio, para que la app se sienta más moderna y ordenada en el día a día.

### US-031 — Menú principal más completo
Como alumno, quiero que el menú principal ofrezca de entrada las acciones que más uso —generar un examen nuevo, ver mis exámenes anteriores, subir material nuevo y entrar a ajustes— en vez de ser solo 4 botones de navegación genéricos (Libros, Nuevo examen, Historial, Ajustes), para llegar más rápido a lo que quiero hacer sin tener que entrar primero a una sección y recién ahí encontrar la acción puntual.

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

### US-014
- Given agrego un `.docx` / `.pptx` / `.pdf` cuyas páginas contienen únicamente imágenes incrustadas (por ejemplo fotos de un libro pegadas en el documento) y el extractor de texto normal no encuentra contenido, when se procesa la fuente, then la app corre automáticamente el mismo motor de interpretación de imágenes que usa US-010 sobre esas imágenes, antes de mostrar el error de "no se encontró contenido".
- Given el OCR/interpretación de imágenes logra recuperar contenido suficiente de esas páginas, when genero el examen, then las preguntas salen de ese contenido igual que si la fuente hubiera tenido texto nativo.
- Given el OCR/interpretación de imágenes no logra recuperar contenido legible de ninguna página, when se agrega la fuente, then la app muestra el mismo mensaje de "no se encontró contenido para generar preguntas en este material" que hoy.
- Given una fuente mixta (algunas páginas con texto real, otras solo con imágenes), when se procesa, then la app combina el texto nativo de las páginas que lo tienen con el contenido interpretado de las páginas que no, sin descartar ninguna de las dos.
- Given el documento requiere pasar por interpretación de imágenes, when se procesa, then la app avisa que puede tardar más y consumir más cuota, igual que hace hoy para fuentes de fotos puras (RN-3).

### US-015
- Given un texto de la interfaz tiene un emoji asignado (por ejemplo el título "Nuevo examen", el ítem "Libros/material", el encabezado de "Historial", el mensaje de felicitación de US-013), when se muestra en pantalla, then aparece acompañado de ese emoji, sin reemplazar el texto.
- Given se define la lista de textos con emoji, when se elige cada emoji, then guarda relación clara con el significado del texto (ej. 📝 examen/lápiz y papel, 📚 libros/material, 🏆 o 🎉 felicitación al aprobar, 📊 resultados/estadísticas, 🗑️ borrar).
- Given un emoji no se renderiza bien en alguna combinación de fuente/tamaño de la app, when se detecta, then se reemplaza por uno equivalente que sí se vea bien, sin dejar el texto roto o con un cuadro vacío.
- Given se agregan emojis, when se revisa el resto de la interfaz, then no se agregan a textos donde no fueron pedidos explícitamente (evita saturar la UI). *(a definir junto con diseño la lista final de textos con emoji)*

### US-016
- Given se agregan animaciones nuevas a una superficie que hoy no anima, when se implementan, then usan los mismos parámetros centralizados de duración y suavizado que ya definió US-011, para mantener consistencia.
- Given el sistema operativo tiene activado "reducir movimiento", when uso la app, then las animaciones nuevas de US-016 también se acortan o desactivan, igual que las de US-011.
- Given una animación nueva se dispara, when ocurre, then no bloquea la interacción del usuario ni dura más de ~250 ms, salvo que sea una animación decorativa no bloqueante (ej. un ícono que "respira" suavemente).
- Given se agrega una animación nueva, when se compara con la versión previa, then no cambia el comportamiento funcional de esa superficie, solo agrega la animación. *(a definir junto con diseño qué superficies puntuales suman animación)*

### US-017
- Given la ventana está maximizada o en pantalla completa, when se muestra cualquier pantalla de la app (asistente de examen, historial, resultados, etc.), then el contenido principal se centra horizontalmente en vez de quedar pegado a un borde.
- Given la ventana está maximizada o en pantalla completa, when el contenido tiene un ancho máximo de diseño, then no se estira más allá de ese máximo (para no ver botones o texto gigantes en monitores anchos): se mantiene centrado con espacio libre a los costados.
- Given el usuario redimensiona la ventana a un tamaño intermedio (ni maximizada ni muy chica), when la app se adapta, then el centrado y los márgenes se ajustan de forma proporcional, sin saltos bruscos.
- Given la ventana vuelve a un tamaño chico o al mínimo soportado, when se muestra el contenido, then sigue siendo usable (sin recortes ni scroll horizontal), igual que hoy.

### US-018
- Given el material fuente (PDF, Office o fotos) contiene figuras/gráficos/imágenes embebidas, when se genera el examen, then algunas preguntas del lote (una proporción aleatoria, no todas) pueden incluir como referencia una de esas imágenes extraídas del propio material, relacionada con lo que pregunta el enunciado.
- Given una pregunta se genera con imagen de referencia del propio material, when se muestra en el examen, then la imagen se ve junto al enunciado antes de elegir respuesta, con una calidad/tamaño legible (no un recorte ilegible).
- Given no hay ninguna imagen aprovechable en el material para el tema de una pregunta, when se arma el examen, then esa pregunta se genera igual, pero sin imagen (no se fuerza una imagen que no corresponde).
- Given se activa la opción de complementar con imágenes de referencia externas (no extraídas del material, ej. una búsqueda de apoyo visual), when una pregunta la usa, then queda claramente distinguida como "imagen de referencia externa" (no como parte del material original del alumno). *(deseable, no bloqueante — ver Fuera de alcance sobre limitaciones de esta parte)*
- Given no hay conexión a internet o falla la búsqueda de una imagen externa, when se genera esa pregunta, then la app no bloquea la generación del examen: la pregunta sale sin imagen o usa una imagen del propio material si hay disponible.
- Given se usan imágenes (propias o externas) en preguntas, when se corrige el examen, then la corrección y el puntaje funcionan igual que hoy; la imagen es solo apoyo visual del enunciado, no cambia la lógica de corrección.
- Given un examen con preguntas con imagen queda guardado en el Historial, when lo reviso después, then las imágenes de esas preguntas siguen disponibles para ver la corrección con el mismo contexto visual.

### US-019
- Given estoy en Ajustes con una clave de Gemini cargada (nueva o existente), when toco "Probar conexión", then la app muestra un estado de "probando" mientras espera la respuesta (no queda en silencio ni parece colgada).
- Given la prueba de conexión responde OK, when termina, then se muestra un mensaje/ícono claro de éxito (ej. tilde verde + texto tipo "Conexión exitosa").
- Given la prueba de conexión falla (clave inválida, sin cuota, sin red, error del servicio), when termina, then se muestra un mensaje/ícono claro de error con el motivo en lenguaje simple (ej. "Clave inválida", "Sin conexión a internet", "Cuota agotada"), sin quedar en un estado ambiguo.
- Given pruebo la conexión varias veces seguidas, when cada prueba termina, then el resultado siempre se actualiza al último intento (no se acumulan mensajes viejos superpuestos).
- Given la prueba fue exitosa, when guardo/confirmo la clave en Ajustes, then queda guardada como la clave activa igual que hoy.

### US-020
- Given estoy en la sección de Libros/material con al menos un ítem subido, when miro un ítem de la lista, then tiene un botón visible para ver su contenido/de qué trata.
- Given toco ese botón, when se abre, then muestro un resumen o vista del contenido de ese material (por ejemplo un resumen generado, o el índice/primeras páginas/nombres de diapositivas según el tipo de fuente), sin necesidad de generar un examen para averiguarlo.
- Given el material es muy extenso, when se genera el resumen, then no se bloquea la interfaz mientras se procesa (se muestra un estado de carga).
- Given el material no tiene contenido suficiente para resumir (por ejemplo el caso de US-014 sin texto recuperable), when toco ver de qué trata, then la app lo informa en vez de mostrar un resumen vacío o inventado.
- Given cierro la vista de "de qué trata", when vuelvo a la lista de Libros, then no se modifica ni se borra el material original.

### US-021
- Given paso el mouse sobre cualquiera de los cuatro botones principales del menú (Libros, Nuevo examen, Historial, Ajustes), when el cursor entra, then se aplica una animación sutil (ej. cambio de color/elevación suave), y al salir el cursor vuelve suavemente al estado normal.
- Given uso los mismos parámetros centralizados de duración/suavizado de US-011/US-016, when se implementa esta animación, then es consistente con el resto de hover de la app.
- Given el sistema operativo tiene "reducir movimiento" activado, when paso el mouse por estos botones, then la animación se acorta o desactiva, igual que el resto de animaciones de la app.
- Given estoy parado en el botón de la sección activa (por ejemplo ya estoy en Historial), when paso el mouse por él, then el hover no genera confusión con el estado "seleccionado actual" (se diferencian visualmente).

### US-022
- Given un único `.docx` o `.pdf` tiene, mezclados en el mismo archivo, tanto texto seleccionable como imágenes (capturas de pantalla o fotos de papel/libro), when se procesa como fuente, then la app extrae el texto nativo de las partes que lo tienen y corre el motor de interpretación de imágenes (US-014) sobre las partes que son solo imagen, y usa todo ese contenido combinado para generar el examen.
- Given ese archivo mezclado incluye capturas de pantalla (por ejemplo de una app, una web, una diapositiva fotografiada), when se interpreta, then se tratan igual que cualquier otra imagen con contenido a extraer, sin necesitar un tipo de archivo especial distinto a "imagen dentro del documento".
- Given el examen se genera a partir de un archivo mezclado, when arma cada pregunta, then puede tomar su contenido de la parte de texto real, de una parte interpretada por imagen, o combinar ambas, sin que el alumno tenga que indicar de qué parte viene cada cosa.
- Given el archivo mezclado tiene tanto contenido apto para imagen de referencia (US-018) como partes de solo texto, when se genera el examen, then las preguntas con imagen de referencia (si las hay) siguen siendo una porción aleatoria, no forzada en todas las preguntas que vienen de una parte con imagen.
- Given el procesamiento de un archivo mezclado requiere pasar varias de sus páginas/secciones por interpretación de imágenes, when se procesa, then la app avisa que puede tardar más y consumir más cuota (mismo aviso que US-014/RN-3), en vez de fallar en silencio o cortarse a mitad de camino.
- Given alguna sección puntual del archivo mezclado no logra interpretarse (ni como texto ni como imagen), when se genera el examen, then esa sección se descarta y se informa la limitación, pero el resto del archivo sí se aprovecha (no se cae todo el examen por una sección ilegible).

### US-023
- Given estoy en la sección de Libros/material, when quiero organizar mi biblioteca, then puedo crear una Materia nueva con un nombre libre (ej. "Bioquímica", "Fisiología").
- Given tengo al menos una Materia creada, when subo un libro/documento nuevo, then tengo que elegir a qué Materia pertenece (o crear una nueva ahí mismo, sin salir del flujo de carga).
- Given tengo materiales subidos antes de que existiera esta organización (US-008/US-010/US-014/US-022), when actualizo a esta versión, then esos materiales quedan agrupados en una Materia por defecto (ej. "Sin materia" o "General"), sin perderse ni duplicarse, y puedo reasignarlos después a la materia que corresponda.
- Given entro a la sección de Libros/material, when la veo, then los documentos se muestran agrupados/filtrados por Materia, no todos mezclados en una sola lista larga.
- Given quiero borrar una Materia, when la elimino, then la app me pregunta qué hacer con los documentos que tenía adentro (moverlos a "Sin materia" o borrarlos también), nunca los borra en silencio.
- Given renombro una Materia, when confirmo el cambio, then los documentos que ya estaban agrupados ahí siguen asociados a la materia renombrada.

### US-024
- Given estoy armando un examen nuevo y elijo una Materia, when veo sus documentos, then puedo tildar uno o más (checkbox o selección múltiple), no solo elegir uno a la vez como hoy.
- Given tildé más de un documento, when confirmo la selección, then el examen se genera combinando el contenido de todos los documentos elegidos, como si fueran una sola fuente para esa generación.
- Given los documentos elegidos son de tipos distintos (ej. un PDF y un Word, o un PDF con texto y otro escaneado), when se combinan, then cada uno se procesa con el extractor que le corresponde (US-008/US-014/US-022) y el resultado se une antes de generar las preguntas.
- Given selecciono varios documentos, when defino el Alcance del examen, then puedo elegir generarlo sobre todos los documentos completos, o si algún documento tiene capítulos/páginas (US-009), acotar por documento individualmente antes de combinar.
- Given el conjunto de documentos elegidos es muy grande, when se genera el examen, then aplica la misma lógica de cuota/aviso que ya existe para una fuente grande (RN-3): no se rompe, informa que puede tardar más.
- Given un examen se generó combinando varios documentos, when lo reviso en el Historial, then la referencia de origen de cada pregunta (JustificacionBibliografia/ReferenciaFuente) indica de qué documento salió esa pregunta puntual, no solo "el material" en general.
- Given selecciono documentos de distinta Materia (si la interfaz lo permitiera), when intento generar, then la app no lo permite: la selección múltiple es siempre dentro de una misma Materia.

### US-025
- Given rindo y corrijo un examen, when queda registrado en el Historial, then se guarda también el detalle completo de cada pregunta (enunciado, opciones, cuál marqué, cuál era la correcta, el análisis por opción y la imagen adjunta si tenía), no solo el resumen numérico que se guarda hoy.
- Given estoy en Historial y toco un examen de la lista, when entro, then veo la lista de sus preguntas con un indicador de acertada/errada/salteada por cada una, igual que en la pantalla de corrección justo después de rendir.
- Given abro una pregunta puntual de ese detalle, when la reviso, then veo el mismo análisis completo que se ve al corregir en el momento (por qué la correcta lo es, por qué las demás no).
- Given el examen tenía preguntas con imagen de referencia (US-018/US-022), when reviso su detalle en el historial, then la imagen sigue disponible ahí (no se borró al terminar el examen).
- Given tengo exámenes rendidos antes de esta versión (sin el detalle guardado), when los abro desde el Historial, then la app lo informa con claridad ("este examen es de antes de esta versión, no tiene el detalle guardado") en vez de romperse o mostrar una lista vacía sin explicación.
- Given borro un examen del historial (US-012), when se borra, then también se borra su detalle guardado (preguntas e imágenes), no queda residual.

### US-026
- Given estoy en el asistente de Nuevo examen (paso Material), when abro esa pantalla, then además de elegir un libro/documento para generar preguntas nuevas con IA, veo una opción para armar el examen a partir de exámenes anteriores ya rendidos.
- Given elijo la opción de generar a partir de exámenes anteriores, when entro a ese modo, then puedo tildar dos o más exámenes ya rendidos del historial (con buscador/filtro si la lista es larga), sin salir del asistente de Nuevo examen.
- Given tildé varios exámenes, when defino el examen nuevo, then elijo la cantidad total de preguntas (ej. 10, 30, 60, o un número a mano), igual que en el asistente de examen nuevo con material normal, saltando los pasos que no aplican a este modo (no hay alcance de páginas/módulos ni formato de generación con IA).
- Given estoy en Historial, when quiero repasar de varios exámenes viejos, then también puedo llegar a este mismo flujo desde ahí (acceso alternativo), pero el punto de entrada principal y siempre disponible es el menú de Nuevo examen.
- Given tildé varios exámenes, when defino el examen nuevo, then elijo la cantidad total de preguntas (ej. 10, 30, 60, o un número a mano), igual que en el asistente de examen nuevo.
- Given confirmo la generación, when se arma el examen, then las preguntas salen mezcladas al azar del conjunto de preguntas de los exámenes elegidos, sin repetir la misma pregunta dos veces en el nuevo examen.
- Given la cantidad pedida es mayor a la cantidad total de preguntas disponibles entre los exámenes elegidos, when se genera, then el examen sale con todas las que hay disponibles (sin repetir preguntas) y la app avisa que se ajustó la cantidad.
- Given este examen combinado no depende de la cuota de la IA (usa preguntas ya generadas antes), when lo genero, then es instantáneo, sin esperar a Gemini ni gastar cuota.
- Given respondo este examen combinado, when lo corrijo, then se corrige y se guarda en el Historial igual que cualquier otro examen (con su propio detalle, US-025), dejando en claro que es un examen de repaso combinado (no vuelve a "contarse" como si fuera un intento nuevo de cada examen original).
- Given elijo exámenes de materias distintas para combinar, when confirmo, then la app lo permite (a diferencia de US-024, acá no se está generando desde material nuevo con IA, así que no aplica la misma restricción), pero identifica de qué examen/materia venía cada pregunta en el detalle.

### US-027
- Given la app hoy usa la paleta actual de WPF-UI, when se aplica la paleta nueva, then se mantiene la distinción clara entre estado correcto/incorrecto/neutral (verde/rojo/gris u equivalente) que ya existe en la corrección de examen: la modernización es de tono/saturación/superficie, no cambia el significado de esos colores.
- Given creo o edito una Materia (US-023), when la estoy configurando, then puedo elegir un color para ella desde una paleta acotada de colores predefinidos (no un selector de color libre tipo RGB), para evitar combinaciones ilegibles.
- Given no elijo un color para una Materia, when se crea, then la app le asigna uno por defecto de forma automática (por ejemplo rotando entre los colores disponibles), nunca queda sin color.
- Given genero o abro un examen de una Materia con color asignado, when lo veo en pantalla, then el color de esa materia aparece como acento (por ejemplo en el encabezado, la barra de progreso o los chips), sin reemplazar los colores de correcto/incorrecto.
- Given cambio el color de una Materia después de haber generado exámenes con el color anterior, when reviso el Historial, then los exámenes ya rendidos de esa materia se actualizan visualmente al color nuevo (el color es un atributo de la Materia, no algo que se copie por examen).
- Given elijo un color para una Materia, when otras Materias ya usan colores, then la app no impide elegir un color repetido, pero sugiere primero los colores todavía no usados para ayudar a diferenciarlas de un vistazo.

### US-028
- Given se define la tipografía nueva, when se aplica en toda la app, then reemplaza la fuente actual de forma consistente (misma familia tipográfica en todas las pantallas), no solo en algunas.
- Given estoy en la pantalla de examen (pregunta y opciones), when se aplica el tamaño nuevo, then el texto se ve un poco más chico que el tamaño actual, pero se mantiene legible y no rompe la accesibilidad básica (contraste, no queda ilegible en pantallas chicas).
- Given estoy en cualquier otra pantalla (menú, historial, biblioteca, ajustes), when se aplica la tipografía nueva, then el tamaño de esas pantallas no necesariamente cambia junto con el de examen: el pedido de tamaño más chico es específico de la pantalla de examen.
- Given la fuente nueva elegida no está garantizada en todos los Windows, when la app arranca en una máquina que no la tiene instalada, then cae de forma prolija a una fuente del sistema similar (no rompe ni muestra texto con una tipografía completamente distinta sin control).

### US-029
- Given uso los mismos parámetros centralizados de duración/suavizado de US-011/US-016/US-021 (RN-11), when se ajustan las microinteracciones de US-029, then se reutilizan esos parámetros en vez de crear un sistema de animación paralelo.
- Given hoy el hover de un botón hace un pequeño zoom que se ve como un salto brusco, when se corrige, then el hover pasa a una transición suave (por ejemplo un cambio de color/sombra/escala progresiva bien interpolada) sin el salto perceptible actual.
- Given navego entre pantallas principales (Libros, Nuevo examen, Historial, Ajustes), when cambio de una a otra, then la transición entre pantallas es una animación suave y consistente (por ejemplo fundido o deslizamiento leve), no un cambio brusco/instantáneo.
- Given tengo activada la opción de "reducir movimiento" del sistema (o la que ya usa RN-11), when navego o paso el mouse por botones, then estas microinteracciones nuevas también la respetan igual que las animaciones existentes.
- Given paso el mouse por una tarjeta de acceso del menú principal (US-031) o por otro botón importante, when hago hover, then la tarjeta/botón hace un zoom leve y su texto crece mínimamente, de forma suave (reusando los parámetros de RN-11/RN-18), sin que el contenido (ícono, título, descripción) deje de verse en ningún momento del hover.
- Given un botón no tiene ya una descripción visible de forma permanente, when paso el mouse por encima y lo mantengo, then aparece una breve descripción de qué hace ese botón debajo de él mientras dura el hover, y desaparece al sacar el mouse.

### US-030
- Given estoy en el menú principal, when lo veo con la ventana en un tamaño normal o maximizada, then los 4 botones (Libros, Nuevo examen, Historial, Ajustes) se muestran en una grilla más centrada y espaciada (más "aire" entre tarjetas) en vez de pegados a un costado, cada uno con su ícono grande arriba y el texto abajo.
- Given estoy en la pantalla de examen, when reviso la pregunta y sus opciones, then la zona de pregunta/imagen y la zona de opciones quedan visualmente separadas (por ejemplo con una tarjeta contenedora con sombra/borde sutil), y la barra de progreso queda fija arriba en vez de desplazarse junto con el contenido al hacer scroll.
- Given estoy en el Historial, when veo la lista de exámenes rendidos, then se muestra como tarjetas (en vez de lista plana de texto) con una franja o acento del color de la Materia correspondiente (US-027), para identificar de un vistazo a qué materia pertenece cada examen sin tener que leer el título.
- Given estoy en Biblioteca, when veo mis libros/documentos subidos, then quedan agrupados visualmente por Materia (con el color de esa materia, US-027) en vez de una lista única mezclada, con la posibilidad de colapsar/expandir cada grupo de materia.
- Given aplico estos cambios de layout, when los reviso en distintos tamaños de ventana, then se respeta lo ya resuelto en US-017 (centrado y aprovechamiento del espacio en pantalla completa): estos layouts nuevos no vuelven a romper ese comportamiento.
- Given estoy en cualquier paso del asistente de Nuevo examen (Material, Alcance, Formato) o en otra pantalla con poco contenido, when el contenido no llena el alto de la ventana, then el bloque de contenido se centra verticalmente (o el layout se ajusta) en vez de quedar pegado arriba con una franja vacía grande abajo.
- Given estoy respondiendo una pregunta del examen, when comparo la tarjeta de la pregunta con las tarjetas de las opciones, then la pregunta tiene más peso visual (fondo o borde distinto) que las opciones, para diferenciarlas de un vistazo.
- Given elijo una opción de respuesta, when queda marcada, then se ve un estado de "seleccionado" claramente distinto al resto de las opciones (no solo un cambio sutil), consistente con los colores de correcto/incorrecto que ya se usan al corregir.
- Given veo tarjetas en cualquier pantalla (documentos, exámenes del historial, resumen de "vas a generar"), when las comparo entre sí, then hay una jerarquía visual clara entre una tarjeta informativa, una seleccionable y un resumen final (por ejemplo con distinto nivel de sombra/profundidad), no todas con el mismo tono.
- Given hay texto de ayuda debajo de un título de sección (por ejemplo las aclaraciones de Capítulos o Materia), when lo leo, then tiene suficiente espaciado/interlineado respecto al texto de arriba para no sentirse pegado.

### US-031
- Given estoy en el menú principal, when lo veo, then sigo teniendo los 4 accesos de navegación existentes (Libros, Nuevo examen, Historial, Ajustes), pero además veo accesos directos a acciones concretas: generar un examen nuevo, ver mis exámenes anteriores/historial, subir material nuevo (sin pasar primero por Biblioteca) y entrar a ajustes.
- Given toco el acceso directo de "generar examen", when se abre, then me lleva directo al asistente de Nuevo examen (paso Material), igual que hoy el botón de navegación.
- Given toco el acceso directo de "subir material nuevo", when se abre, then me lleva directo al flujo de agregar un archivo (el mismo que hoy se usa desde Biblioteca o desde el paso Material del asistente), sin pasos intermedios extra.
- Given tengo actividad reciente (por ejemplo el último examen rendido o el último material subido), when estoy en el menú principal, then veo esa información resumida (por ejemplo "Último examen: Tp2 Endocrino — 8/10") como parte de este menú más completo, no solo botones vacíos de navegación.
- Given todavía no rendí ningún examen ni subí ningún material, when entro al menú principal por primera vez, then los accesos directos igual están disponibles y invitan a la primera acción (por ejemplo "Subí tu primer material para empezar"), sin mostrarse rotos ni vacíos sin explicación.
- Given estos accesos directos nuevos conviven con los 4 botones de navegación, when reviso el menú, then no queda duplicado ni confuso: los accesos directos son atajos a la acción puntual, los botones de navegación siguen llevando a la sección completa.
- Given estoy en el menú principal, when veo cualquiera de las tarjetas de acceso directo, then su ícono, título y descripción breve están siempre visibles (no solo al pasar el mouse por encima): ninguna tarjeta queda vacía o en blanco a la espera del hover.
- Given estoy en el menú principal, when busco entender qué es la app, then hay un botón/acceso chico ("¿Qué es AutoExam?" o equivalente) que muestra una explicación breve, en lenguaje simple orientado a un estudiante nuevo, de para qué sirve la aplicación.

## Reglas de negocio
- **RN-1** — La escala de calificación no cambia: UBA 1 a 10, se aprueba con 4 (60% de aciertos). "7 o más" (US-013) significa nota ≥ 7, equivalente a ≥ 74% de aciertos.
- **RN-2** — Toda fuente nueva (Office, imágenes) usa el mismo flujo que un PDF: se suma al material, se elige alcance y formato, se genera con el mismo motor de preguntas y se corrige localmente.
- **RN-3** — El límite real de generación lo sigue poniendo la cuota del proveedor de IA. Las fuentes que viajan como imagen (fotos de apuntes, Office sin texto) consumen más cuota y pueden tardar más; la app debe informarlo antes o durante la generación.
- **RN-4** — Si una fuente no aporta material suficiente, no se crea un examen vacío: se explica el motivo.
- **RN-5** — El mensaje de US-013 es fijo y se distribuye en el release: no es configurable ni se puede desactivar desde la interfaz.
- **RN-6** — Cualquier borrado del historial (individual o total) exige confirmación explícita y recalcula las estadísticas del perfil.
- **RN-7** — Superficies existentes en revisión para US-011: transición entre secciones de la navegación principal; hover y pulsado de botones y chips; riel de pasos del asistente (línea de avance); baldosas del navegador de preguntas al cambiar de estado o de pregunta; entrada de la pantalla de Resultados; apertura y cierre de los avisos (InfoBar); anillos de progreso; alta y baja de ítems en las listas de Historial y Libros. *(la restricción original de "no agregar animaciones a superficies que hoy no animan" queda levantada por acuerdo explícito en US-016; ver RN-11.)*
- **RN-8** — En v1 las fuentes de Office admitidas son exclusivamente `.docx`, `.xlsx` y `.pptx`. No se les impone un límite propio de tamaño ni de páginas/diapositivas/filas: aplica únicamente la cuota general del proveedor de IA (RN-3).
- **RN-9** — Formatos de imagen admitidos (US-010): `.jpg` / `.jpeg` / `.png` de forma nativa, y `.heic` / `.heif` mediante conversión automática a un formato soportado antes del envío a la IA. Cualquier otro formato de imagen queda fuera de alcance.
- **RN-10** — El fallback de OCR de US-014 solo se dispara cuando el extractor de texto normal de la fuente no encontró contenido; si la fuente ya tiene texto extraíble, no se corre interpretación de imágenes sobre ella (evita gasto de cuota innecesario).
- **RN-11** — Las animaciones nuevas de US-016 usan los mismos parámetros centralizados de duración/suavizado que US-011 y respetan "reducir movimiento". Las superficies concretas a animar se definen junto con diseño antes de implementar.
- **RN-12** — Los emojis de US-015 se definen en una lista acotada de textos (no se aplican "a mansalva"); si un emoji no renderiza bien, se reemplaza por un equivalente antes de publicar.
- **RN-13** — El centrado de US-017 aplica un ancho máximo de contenido: en monitores anchos con la ventana maximizada, el contenido no se estira a todo el ancho, queda centrado con márgenes.
- **RN-14** — Las imágenes de referencia de US-018 son un complemento aleatorio del examen, no un requisito: la app nunca falla ni bloquea la generación por no poder conseguir una imagen para una pregunta puntual.
- **RN-15** — Buscar imágenes externas de referencia (fuera del propio material del alumno) consume cuota/red adicional y depende de un proveedor de búsqueda de imágenes; queda como capacidad opcional y configurable (se puede desactivar), separada de las imágenes extraídas del propio material, que no dependen de un servicio externo.
- **RN-16** — El resultado de "Probar conexión" (US-019) es siempre binario y explícito (éxito o error con motivo); nunca queda en un estado neutro/sin respuesta visible una vez terminada la prueba.
- **RN-17** — El resumen de "de qué trata" (US-020) se genera bajo demanda (al tocar el botón), no automáticamente al subir el material, para no gastar cuota de IA en materiales que el alumno no llega a usar.
- **RN-18** — El hover de US-021 usa los mismos parámetros centralizados de animación que US-011/US-016 (ver RN-11) y respeta "reducir movimiento".
- **RN-19** — US-022 no agrega un pipeline nuevo: reutiliza el extractor de texto de US-008, el motor de interpretación de imágenes de US-010/US-014 y el armado de preguntas con imagen de US-018, aplicados sección por sección dentro de un mismo archivo en vez de a un archivo entero de un solo tipo.
- **RN-20** — (Fix aplicado en `PdfExtractorService.cs`) La extracción de figuras para US-018 ya no depende de que la página tenga texto extraíble: se evalúa en cualquier página, con o sin texto, dentro del mismo PDF. Para seguir evitando que la página entera escaneada se use como "figura" (revelaría la respuesta), se descarta cualquier imagen que ocupe ≥75% del área de la página (`OpcionesExtraccion.MaxProporcionPaginaParaFigura`); solo imágenes menores a ese umbral —diagramas, ilustraciones puntuales— quedan disponibles como referencia de una pregunta.
- **RN-21** — (Fix aplicado en `GeminiApiService.cs`) Caso material 100% fotografiado (cada página es una única foto/escaneo completo, sin ninguna figura embebida separada — ej. apuntes manuscritos fotografiados): como RN-20 no encuentra ninguna figura en ese caso (no hay una imagen más chica que recortar dentro de la página), se admite como excepción que el modelo use la página escaneada completa como imagen de referencia, pero solo cuando esa página muestra visualmente un esquema/diagrama/dibujo (no una página de puro texto escrito), como máximo en 1 o 2 preguntas de todo el examen, y con una consigna que evalúe el diagrama en sí (no "qué dice el texto de la página", que revelaría la respuesta). Esta excepción solo se activa cuando no hay ninguna figura separada disponible (si las hay, se usan esas primero, como en RN-20).

- **RN-22** — Todo material existente antes de US-023 se migra a una Materia por defecto al actualizar; ningún libro/documento queda "huérfano" sin materia.
- **RN-23** — La selección múltiple de documentos (US-024) es siempre dentro de una sola Materia; no se combinan documentos de materias distintas en un mismo examen.
- **RN-24** — Al generar un examen combinando varios documentos, cada pregunta conserva la referencia de en qué documento se originó (no se pierde la trazabilidad que ya existía por página/documento individual).
- **RN-25** — (Cambio técnico base de US-025) `ExamenRendido` pasa a persistir la lista completa de `Pregunta` de ese intento, no solo el resumen agregado. Los exámenes rendidos antes de este cambio no tienen ese detalle y se informan como tales (RN-26), no se intenta reconstruirlo.
- **RN-26** — Un examen del historial sin detalle guardado (rendido con una versión anterior a US-025) muestra un aviso claro al intentar abrirlo, nunca una lista vacía sin explicación ni un error.
- **RN-27** — Un examen combinado (US-026) no consume cuota de IA ni depende de conexión: se arma localmente a partir de preguntas ya generadas y guardadas. Nunca repite la misma pregunta dos veces dentro de un mismo examen combinado.
- **RN-28** — Borrar un examen individual (US-012) también borra su detalle de preguntas e imágenes guardado por US-025; si ese examen había sido usado como fuente de un examen combinado (US-026) ya generado, el combinado ya generado no se ve afectado retroactivamente (conserva sus propias preguntas copiadas al momento de combinar).
- **RN-29** — El punto de entrada principal de US-026 es el asistente de Nuevo examen (junto a la opción de elegir un libro/documento como fuente): ahí siempre está disponible, sin depender de si el usuario llega desde Historial o directamente desde el menú principal. Si además se ofrece un acceso alternativo desde Historial, es un atajo al mismo flujo, no una pantalla distinta.
- **RN-30** — El color de Materia (US-027) es un atributo de `Materia`/`Libro` (no de `ExamenRendido`): se resuelve en tiempo de visualización a partir de la materia del examen, para que un cambio de color se refleje también en exámenes ya rendidos de esa materia.
- **RN-31** — Los colores por Materia salen de una paleta predefinida y accesible (contraste suficiente sobre fondo claro y oscuro), no de un selector RGB libre; se define junto con diseño antes de implementar.
- **RN-32** — La tipografía nueva de US-028 y el tamaño reducido en la pantalla de examen usan variables/recursos centralizados de estilo (no valores sueltos repetidos por pantalla), para que ajustar el tamaño o la fuente a futuro no requiera tocar cada vista una por una.
- **RN-33** — Las microinteracciones nuevas de US-029 (transición entre pantallas, corrección del zoom de hover) reutilizan los parámetros centralizados de duración/suavizado ya definidos por RN-11/RN-18 y respetan "reducir movimiento"; no se define un sistema de animación aparte.
- **RN-34** — Los layouts nuevos de US-030 (tarjetas de Historial y grupos de Biblioteca) reutilizan el color de Materia definido por US-027/RN-30: no se define un esquema de color de tarjeta separado del ya usado para identificar materias.
- **RN-35** — Los cambios de layout de US-030 no reabren ni contradicen lo ya resuelto por US-017 (centrado/aprovechamiento de espacio en pantalla completa): se construyen sobre esa base, no la reemplazan.
- **RN-36** — Los accesos directos del menú principal (US-031) son atajos de navegación a pantallas/flujos ya existentes (Nuevo examen, Historial, agregar material, Ajustes): no crean lógica de negocio nueva ni una copia paralela de esas pantallas.
- **RN-37** — El resumen de actividad reciente del menú (US-031) es de solo lectura: no permite corregir ni interactuar con el examen/material mostrado desde ahí, solo lleva a la pantalla correspondiente si se lo toca.
- **RN-38** — El contenido esencial de una tarjeta o botón (ícono, título, descripción breve ya definida como parte del diseño) nunca depende exclusivamente del estado de hover para mostrarse: el hover solo agrega el efecto de zoom/crecimiento de texto (US-029) y, cuando corresponde, una descripción adicional que no tenía lugar fijo en el layout (tooltip).
- **RN-39** — El texto de "¿Qué es AutoExam?" (US-031) es fijo y se define una sola vez junto con el resto del contenido de la interfaz; no depende de conexión a Gemini ni se genera dinámicamente.

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
- Rediseño visual completo de la app (US-015/016/017 son ajustes puntuales, no un rebranding).
- Emojis animados/personalizados (solo emojis Unicode estándar, US-015).
- Diseño responsive para pantallas táctiles o resoluciones no soportadas hoy por la app de escritorio.
- Personalizar, traducir, condicionar, ocultar o mostrar en revancha el mensaje de US-013.
- Sincronización en la nube o entre dispositivos de las nuevas fuentes.
- Verificar licencia/derechos de uso de imágenes externas buscadas para US-018: son solo apoyo visual de estudio personal, no se garantiza que sean libres de derechos ni se citan fuentes.
- Elegir manualmente qué imagen específica acompaña una pregunta (US-018 es automático/aleatorio, no un buscador de imágenes para el alumno).
- Generar imágenes con IA (las imágenes de referencia se extraen del material o se buscan ya existentes; no se crean desde cero).
- Subcarpetas o jerarquías dentro de una Materia (US-023 es un solo nivel: Materia → documentos, sin sub-temas anidados).
- Mover un documento de una Materia a otra arrastrándolo (drag & drop); alcanza con poder reasignarlo desde un menú/acción.
- Combinar en un mismo examen documentos de materias distintas (RN-23).
- Reconstruir el detalle de preguntas de exámenes rendidos ANTES de US-025 (RN-25/26): esos quedan solo con su resumen, como hoy.
- Editar o corregir a mano el detalle de un examen ya rendido desde su vista de historial (US-025 es de solo lectura, ya cubierto en general por "Editar exámenes ya rendidos" más arriba).
- Elegir a mano preguntas puntuales para el examen combinado (US-026 es siempre aleatorio dentro de los exámenes elegidos, no un selector pregunta por pregunta).
- Modo revancha sobre un examen combinado que a su vez combine otro combinado (encadenar combinados de combinados); un examen combinado se arma solo a partir de exámenes "originales" rendidos.

## Preguntas abiertas (bloquean)
Ninguna. Las dos que bloqueaban quedaron resueltas:
- US-013: el mensaje literal va en el release distribuido por actualización automática a terceros, sin flag ni configuración para ocultarlo (criterio de aceptación firme en US-013 y RN-5).
- US-008: v1 admite únicamente `.docx` / `.xlsx` / `.pptx`; los formatos legacy `.doc` / `.xls` / `.ppt` quedan fuera de alcance (RN-8 y Fuera de alcance).

## Supuestos
Un material puede tener varias imágenes pero una sola fuente por examen (salvo el set de imágenes); el mensaje de US-013 se muestra solo en el resultado del intento original; se respeta el "reducir movimiento" del sistema operativo; el ítem de interfaz hoy llamado "Libro" se generaliza a "material" o se mantiene el término, a decidir en diseño.

Sugerencia: definir con analista-técnico el contrato del pipeline de extracción multi-formato (texto e imágenes), la conversión de HEIC/HEIF y cómo se arma el alcance/recorte que se envía al servicio de IA cuando la fuente no es un PDF.
