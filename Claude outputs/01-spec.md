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
- **US-032** (repaso inteligente: armar un examen corto con las preguntas que más se fallaron entre varios exámenes de un mismo material), **US-033** (gráfico de evolución de notas por Materia, no solo el promedio general) y **US-034** (modo cronómetro real: examen a tiempo límite total, no por pregunta).
- **US-035** (buscador por texto dentro de Biblioteca e Historial).
- **US-036** (atajos de teclado durante el examen: números para elegir opción, flechas para avanzar/retroceder) y **US-037** (exportar/importar un examen generado para compartirlo con un compañero sin gastar cuota de Gemini).
- **US-038** (botón "explicame mejor" en la corrección: pedirle a Gemini una explicación más detallada de una pregunta puntual) y **US-039** (tiempo total de estudio acumulado, visible en el Historial).
- **US-040** (botón de "Notas de versión" en Ajustes: qué se agregó o cambió en cada actualización, en lenguaje simple).
- **US-041** (rediseño visual del menú principal según el mockup aprobado en Claude Design: se mantiene la grilla 2x2 de las 4 tarjetas de acceso directo, pero con el estilo de íconos, jerarquía y hover del mockup).
- **US-042** (rediseño de la pantalla de configuración inicial —carga de la clave de Gemini— según mockup aprobado, y cambio del esquema de múltiples claves: pasa de un solo campo con las claves separadas por coma a una pestaña individual por clave).
- **US-043** (rediseño visual de los 3 pasos del asistente de Nuevo examen —Material, Alcance y Formato— según mockup aprobado: mismo contenido y opciones, con más pulido en el riel de pasos, las tarjetas seleccionables y sus animaciones de hover/click).
- **US-044** (rediseño visual del Historial según mockup aprobado: layout en dos columnas, resumen con anillo de progreso, pastillas de color para correctas/incorrectas/salteadas, y nuevas animaciones de hover y despliegue).
- **US-045** (rediseño visual de Biblioteca según mockup aprobado: el detalle del libro pasa a ocupar todo el ancho disponible en vez de dejar espacio vacío, con header grande y dos columnas de contenido debajo; mismo fondo con luz violeta tenue y mismas animaciones que el resto de las pantallas rediseñadas, excepto la pantalla de configuración inicial que queda igual).
- **US-046** (Ajustes: sección "Datos y almacenamiento" para ver cuánto espacio ocupan libros/historial/temporales, abrir esa carpeta y vaciar el caché sin perder datos), **US-047** (Ajustes: botón "Restaurar valores de fábrica" que resetea preferencias de interfaz sin borrar libros, historial ni claves de Gemini).
- **US-048** (Ajustes: control de zoom/tamaño de la interfaz para toda la app, con el valor recordado entre sesiones) y **US-049** (Ajustes: elegir un tema de color general de la app entre varias paletas predefinidas, independiente del color por Materia ya existente).
- **US-050** (Ajustes: activar o desactivar las notificaciones informativas de la app) y **US-051** (Ajustes: copia de seguridad manual y local para exportar e importar libros, historial y configuración en un solo archivo).
- **US-052** (Ajustes: elegir qué modelo de Gemini usa la app para generar exámenes), **US-053** (Ajustes: botón "Buscar actualizaciones" manual, además de la actualización automática que ya existe).
- **US-054** (Ajustes: "Reducir movimiento" propio de la app, además de respetar la preferencia del sistema operativo) y **US-055** (recordar tamaño y posición de la ventana entre sesiones, sin acción del usuario).
- **US-056** (ya implementado; se documenta para que no se pierda en el rediseño: el control existente de "Preguntas por lote" al generar con Gemini pasa a vivir en una sección "Avanzado" de Ajustes, sin cambiar su comportamiento actual).
- **US-057** (rediseño visual de la pantalla de Examen —en curso y corrección— según mockup aprobado: más calma y minimalista, sin banner grande de atajos de teclado, con anillo de nota más templado y tarjetas de corrección menos saturadas; es exclusivamente visual, ninguna funcionalidad cambia).
- **US-058** (rediseño del asistente de Nuevo examen —Material, Alcance y Formato— según mockup aprobado, yendo más allá del pulido visual de US-043: cada grupo de opciones pasa a vivir en su propia tarjeta con encabezado, el riel de pasos muestra un resumen de lo ya elegido en cada paso, y se agrega un panel "Tu examen" fijo a la derecha que acompaña los 3 pasos mostrando materia/material/alcance/formato a medida que se completan y termina en el botón de generar; ninguna opción, validación ni comportamiento de generación cambia, solo cómo se agrupa y se acompaña el recorrido).

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

### US-032 — Repaso inteligente de preguntas falladas
Como alumno, quiero poder armar un examen corto usando las preguntas que más fallé entre varios exámenes que ya rendí de un mismo material o materia, para repasar puntualmente lo que más me cuesta en vez de repasar todo por igual.

### US-033 — Gráfico de evolución de notas por Materia
Como alumno, quiero ver un gráfico de cómo evolucionaron mis notas a lo largo del tiempo separado por Materia, para saber si estoy mejorando o estancado en cada una, no solo mi promedio general.

### US-034 — Modo cronómetro real (examen a tiempo límite total)
Como alumno, quiero poder rendir un examen con un tiempo total límite (por ejemplo 40 minutos para todo el examen, no por pregunta), para practicar el ritmo real de un parcial.

### US-035 — Buscador por texto en Biblioteca e Historial
Como alumno, quiero poder buscar por texto dentro de mi Biblioteca y de mi Historial (por nombre de material, materia o tema), para encontrar algo puntual rápido cuando ya tengo muchos documentos o exámenes rendidos.

### US-036 — Atajos de teclado durante el examen
Como alumno, quiero poder usar el teclado durante el examen (números para elegir una opción, flechas para avanzar o retroceder de pregunta), para rendir más rápido sin depender del mouse en cada click.

### US-037 — Compartir un examen generado con un compañero
Como alumno, quiero poder exportar un examen que ya generé y que un compañero lo importe en su propia AutoExam, para que pueda rendir el mismo examen sin gastar su propia cuota de Gemini generándolo de nuevo.

### US-038 — Botón "Explicame mejor" en la corrección
Como alumno, quiero poder pedirle a Gemini una explicación más larga y en otras palabras de una pregunta puntual que rendí, para entender mejor por qué la respuesta correcta es esa cuando el análisis breve que ya se muestra al corregir no me alcanza.

### US-039 — Tiempo total de estudio acumulado
Como alumno, quiero ver cuánto tiempo acumulado pasé rindiendo exámenes (por ejemplo esta semana o en total), además de la cantidad de exámenes y mi nota, para tener una noción de mi esfuerzo de estudio más allá de los resultados.

### US-040 — Notas de versión
Como usuario, quiero un botón de "Notas de versión" en Ajustes donde se publique, de forma breve y en lenguaje simple (entendible para cualquiera, no solo para quien programó la app), qué se agregó o cambió en cada actualización, para saber qué es nuevo cuando la app se actualiza sin tener que preguntarle a nadie.

### US-046 — Ver y gestionar dónde se guardan los datos
Como usuario, quiero ver en Ajustes dónde guarda AutoExam mis libros, mi historial y sus archivos temporales, y poder abrir esa carpeta o vaciar el caché sin perder mis datos, para entender qué espacio ocupa la app y liberarlo si lo necesito.

### US-047 — Restaurar valores de fábrica
Como usuario, quiero un botón en Ajustes que restaure la configuración de la app a sus valores iniciales, para volver a empezar de cero con mis preferencias sin tener que desinstalar y reinstalar AutoExam.

### US-048 — Ajustar el zoom/tamaño de la interfaz
Como usuario, quiero poder aumentar o achicar el tamaño de todo el contenido de la app desde Ajustes, para que se vea cómodo según mi pantalla o mi vista.

### US-049 — Elegir un tema de color general de la app
Como usuario, quiero poder elegir entre varios temas de color predefinidos para la app, además del violeta actual, para personalizar cómo se ve AutoExam más allá del color de cada Materia.

### US-050 — Notificaciones de la app
Como usuario, quiero poder activar o desactivar los avisos informativos que muestra AutoExam (por ejemplo cuando termina de generar un examen), para decidir si quiero que me interrumpan o no.

### US-051 — Copia de seguridad manual
Como usuario, quiero poder exportar mis libros, mi historial y mi configuración a un solo archivo, y volver a importarlos, para tener un respaldo propio o pasar mis datos a otra computadora.

### US-052 — Elegir el modelo de Gemini
Como usuario, quiero poder elegir qué modelo de Gemini usa AutoExam para generar exámenes, para adaptar velocidad y calidad a lo que necesito en cada momento.

### US-053 — Buscar actualizaciones manualmente
Como usuario, quiero un botón en Ajustes para buscar actualizaciones ahora mismo, además de la actualización automática que ya existe, para no tener que esperar al chequeo automático si sé que salió una versión nueva.

### US-054 — Reducir movimiento dentro de la app
Como usuario, quiero poder apagar las animaciones de AutoExam desde sus propios Ajustes, además de que la app ya respete la preferencia de "reducir movimiento" del sistema operativo, para desactivarlas puntualmente sin cambiar la configuración de todo Windows.

### US-055 — Recordar tamaño y posición de la ventana
Como usuario, quiero que AutoExam recuerde el tamaño y la posición en que dejé la ventana, para no tener que acomodarla de nuevo cada vez que la abro.

### US-056 — (ya implementado) Preguntas por lote al generar con Gemini, reflejado en el nuevo Ajustes
Como usuario, ya puedo configurar hoy cuántas preguntas como mínimo le pide AutoExam a Gemini en cada petición al generar un examen; esta historia no cambia ese comportamiento, solo asegura que el control siga presente (en una sección "Avanzado") dentro del rediseño de Ajustes.

### US-057 — Rediseño visual de la pantalla de Examen, más calma y minimalista
Como alumno, quiero que la pantalla de Examen (mientras rindo y al ver la corrección) se sienta más calma, prolija y minimalista, sin elementos que abrumen o distraigan, para poder concentrarme en responder y entender mi resultado sin ruido visual.

### US-058 — Rediseño del asistente de Nuevo examen para que se entienda el recorrido
Como alumno, quiero que el asistente de Nuevo examen (Material, Alcance y Formato) se vea más armónico y prolijo, con cada grupo de opciones bien delimitado y un resumen de lo que ya elegí siempre visible, para entender claramente cómo seleccionar y cómo se llega a generar el examen, en vez de sentir que las opciones están sueltas o "en el aire".

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
- Given estoy en Biblioteca, when veo la tarjeta de "Materias" (para renombrar/eliminar), then también muestra el color de cada Materia (por ejemplo un punto o chip de color junto al nombre), igual que ya se ve en las franjas de color de los grupos de la lista de abajo.
- Given cualquier texto de la interfaz muestra una cantidad (por ejemplo "1 módulo" o "2 módulos", "1 examen rendido" o "2 exámenes rendidos"), when la cantidad es 1, then el texto usa la forma singular correcta en español, no la forma plural aplicada de forma fija (por ejemplo, hoy "1 modulos" está mal escrito y debería decir "1 módulo").

### US-031
- Given estoy en el menú principal, when lo veo, then sigo teniendo los 4 accesos de navegación existentes (Libros, Nuevo examen, Historial, Ajustes), pero además veo accesos directos a acciones concretas: generar un examen nuevo, ver mis exámenes anteriores/historial, subir material nuevo (sin pasar primero por Biblioteca) y entrar a ajustes.
- Given toco el acceso directo de "generar examen", when se abre, then me lleva directo al asistente de Nuevo examen (paso Material), igual que hoy el botón de navegación.
- Given toco el acceso directo de "subir material nuevo", when se abre, then me lleva directo al flujo de agregar un archivo (el mismo que hoy se usa desde Biblioteca o desde el paso Material del asistente), sin pasos intermedios extra.
- Given tengo actividad reciente (por ejemplo el último examen rendido o el último material subido), when estoy en el menú principal, then veo esa información resumida (por ejemplo "Último examen: Tp2 Endocrino — 8/10") como parte de este menú más completo, no solo botones vacíos de navegación.
- Given todavía no rendí ningún examen ni subí ningún material, when entro al menú principal por primera vez, then los accesos directos igual están disponibles y invitan a la primera acción (por ejemplo "Subí tu primer material para empezar"), sin mostrarse rotos ni vacíos sin explicación.
- Given estos accesos directos nuevos conviven con los 4 botones de navegación, when reviso el menú, then no queda duplicado ni confuso: los accesos directos son atajos a la acción puntual, los botones de navegación siguen llevando a la sección completa.
- Given estoy en el menú principal, when veo cualquiera de las tarjetas de acceso directo, then su ícono, título y descripción breve están siempre visibles (no solo al pasar el mouse por encima): ninguna tarjeta queda vacía o en blanco a la espera del hover.
- Given estoy en el menú principal, when busco entender qué es la app, then hay un botón/acceso chico ("¿Qué es AutoExam?" o equivalente) que muestra una explicación breve, en lenguaje simple orientado a un estudiante nuevo, de para qué sirve la aplicación.

### US-032
- Given tengo dos o más exámenes rendidos con detalle guardado (US-025) de un mismo material o de una misma Materia, when entro a "Repaso inteligente", then puedo elegir ese material/materia y la app arma un examen nuevo usando las preguntas que marqué incorrectas (o salteadas) en esos intentos.
- Given una misma pregunta aparece fallada en más de un examen, when se arma el repaso inteligente, then esa pregunta entra una sola vez (no se repite).
- Given no tengo suficientes preguntas falladas para armar el repaso pedido, when lo genero, then sale con todas las disponibles y la app avisa que se ajustó la cantidad, igual que en US-026.
- Given respondo bien una pregunta en el repaso inteligente, when reviso mi historial de aciertos por pregunta, then eso queda registrado: si esa misma pregunta vuelve a aparecer en un futuro repaso, ya no cuenta como "fallada" salvo que la vuelva a errar.
- Given el repaso inteligente usa preguntas ya generadas antes, when lo genero, then es instantáneo y no consume cuota de IA, igual que un examen combinado de US-026.

### US-033
- Given tengo al menos dos exámenes rendidos de una misma Materia, when entro a ver su evolución, then veo un gráfico simple (por ejemplo de línea) con la nota o el porcentaje de aciertos de cada intento en el tiempo, para esa materia puntual.
- Given tengo varias Materias con exámenes rendidos, when reviso evolución, then puedo elegir de cuál materia ver el gráfico (no solo un gráfico general mezclado).
- Given todavía no tengo al menos dos exámenes rendidos de una materia, when quiero ver su evolución, then la app lo indica con claridad ("rendí al menos dos exámenes de esta materia para ver tu evolución") en vez de mostrar un gráfico vacío o roto.
- Given el color de la Materia ya está definido (US-027), when veo su gráfico de evolución, then usa ese mismo color como acento, para mantener consistencia visual.
- Given tengo dos o más Materias con al menos dos exámenes rendidos cada una, when reviso "Mi evolución por materia", then puedo superponer varias Materias en el mismo gráfico (no solo verlas una por vez), cada una dibujada con su propio color de Materia (US-027) para poder compararlas de un vistazo.
- Given superpongo varias Materias en el gráfico, when las reviso, then hay una referencia (leyenda o los mismos chips de Materia ya usados para filtrar) que indica qué línea de color corresponde a cada Materia.

### US-034
- Given estoy en el paso Formato del asistente de Nuevo examen, when configuro el examen, then puedo elegir "modo cronómetro" con un tiempo límite total (por ejemplo 20, 40, 60 minutos, o un valor a mano), además del modo actual sin límite de tiempo global.
- Given rindo un examen en modo cronómetro, when se acaba el tiempo, then el examen se entrega automáticamente con las respuestas marcadas hasta ese momento (las preguntas sin responder quedan como salteadas), sin perder lo ya contestado.
- Given estoy rindiendo en modo cronómetro, when reviso la pantalla de examen, then veo la cuenta regresiva del tiempo total de forma clara, con un aviso visual cuando queda poco tiempo (por ejemplo los últimos 2 minutos).
- Given genero un examen combinado (US-026) o de repaso inteligente (US-032), when configuro el formato, then también puedo elegir modo cronómetro para esos exámenes, no solo para los generados con IA.
- Given elijo "Sin límite" en el modo cronómetro, when reviso esa sección, then no se muestra ningún campo numérico de minutos ni ningún valor tipo "0" al lado: ese campo personalizado solo aparece si el usuario elige explícitamente definir un tiempo a mano, distinto de los presets (20/40/60/90 min).

### US-035
- Given estoy en Biblioteca, when escribo en el buscador, then la lista se filtra en tiempo real por título del material, Materia o nombre de archivo original.
- Given estoy en Historial, when escribo en el buscador, then la lista se filtra en tiempo real por título del examen, Materia o alcance/tema.
- Given la búsqueda no encuentra resultados, when reviso la lista filtrada, then la app lo indica con claridad ("no se encontró nada para 'x'") en vez de mostrar una lista vacía sin explicación.
- Given borro el texto del buscador, when queda vacío, then la lista vuelve a mostrar todos los elementos, sin filtro aplicado.

### US-036
- Given entro a la pantalla de examen, when todavía no toqué nada con el mouse ni con el teclado, then los atajos de teclado (números/letras para elegir opción, flechas para avanzar/retroceder) ya están activos por defecto: no hace falta apretar ningún botón, tocar "Entendido" ni darle foco a nada en particular de antemano para que funcionen.
- Given estoy respondiendo una pregunta de opción múltiple, when presiono una tecla numérica (1, 2, 3, 4) o de letra (A, B, C, D) correspondiente a una opción visible, then esa opción queda seleccionada, igual que si la tocara con el mouse.
- Given ya respondí o quiero pasar de pregunta, when presiono la flecha derecha (o similar), then avanzo a la siguiente pregunta; con la flecha izquierda retrocedo a la anterior.
- Given estoy usando atajos de teclado, when un campo de texto tiene el foco (por ejemplo si hubiera texto libre en alguna pantalla), then los atajos no interfieren con lo que estoy escribiendo ahí.
- Given no conozco los atajos, when entro por primera vez a un examen, then hay una referencia visible pero discreta de qué atajos existen (por ejemplo un texto chico o un ícono de ayuda), sin que estorbe la pantalla; esto es solo informativo, cerrarlo o no verlo nunca desactiva los atajos.
- Given la referencia de atajos muestra un círculo o ícono por cada tecla disponible, when la veo, then cada círculo tiene visible adentro el número o letra real que representa (1, 2, 3, 4 o A, B, C, D), no un círculo vacío sin indicar qué tecla es.

### US-037
- Given ya generé un examen (con IA, combinado o de repaso), when quiero compartirlo, then puedo exportarlo a un archivo (por ejemplo un `.json` propio de AutoExam) que contiene las preguntas, opciones, respuestas correctas y justificaciones, sin datos personales míos (mi historial o mis notas no se incluyen).
- Given un compañero me pasa un archivo exportado de AutoExam, when lo importo desde mi propia app, then puedo rendir ese examen igual que uno generado por mí, y al corregirlo se guarda en mi Historial como cualquier otro.
- Given intento importar un archivo que no es un examen válido de AutoExam (corrupto o de otra versión incompatible), when lo intento abrir, then la app lo rechaza con un mensaje claro, sin romperse.
- Given el examen exportado tenía preguntas con imagen de referencia (US-018), when se exporta e importa, then las imágenes viajan incluidas (por ejemplo embebidas o en un paquete junto al archivo), no se pierden ni quedan rotas del otro lado.

### US-038
- Given estoy revisando la corrección de un examen (recién rendido o desde el detalle del Historial, US-025), when abro una pregunta puntual, then veo un botón "Explicame mejor" además del análisis breve que ya se muestra por opción.
- Given toco "Explicame mejor", when se genera la respuesta, then Gemini devuelve una explicación más extendida y en otras palabras de por qué la opción correcta lo es y por qué las demás no, usando el mismo contexto/material de esa pregunta.
- Given pido una explicación extendida, when se está generando, then veo un indicador de carga y no se bloquea el resto de la pantalla; si falla (sin conexión, cuota agotada), se informa con claridad, igual que otros usos de Gemini en la app.
- Given ya pedí la explicación extendida de una pregunta, when vuelvo a entrar a esa misma pregunta más tarde (por ejemplo desde el detalle del Historial), then la explicación ya generada queda guardada y se muestra directo, sin tener que volver a gastar cuota pidiéndola de nuevo.
- Given pido explicaciones extendidas repetidas veces, when reviso el consumo, then esto también cuenta contra la cuota general del proveedor de IA (RN-3), igual que generar preguntas.

### US-039
- Given rindo un examen (de cualquier tipo: con IA, combinado, de repaso o compartido), when se guarda en el Historial, then también se registra cuánto duró ese intento (ya existe `DuracionSegundos` en `ExamenRendido`) como parte del cálculo de tiempo acumulado.
- Given entro al Historial, when reviso el resumen general, then veo el tiempo total acumulado de estudio (por ejemplo "3h 20min en total"), sumando la duración de todos los exámenes rendidos.
- Given quiero ver mi esfuerzo reciente, when reviso ese mismo resumen, then también puedo ver el tiempo acumulado de un período más corto (por ejemplo esta semana), no solo el total histórico.
- Given todavía no rendí ningún examen, when reviso el Historial, then el tiempo acumulado se muestra en 0 o no se muestra, sin romperse ni mostrar un valor sin sentido.

### US-040
- Given estoy en Ajustes, when busco información de la versión instalada, then hay un botón "Notas de versión" (o equivalente) cerca de donde ya se muestra el número de versión actual.
- Given toco ese botón, when se abre, then veo las notas de la versión actual: una lista breve de qué se agregó, cambió o arregló, redactada en lenguaje simple orientado al alumno que usa la app (no jerga técnica ni mensajes de commit de git copiados tal cual).
- Given hay versiones anteriores con notas cargadas, when reviso esta pantalla, then también puedo ver el historial de versiones previas (orden cronológico, la más reciente arriba), no solo la actual.
- Given se instala una actualización nueva, when abro "Notas de versión" después de actualizar, then ya están disponibles las notas de esa versión nueva sin necesitar conexión a internet (vienen incluidas en el propio instalador/build, no se buscan en el momento).
- Given una versión todavía no tiene notas cargadas (por ejemplo un build de prueba), when la reviso, then la app lo indica con claridad ("todavía no hay notas para esta versión") en vez de mostrar la sección vacía sin explicación.

### US-041
- Given estoy en el menú principal, when veo las 4 tarjetas de acceso directo (Generar examen, Subir material, Ver historial, Ajustes), then siguen ordenadas en grilla 2x2 como hoy (US-031): esta historia NO cambia esa disposición, solo el estilo visual de cada tarjeta.
- Given cada tarjeta de la grilla, when la reviso, then el ícono queda dentro de un cuadrado con esquinas redondeadas y fondo con degradado violeta (en vez del ícono suelto actual), con el título en negrita y la descripción breve en gris debajo, dentro de la misma tarjeta.
- Given "Generar examen" es la acción más usada, when comparo su tarjeta con las otras tres, then se distingue visualmente como la principal (fondo con un tinte violeta más marcado que el resto, que usan un fondo más neutro/oscuro), sin cambiar su contenido, tamaño ni su acción.
- Given reviso el panel de "Últimos exámenes" al lado de la grilla, when veo cada ítem de la lista, then tiene una barra vertical de acento a la izquierda y la nota de ese examen se muestra como un número al costado derecho de la fila, en un tono neutro/blanco acorde a la paleta (no en rojo), manteniendo título, fecha y aciertos como ya se muestran.
- Given paso el mouse por encima de cualquiera de las 4 tarjetas del menú (o de un ítem de "Últimos exámenes"), when hago hover, then el elemento hace un zoom mínimo (sutil, no exagerado) y se le agrega un borde redondeado de color violeta alrededor, además de la animación de hover que ya existe (US-029); al sacar el mouse, vuelve suavemente a su estado normal.
- Given este rediseño solo toca la apariencia visual del menú principal, when reviso el comportamiento, then ningún acceso directo, navegación, resumen de actividad ni el botón "¿Qué es AutoExam?" (US-031) cambia su funcionalidad, ni se agregan textos o datos nuevos que no estaban antes (por ejemplo cantidad de libros o de exámenes rendidos al lado de cada tarjeta): solo cambia el estilo visual descripto acá.
- Given hay un mockup de referencia (`Inicio AutoExam.pdf`, generado en Claude Design) adjunto a este pedido, when se implementa esta historia, then el resultado final respeta ese mockup en colores, espaciado e íconos tanto como sea razonable dentro de la paleta y componentes ya definidos en US-027/US-028 y dentro de la grilla 2x2 existente, priorizando la referencia visual por sobre la descripción textual de este documento ante cualquier ambigüedad que no sea la disposición en grilla (aclarada en el primer criterio).

### US-042
- Given estoy en la pantalla de configuración inicial (primera vez que abro la app, sin clave cargada), when la veo, then tiene el nuevo diseño: título "AutoExam" grande con su descripción corta a la izquierda, un ícono circular con la inicial "A" a la derecha con un detalle decorativo sutil de fondo, el campo de clave con su botón de mostrar/ocultar, y una tarjeta separada a la derecha con el botón "Verificar y empezar", el estado de la prueba de conexión con su barra de progreso, el link "Conseguí una gratis en Google AI Studio" y, más abajo, "Entrar sin verificar".
- Given tengo una sola clave de Gemini cargada, when reviso el campo de clave, then veo una pestaña "clave 1" ya seleccionada con esa clave, y un botón "+ clave 2" para agregar otra si quiero, en vez del cuadro de texto único donde antes se pegaban todas separadas por coma.
- Given agrego una clave nueva con "+ clave N", when la cargo, then se crea su propia pestaña con su propio campo de texto y su propio botón de mostrar/ocultar, independiente de las demás pestañas ya cargadas.
- Given tengo dos o más claves cargadas como pestañas, when una se queda sin cuota diaria, then la app sigue probando con la clave de la siguiente pestaña en el mismo orden en que están ordenadas, igual que hoy funciona con el texto separado por comas (mismo comportamiento de fallback, solo cambia cómo se cargan y se ven).
- Given tengo más de una pestaña de clave cargada, when quiero borrar una que ya no uso (que no sea la única que queda), then puedo eliminarla desde su propia pestaña, sin afectar las claves de las demás.
- Given ya tenía una o más claves guardadas en el formato viejo (un texto con las claves separadas por coma), when abro la app por primera vez después de esta actualización, then esas claves se migran automáticamente a una pestaña por cada una, sin que tenga que volver a cargarlas a mano ni perder ninguna.
- Given este mismo campo de clave también se edita desde Ajustes (US-031), when entro a Ajustes después de esta actualización, then ahí también se usa el esquema de pestañas por clave, para que sea consistente en toda la app y no haya dos formas distintas de cargar la clave según la pantalla.
- Given hay un mockup de referencia (`AutoExam Setup.pdf`, generado en Claude Design) adjunto a este pedido, when se implementa esta historia, then el resultado final respeta ese mockup en distribución, colores y espaciado dentro de la paleta y componentes ya definidos en US-027/US-028.

### US-043
- Given estoy en cualquiera de los 3 pasos del asistente de Nuevo examen (Material, Alcance o Formato), when reviso el riel de pasos de arriba, then cada paso completado muestra un tilde/check en vez del número, el paso actual queda resaltado en violeta con su número, y hay una pequeña etiqueta arriba de la tarjeta principal indicando el paso actual (por ejemplo "01 MATERIAL", "02 ALCANCE", "03 FORMATO").
- Given estoy en el paso Material, when reviso las 4 tarjetas de "¿De dónde salen las preguntas?" (Material nuevo, Exámenes anteriores, Lo que falle, Examen compartido), when paso el mouse por encima de cualquiera, then hace un zoom leve y sutil (aprox. 1,02x) con su borde de acento encendido en violeta, y al hacer click un pequeño "hundimiento" (la tarjeta se achica levemente un instante, dando sensación de haber sido presionada) antes de quedar seleccionada.
- Given estas mismas tarjetas y las de selección de documentos, when reviso su fondo, then tienen un poco más de textura/profundidad que la versión plana actual (un degradado sutil o una viñeta suave), sin que se sienta recargado ni afecte la legibilidad del texto.
- Given este rediseño toca los 3 pasos del asistente, when reviso el contenido de cada uno, then ninguna opción, campo, texto ni comportamiento cambia respecto a lo que ya existe hoy (Material, Alcance y Formato mantienen exactamente las mismas preguntas, botones y validaciones): es exclusivamente un pulido visual y de animación.
- Given hay un mockup de referencia (`Nuevo examen - rediseño.pdf`, generado en Claude Design) adjunto a este pedido, when se implementa esta historia, then el resultado final respeta ese mockup en distribución, colores, espaciado y las animaciones descriptas arriba, dentro de la paleta y componentes ya definidos en US-027/US-028/US-029.

### US-044
- Given estoy en el Historial, when veo la pantalla, then el layout pasa a dos columnas: a la izquierda el buscador y la lista de exámenes rendidos, a la derecha una tarjeta de resumen con un anillo/gráfico circular de progreso mostrando el promedio, y debajo el porcentaje de aciertos, la mejor nota y el total de exámenes; la sección "Mi evolución por materia" (US-033) queda debajo de esa tarjeta de resumen, colapsable.
- Given reviso un ítem de la lista de exámenes, when veo sus datos (correctas, incorrectas, salteadas), then cada uno se muestra como una pequeña pastilla de color (por ejemplo verde para correctas, rojo/rosado para incorrectas, gris para salteadas), en vez de texto plano, para que se identifiquen de un vistazo sin agregar información nueva.
- Given paso el mouse por encima de un examen de la lista, when hago hover, then el contenido de esa tarjeta se desplaza levemente hacia la izquierda (un movimiento chico y suave), sin que se corte ni se superponga con nada.
- Given paso el mouse por encima de cualquier botón de esta pantalla (por ejemplo "Armar repaso", "Borrar todo historial", los íconos de copiar o borrar de cada examen), when hago hover, then aparece un coloreado suave de fondo sobre el botón, del color que corresponda a esa acción, con una transición gradual (no un cambio brusco).
- Given toco el encabezado de "Mi evolución por materia" para desplegarla o colapsarla, when cambia de estado, then la animación de apertura/cierre es suave (con una transición de altura/opacidad), no un salto instantáneo.
- Given reviso el fondo de la pantalla, when lo comparo con la versión plana actual, then tiene algunas zonas con un coloreado muy sutil tipo "luz violeta lejana" (manchas suaves de degradado, no un patrón ni una textura marcada), sin afectar la legibilidad de ningún texto ni tarjeta.
- Given hoy existe una sección separada "Escala UBA y datos guardados" al final del Historial, when se implementa este rediseño, then esa sección se elimina, y el botón "Borrar todo historial" que ya existía queda como el único punto de borrado total, ahora en un color rojizo que deje claro que es una acción destructiva.
- Given toco "Borrar todo historial", when confirmo la acción (RN-6 sigue aplicando: pide confirmación explícita antes de borrar), then se eliminan todos los exámenes del historial y se recalculan las estadísticas del perfil, igual que ya funciona hoy esa acción.
- Given este rediseño toca principalmente la apariencia visual y las animaciones del Historial (salvo la eliminación de "Escala UBA y datos guardados" aclarada arriba), when reviso el resto del comportamiento, then no cambia: el buscador (US-035), el resumen, la lista, "Mi evolución por materia" (US-033) y "Repaso combinado" (US-032) siguen funcionando exactamente igual que hoy.
- Given hay un mockup de referencia (`Historial AutoExam - 1b.pdf`, generado en Claude Design) adjunto a este pedido, when se implementa esta historia, then el resultado final respeta ese mockup en distribución, colores, espaciado y las animaciones descriptas arriba, dentro de la paleta y componentes ya definidos en US-027/US-028/US-029.

### US-045
- Given entro a Biblioteca y selecciono un libro, when veo el panel de detalle, then pasa a ocupar todo el ancho disponible de la pantalla (en vez de quedar como una tarjeta chica con espacio vacío al costado): arriba un encabezado grande con el nombre y color de la Materia, el título del libro, sus datos (páginas, nombre de archivo, módulos, fecha de subida) y los botones "Guardar cambios"/"Quitar libro"; debajo, dos columnas con los campos editables (título, materia) a la izquierda y "De qué trata" + "Módulos y capítulos" a la derecha.
- Given el resumen de "De qué trata" ya fue generado para un libro, when lo reviso, then además del botón para verlo también hay un botón "Volver a generar" por si quiero pedir un resumen nuevo.
- Given reviso "Módulos y capítulos" de un libro, when lo veo expandido, then cada módulo muestra su etiqueta (por ejemplo "M1"), su nombre y el rango de páginas que cubre, y sus capítulos se listan en una grilla de dos columnas en vez de una lista larga vertical.
- Given este rediseño toca la apariencia de Biblioteca, when reviso el comportamiento, then ninguna funcionalidad cambia: subir material, buscar, agrupar por Materia, editar título/materia, generar o regenerar el resumen, ver módulos/capítulos, guardar cambios y quitar un libro siguen funcionando exactamente igual que hoy.
- Given el fondo de esta pantalla, when lo reviso, then tiene el mismo coloreado sutil tipo "luz violeta lejana" agregado en el Historial (US-044), y las tarjetas/botones seleccionables de esta pantalla usan las mismas animaciones de hover y click ya definidas para el resto de la app (zoom leve + borde de acento en tarjetas seleccionables, coloreado suave de fondo en botones al hacer hover) — excepto la pantalla de configuración inicial de la clave de Gemini (US-042), que no se toca y queda tal cual está.
- Given hay un mockup de referencia (`Libros AutoExam.pdf`, generado en Claude Design) adjunto a este pedido, when se implementa esta historia, then el resultado final respeta ese mockup en distribución, colores y espaciado, dentro de la paleta y componentes ya definidos en US-027/US-028/US-029.

### US-046
- Given estoy en Ajustes, when busco información de almacenamiento, then hay una sección "Datos y almacenamiento" que muestra, en términos simples, el espacio que ocupan mis libros, mi historial y los archivos temporales de la app.
- Given reviso esa sección, when toco "Abrir carpeta de datos", then se abre el explorador de archivos de Windows directamente en la carpeta donde AutoExam guarda esos datos.
- Given quiero liberar espacio, when toco "Vaciar caché", then la app pide confirmación explícita (mismo criterio de RN-6) y, al confirmar, borra únicamente archivos temporales regenerables (por ejemplo miniaturas o archivos intermedios de extracción), sin borrar ningún libro, examen del historial ni configuración.
- Given la app no puede calcular el tamaño exacto de algún dato (por ejemplo por un permiso del sistema operativo), when muestro esta sección, then lo informa con claridad en vez de mostrar un número incorrecto o quedar en blanco sin explicación.

### US-047
- Given estoy en Ajustes, when busco cómo empezar de nuevo con mi configuración, then hay un botón "Restaurar valores de fábrica" claramente diferenciado (por ejemplo con color de acción destructiva), separado de "Vaciar caché" (US-046) y de "Borrar todo historial" (ya existente en Historial).
- Given toco "Restaurar valores de fábrica", when confirmo la acción (mismo criterio de confirmación explícita de RN-6), then vuelven a su valor inicial el tema de color (US-049), el zoom (US-048), las notificaciones (US-050) y los colores elegidos por Materia (US-027).
- Given restauro los valores de fábrica, when reviso mis libros, mi historial y mis claves de Gemini cargadas (US-042), then no se pierde ni se borra nada de eso: esta acción solo afecta preferencias de interfaz, nunca contenido ni credenciales.

### US-048
- Given estoy en Ajustes, when busco cómo cambiar el tamaño de la interfaz, then hay un control (por ejemplo botones +/- o un slider) para el zoom general de la app, con el porcentaje actual siempre visible (por ejemplo "100%").
- Given cambio el nivel de zoom, when lo aplico, then todo el contenido de la app (textos, íconos, botones, tarjetas) escala de forma consistente en todas las pantallas, no solo en Ajustes.
- Given cierro y vuelvo a abrir AutoExam, when la app arranca, then recuerda el último nivel de zoom que elegí.
- Given el zoom llega a su valor mínimo o máximo permitido, when sigo intentando reducir o aumentar, then el control se deshabilita o avisa que llegó al límite, en vez de dejar el contenido ilegible o desproporcionado.

### US-049
- Given estoy en Ajustes, when busco personalizar la apariencia, then hay una sección "Tema" con varias paletas de color predefinidas para elegir (por ejemplo violeta -el actual-, y al menos una o dos alternativas más), cada una mostrada con una pequeña vista previa de color.
- Given elijo un tema distinto al actual, when lo aplico, then cambia el color de acento general de la app (botones principales, bordes de foco, elementos destacados) en todas las pantallas, respetando el mismo criterio de contraste accesible que ya usa la paleta por Materia (RN-31).
- Given cada Materia tiene su propio color elegido por el alumno (US-027), when cambio el tema general de la app, then el color de cada Materia no se modifica: son ajustes independientes.
- Given elijo un tema, when cierro y vuelvo a abrir la app, then el tema elegido se mantiene.

### US-050
- Given estoy en Ajustes, when busco controlar los avisos de la app, then hay una sección "Notificaciones" con una opción para activarlas o desactivarlas en conjunto.
- Given las notificaciones están activadas, when termina de generar un examen o de procesar un material que tardó lo suficiente como para haber cambiado de pantalla mientras tanto, then la app muestra un aviso breve dentro de la propia ventana avisando que terminó.
- Given desactivo las notificaciones, when ocurre cualquiera de esos eventos, then la app no muestra ningún aviso, pero el proceso en sí sigue funcionando igual: la desactivación solo afecta el aviso, no la funcionalidad.
- Given reviso mensajes que no son opcionales (por ejemplo errores de conexión o confirmaciones de borrado ya existentes, RN-6), when tengo las notificaciones desactivadas, then esos siguen mostrándose igual: "Notificaciones" solo controla avisos informativos, no errores ni confirmaciones necesarias para completar una acción.

### US-051
- Given estoy en Ajustes, when busco cómo respaldar mis datos, then hay una sección "Copia de seguridad" con dos acciones: "Exportar todo" e "Importar copia de seguridad".
- Given toco "Exportar todo", when elijo dónde guardarlo, then se genera un único archivo con mis libros, mi historial completo (incluyendo el detalle de preguntas de US-025) y mi configuración (ajustes, colores por Materia, claves de Gemini), para poder guardarlo o pasarlo a otra computadora.
- Given tengo un archivo de copia de seguridad generado por AutoExam, when toco "Importar copia de seguridad" y lo selecciono, then la app pide confirmación explícita (mismo criterio de RN-6) antes de reemplazar los datos actuales, dejando claro que la importación sobrescribe lo que ya tenía cargado.
- Given el archivo que intento importar no es una copia de seguridad válida de AutoExam (por ejemplo está corrupto o es de otra app), when la app lo detecta, then muestra un error claro y no modifica ningún dato existente.
- Given genero una copia de seguridad, when reviso qué hace la app con ese archivo, then nunca lo sube a ningún servidor ni nube: se guarda únicamente donde el usuario elija en su propia computadora.

### US-052
- Given estoy en Ajustes, when busco cómo cambiar el modelo de IA, then hay un selector "Modelo de Gemini" con los modelos disponibles, mostrando cuál está activo en este momento.
- Given cambio el modelo elegido, when genero un examen nuevo, then esa generación usa el modelo recién elegido, y el estado de conexión que ya muestra el modelo activo (visible hoy en el menú principal y en el pie de pantalla) refleja el cambio.
- Given el modelo elegido deja de estar disponible del lado de Google, when la app lo detecta, then avisa con claridad en vez de fallar en silencio, y sugiere volver al modelo por defecto.

### US-053
- Given estoy en Ajustes, when busco información de actualizaciones, then hay un botón "Buscar actualizaciones" cerca del número de versión instalada, separado de "Notas de versión" (US-040).
- Given toco ese botón, when la app consulta si hay una versión nueva, then muestra un estado de "buscando" mientras espera, con el mismo criterio de feedback ya definido para "Probar conexión" (RN-16).
- Given hay una versión nueva disponible, when termina la búsqueda, then la app lo informa y ofrece actualizar usando el mismo mecanismo de actualización automática ya existente, sin instaladores ni canales nuevos.
- Given ya tengo la última versión instalada, when busco actualizaciones, then la app lo confirma con un mensaje claro en vez de quedar en un estado ambiguo o sin respuesta.

### US-054
- Given estoy en Ajustes, when busco cómo controlar las animaciones, then hay un toggle "Reducir movimiento" dentro de la sección de apariencia.
- Given activo "Reducir movimiento", when navego por la app, then las animaciones y transiciones existentes (RN-7/11/18/33) se desactivan o se reducen al mínimo indispensable, igual que cuando el sistema operativo ya lo pide.
- Given el sistema operativo tiene su propia preferencia de "reducir movimiento" activada, when reviso este toggle, then lo veo reflejado como ya activado, sin quedar dos configuraciones contradictorias entre sí sin avisar.

### US-055
- Given dejo la ventana de AutoExam en un tamaño y posición particular (o maximizada), when cierro la app, then la próxima vez que la abro aparece igual a como la dejé.
- Given la posición guardada ya no es válida (por ejemplo se desconectó el segundo monitor donde estaba), when la app arranca, then se ubica en una posición visible por defecto en vez de abrir fuera de pantalla o inaccesible.

### US-056
- Given reviso el rediseño de Ajustes, when busco el control de "Preguntas por lote" que ya existe hoy, then lo sigo encontrando en el nuevo layout (dentro de una sección "Avanzado"), sin que haya cambiado su comportamiento actual.
- Given cambio el valor de "Preguntas por lote" y genero un examen, when reviso el resultado, then el comportamiento es exactamente el mismo que ya tiene la app hoy: el valor configurado actúa como piso (mínimo), nunca como tamaño fijo, y la app puede pedir lotes más grandes automáticamente o achicarlos temporalmente si una respuesta viene cortada por límite de tokens.
- Given reviso el resto de los controles nuevos de Ajustes (US-046 a US-055), when los comparo con este ajuste existente, then ninguno lo reemplaza ni cambia su comportamiento: conviven en la misma pantalla.

### US-057
- Given estoy rindiendo un examen, when reviso la pantalla, then el aviso de "podés rendir con el teclado" ya no es un banner grande: aparece como una línea chica y discreta cerca del pie de pantalla, sin ocupar espacio destacado ni tapar contenido.
- Given estoy respondiendo, when reviso el progreso, then "Pregunta X de N", cuántas respondí y cuántas salteé se muestran en una sola línea con una barra de progreso fina debajo, en vez de dos líneas de texto separadas como hoy.
- Given reviso el navegador de números de pregunta (1 a N), when los veo, then son pastillas discretas con borde sutil, y solo la pregunta actual se distingue con color de acento; ya no tienen un borde marcado por defecto en las que todavía no visité.
- Given reviso las opciones de respuesta, when las comparo con el diseño actual, then tienen más espacio entre sí y bordes más suaves; la opción elegida se distingue con un tono violeta apenas insinuado (relleno tenue + borde fino), no un marco grueso y saturado.
- Given termino el examen y veo la corrección, when reviso el resumen de arriba, then el círculo de nota deja de ser un aro sólido en rojo saturado: pasa a ser un anillo fino de progreso (mismo lenguaje visual que el anillo de "Promedio" de Historial, US-044), con colores más templados según la nota.
- Given reviso cada pregunta en la corrección, when la comparo con el diseño actual, then la franja de color a todo lo alto (verde/roja) se reemplaza por un ícono chico junto a una etiqueta de texto ("Correcta"/"Incorrecta") y un fondo apenas teñido del color correspondiente, sin perder claridad sobre cuál está bien y cuál mal.
- Given reviso los controles de tiempo (timer), tamaño de letra (A-/A+) y cerrar examen arriba a la derecha, when los comparo con hoy, then siguen funcionando exactamente igual, solo se reordenan y aligeran visualmente.
- Given reviso el resto del contenido y la funcionalidad de Examen y su corrección, when comparo con lo que existe hoy, then nada cambia salvo lo descripto acá: preguntas, opciones, atajos de teclado (US-036), modo revancha, compartir examen (US-037), "armar otro examen" y toda la lógica de corrección siguen funcionando exactamente igual.
- Given hay un mockup de referencia (`Examen_AutoExam.pdf`, generado en Claude Design) adjunto a este pedido, when se implementa esta historia, then el resultado final respeta ese mockup en distribución, colores, espaciado y jerarquía, dentro de la paleta y componentes ya definidos en US-027/US-028/US-029 y el estándar visual de RN-53.

### US-058
- Given estoy en cualquiera de los 3 pasos del asistente (Material, Alcance o Formato), when reviso el contenido, then cada grupo de opciones relacionadas (por ejemplo "¿De dónde salen las preguntas?", "¿De qué materia?", "Elegí uno o más documentos", "Capítulos", "Páginas", "Eje temático", "¿Cuántas preguntas?", "Tiempo") vive en su propia tarjeta con encabezado, en vez de quedar como texto y controles sueltos directamente sobre el fondo.
- Given reviso el riel de pasos de arriba, when un paso ya está completo, then debajo de su nombre muestra un resumen corto de lo que elegí ahí (por ejemplo "Tp2 Endocrino · 32 pág." bajo "Material", o "Libro completo · 32 pág." bajo "Alcance"), no solo el número de paso.
- Given estoy en cualquiera de los 3 pasos, when miro a la derecha de la pantalla, then hay un panel fijo "Tu examen" que se mantiene visible en Material, Alcance y Formato, mostrando Materia, Material, Alcance y Formato a medida que los voy completando (los que todavía no definí se ven atenuados con una aclaración de en qué paso se definen).
- Given llego al paso Formato con todo ya elegido, when reviso el panel "Tu examen", then el botón para generar el examen está ahí mismo, al final de ese panel, en vez de solo al pie de la columna principal.
- Given reviso el contenido de los 3 pasos, when lo comparo con lo que existe hoy, then ninguna opción, campo, validación ni comportamiento cambia (mismas fuentes en Material, mismos criterios de alcance, mismos formatos y límites de tiempo): es exclusivamente una reorganización visual y de acompañamiento, no un cambio funcional.
- Given hay un mockup de referencia (`NuevoExamen_AutoExam.pdf`, generado en Claude Design, 3 páginas: Material, Alcance y Formato) adjunto a este pedido, when se implementa esta historia, then el resultado final respeta ese mockup en distribución, agrupación en tarjetas, el riel de pasos con resumen y el panel "Tu examen", dentro de la paleta y componentes ya definidos en US-027/US-028/US-029/US-043.

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
- **RN-40** — El repaso inteligente (US-032) reutiliza el mismo mecanismo local de armado que un examen combinado (RN-27): no consume cuota de IA ni depende de conexión, y nunca repite la misma pregunta dos veces dentro de un mismo repaso.
- **RN-41** — Para saber qué preguntas están "falladas" (US-032), la app se apoya en el detalle de preguntas por examen que ya persiste `ExamenRendido` desde US-025 (RN-25); un examen sin ese detalle guardado (de antes de US-025) no participa del repaso inteligente.
- **RN-42** — El gráfico de evolución (US-033) se arma con los datos ya existentes de `ExamenRendido` por Materia; no requiere guardar información nueva más allá de lo que ya persiste el Historial.
- **RN-43** — El modo cronómetro (US-034) es una opción del paso Formato, independiente del origen de las preguntas (material nuevo con IA, examen combinado o repaso inteligente): no es exclusivo de un solo tipo de generación.
- **RN-44** — Los atajos de teclado (US-036) se definen con un mapeo centralizado y documentado (no hardcodeado disperso por vista), para poder ajustarlos a futuro sin buscar en múltiples archivos.
- **RN-45** — El archivo exportado de un examen (US-037) nunca incluye datos personales del usuario que lo generó (historial, notas, progreso): solo el contenido del examen en sí (preguntas, opciones, correctas, justificaciones e imágenes).
- **RN-46** — Los atajos de teclado (US-036) están activos apenas se entra a la pantalla de examen, sin ninguna acción previa del usuario (click, toque de "Entendido", etc.) que los habilite; el banner/referencia de atajos es solo informativo y no es un interruptor de la funcionalidad.
- **RN-47** — Todo texto de la interfaz que arma una cantidad con una palabra pluralizable (módulos, exámenes, materiales, preguntas, etc.) usa una única función/helper centralizado de pluralización en español, para no repetir el bug de "1 modulos" en otros lugares ni tener que corregir cada texto por separado a futuro.
- **RN-48** — La explicación extendida de US-038 se guarda como parte del detalle de esa pregunta (el mismo detalle persistido por US-025), para no volver a gastar cuota pidiéndola de nuevo sobre la misma pregunta.
- **RN-49** — El tiempo total de estudio (US-039) se calcula a partir de `DuracionSegundos` de cada `ExamenRendido` ya existente; no requiere un cronómetro ni un tracking nuevo aparte del que ya se guarda por examen.
- **RN-50** — Las notas de versión (US-040) se mantienen en un archivo propio del repositorio (por ejemplo un `CHANGELOG.md` o un recurso embebido en el proyecto), redactado a mano en lenguaje simple, separado de los mensajes de commit de git (que pueden ser técnicos). Cada nueva versión que se publica agrega ahí su propia entrada antes de subir el release.
- **RN-51** — Las notas de versión viajan empaquetadas dentro del propio build de la app (no se descargan de internet en tiempo de ejecución), para que estén disponibles incluso sin conexión, igual que el resto de la información de Ajustes.
- **RN-52** — El cambio de formato de almacenamiento de claves de Gemini (US-042, de texto separado por comas a una entrada por pestaña) incluye una migración automática y silenciosa la primera vez que corre la nueva versión: lee el valor guardado en el formato viejo, lo separa por coma y crea una pestaña por cada clave encontrada, conservando el orden original (relevante para el fallback de cuota, RN-3). No debe pedirle nada al usuario ni perder ninguna clave ya cargada.
- **RN-53** — El fondo con coloreado sutil tipo "luz violeta lejana" (introducido en US-044) y las animaciones de hover/click de tarjetas y botones (zoom leve + borde de acento, coloreado suave de fondo) son, a partir de US-045, el estándar visual de toda la app: cualquier pantalla nueva o rediseñada de acá en adelante los usa por defecto, salvo que una historia puntual indique explícitamente lo contrario. La única excepción ya definida es la pantalla de configuración inicial de la clave de Gemini (US-042), que mantiene su propio diseño sin este fondo.
- **RN-54** — "Datos y almacenamiento" y "Vaciar caché" (US-046) solo tocan archivos temporales/regenerables; nunca borran libros, historial ni configuración (esos borrados ya tienen sus propios flujos con confirmación: US-012, "Borrar todo historial", US-047).
- **RN-55** — "Restaurar valores de fábrica" (US-047) resetea únicamente preferencias de interfaz (tema US-049, zoom US-048, notificaciones US-050, colores por Materia US-027); nunca borra libros, documentos, historial de exámenes rendidos ni las claves de Gemini cargadas (US-042).
- **RN-56** — El zoom de US-048 usa el mismo mecanismo centralizado de escalado/estilos ya definido por RN-32, para no duplicar lógica de tamaño entre pantallas.
- **RN-57** — Los temas de color de US-049 se limitan a paletas predefinidas y accesibles (mismo criterio de contraste que RN-31), no a un selector RGB libre; el color por Materia (US-027/RN-30) sigue siendo independiente y no se ve afectado por el tema general elegido.
- **RN-58** — Las notificaciones de US-050 son únicamente avisos informativos no bloqueantes dentro de la propia ventana de la app; nunca reemplazan un mensaje de error ni una confirmación de una acción destructiva (RN-6 sigue aplicando siempre, sin importar esta configuración).
- **RN-59** — La copia de seguridad de US-051 se genera y se restaura siempre en forma local: no se sube a ningún servidor propio ni de terceros, y el usuario es responsable de dónde guarda el archivo exportado (mismo criterio de "sin sincronización en la nube" ya definido para el resto de la app).
- **RN-60** — El listado de modelos de Gemini de US-052 es una lista acotada y mantenida por la app (no un campo de texto libre), para no permitir nombres de modelo inválidos que rompan la generación.
- **RN-61** — "Buscar actualizaciones" (US-053) reutiliza el mismo mecanismo de actualización automática ya existente (RN-51, `publish.yml`/`update.xml`): no agrega un canal ni un instalador propio, solo adelanta manualmente el mismo chequeo que ya corre solo.
- **RN-62** — El toggle "Reducir movimiento" de la app (US-054) y la preferencia de reducir movimiento del sistema operativo se combinan con OR: las animaciones se reducen si cualquiera de las dos está activada.
- **RN-63** — El tamaño y la posición de ventana de US-055 se guardan localmente junto con el resto de las preferencias de interfaz (RN-55) y se resetean junto con "Restaurar valores de fábrica" (US-047); no requiere ningún control visible propio, es un comportamiento automático.
- **RN-64** — El control de "Preguntas por lote" (US-056) no cambia de comportamiento con este rediseño: sigue la misma lógica ya implementada en `GeminiApiService.CalcularPreguntasPorLote` (valor por defecto 12, tope interno 15, piso 3; es un mínimo que la app puede superar automáticamente y solo reduce temporalmente ante una respuesta truncada por límite de tokens).
- **RN-65** — El rediseño de Examen (US-057) es exclusivamente visual (colores, espaciado, jerarquía, agrupación de elementos): no cambia preguntas, opciones, atajos de teclado (RN-44), el mecanismo de corrección, el modo revancha, "compartir examen" (US-037) ni ninguna otra lógica ya definida.
- **RN-66** — La pantalla de Examen (en curso y corrección) también usa el fondo con luz violeta tenue y las animaciones ya estandarizadas por RN-53, pero atenuadas respecto al resto de la app: como es la pantalla donde el alumno necesita concentrarse, el glow de fondo y cualquier animación acá priorizan no distraer por sobre calzar exactamente con la intensidad usada en Historial/Biblioteca/Ajustes.
- **RN-67** — El panel "Tu examen" de US-058 es de solo lectura y acompañamiento: no agrega una forma nueva de editar Materia/Material/Alcance/Formato aparte de los controles que ya existen en cada paso, y no introduce validaciones nuevas más allá de las que el asistente ya tiene hoy.
- **RN-68** — El resumen por paso que aparece en el riel (US-058) se arma a partir de las mismas selecciones que ya guarda el asistente (material elegido, alcance definido, formato elegido); no es un estado separado que se pueda desincronizar de lo que el usuario efectivamente configuró.

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
- Sincronización o backup automático/en la nube de la copia de seguridad (US-051): la copia es siempre manual y local, sin cuenta de usuario ni servidor propio.
- Temas de color totalmente personalizados con selector RGB/HEX libre (US-049): solo paletas predefinidas y accesibles, igual que el color por Materia (RN-31).
- Notificaciones del sistema operativo (bandeja de Windows) fuera de la propia ventana de la app (US-050): son avisos dentro de AutoExam, no notificaciones nativas de Windows.
- Copia de seguridad parcial o selectiva (elegir solo algunos libros o exámenes para exportar): US-051 siempre exporta/importa todo en conjunto.
- Elegir un modelo de Gemini escribiendo su nombre a mano (US-052): siempre se elige de una lista mantenida por la app.
- Un canal de actualización separado (beta/estable) o descargar el instalador manualmente desde Ajustes (US-053): "Buscar actualizaciones" solo adelanta el chequeo del mecanismo automático que ya existe.
- Un control visible para el tamaño/posición de ventana (US-055): es un comportamiento automático, no una opción que el usuario configure a mano.
- Cambiar el mínimo, el tope o el piso de "Preguntas por lote" (US-056): esta historia solo reubica visualmente un control que ya existe, no toca sus valores ni su lógica.
- Cambiar el orden de los pasos del asistente (Material → Alcance → Formato), agregar o quitar pasos, o permitir saltar directamente a un paso sin completar el anterior (US-058): es una reorganización visual del recorrido existente, no un rediseño del flujo en sí.
- Permitir editar Materia/Material/Alcance/Formato desde el panel "Tu examen" (US-058, ver RN-67): el panel es de solo lectura, la edición sigue haciéndose únicamente desde los controles de cada paso.

## Preguntas abiertas (bloquean)
Ninguna. Las dos que bloqueaban quedaron resueltas:
- US-013: el mensaje literal va en el release distribuido por actualización automática a terceros, sin flag ni configuración para ocultarlo (criterio de aceptación firme en US-013 y RN-5).
- US-008: v1 admite únicamente `.docx` / `.xlsx` / `.pptx`; los formatos legacy `.doc` / `.xls` / `.ppt` quedan fuera de alcance (RN-8 y Fuera de alcance).

## Supuestos
Un material puede tener varias imágenes pero una sola fuente por examen (salvo el set de imágenes); el mensaje de US-013 se muestra solo en el resultado del intento original; se respeta el "reducir movimiento" del sistema operativo; el ítem de interfaz hoy llamado "Libro" se generaliza a "material" o se mantiene el término, a decidir en diseño.

Sugerencia: definir con analista-técnico el contrato del pipeline de extracción multi-formato (texto e imágenes), la conversión de HEIC/HEIF y cómo se arma el alcance/recorte que se envía al servicio de IA cuando la fuente no es un PDF.
