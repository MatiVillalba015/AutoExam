# Notas de versión de AutoExam

Qué cambió en cada actualización, contado en criollo. Esto no son los mensajes de commit: es
lo que una persona que usa la app necesita saber para entender qué hay de nuevo.

**Cómo agregar una versión nueva:** copiá el bloque de abajo, cambiá el número y la fecha, y
escribí los puntos en presente y en segunda persona ("ya podés...", "ahora la app..."). Las
tres secciones —Nuevo, Cambios, Arreglos— son opcionales: poné solo las que correspondan. El
formato importa porque la app parsea este archivo para mostrarlo en Ajustes → Notas de versión.

<!--
## X.Y.Z — D de mes de AAAA

### Nuevo
- ...

### Cambios
- ...

### Arreglos
- ...
-->

## 1.3.0 — 5 de septiembre de 2026

### Nuevo
- Las claves de Gemini ahora se cargan en pestañas, una por clave, con un botón "+ clave 2"
  para sumar otra. Cada pestaña tiene su propio campo y su propio botón de mostrar u ocultar,
  así que agregar o borrar una no toca las demás. Si ya tenías varias claves cargadas separadas
  por comas, se acomodan solas en pestañas la primera vez que abrís esta versión, en el mismo
  orden y sin perder ninguna.

- Ajustes se rehizo entero y ahora junta todo lo que se puede configurar de la app.
- Podés elegir el tema de color de AutoExam entre violeta, azul, verde y claro. Es aparte del
  color de cada Materia, que no se toca.
- Podés agrandar o achicar toda la interfaz desde Ajustes, con el porcentaje siempre a la
  vista. El tamaño que elijas queda para la próxima vez que abras la app.
- "Datos y almacenamiento" te muestra cuánto ocupan tus libros, tu historial y los archivos
  temporales, con un botón para abrir la carpeta y otro para vaciar la caché. Vaciar la caché
  solo borra archivos temporales: no toca ningún libro, examen ni configuración.
- "Copia de seguridad": exportás tus libros, tu historial y tu configuración a un solo archivo
  y los volvés a importar cuando quieras, por ejemplo para pasarlos a otra computadora. La
  copia queda donde vos la guardes; nunca se sube a ningún lado.
- "Restaurar valores de fábrica" devuelve el tema, el tamaño, las notificaciones y los colores
  de tus Materias a como venían. Tus libros, tu historial y tus claves de Gemini no se tocan, y
  pide confirmación antes.
- Podés apagar los avisos informativos de la app (por ejemplo el que aparece cuando termina de
  generar un examen). Los errores y las confirmaciones se siguen mostrando siempre.
- "Reducir movimiento" ahora también se puede activar desde AutoExam, sin tener que cambiar la
  configuración de todo Windows. Si Windows ya lo pide, las animaciones quedan apagadas igual.

### Cambios
- El modelo de Gemini se elige de una lista en lugar de escribirlo a mano, así no se puede
  guardar un nombre inválido que recién falle al generar un examen. "Detectar" sigue estando
  para traer los modelos que tu clave habilita.
- "Buscar actualizaciones" ahora muestra que está buscando y contesta ahí mismo en Ajustes, en
  lugar de abrir un cuadro de diálogo.
- La pantalla que aparece la primera vez que abrís la app se rediseñó: el nombre y la
  descripción arriba a la izquierda, la clave abajo, y a la derecha una tarjeta con el botón
  para verificar, cómo va la prueba de conexión y el link para conseguir una clave gratis.
- Las claves se cargan igual en esa pantalla y en Ajustes, con las mismas pestañas.
- Los tres pasos de Nuevo examen se ven más prolijos: los pasos que ya completaste muestran un
  tilde en lugar del número, y arriba del paso dice cuál estás haciendo ("01 MATERIAL").
- Las cuatro tarjetas de "¿De dónde salen las preguntas?" crecen apenas y se les enciende un
  borde violeta al pasar el mouse, y se hunden un instante al tocarlas. Las tarjetas que se
  eligen (esas y la lista de documentos) tienen un fondo con un poco más de relieve.
- El Historial se reacomodó en dos columnas: el buscador y tus exámenes a la izquierda, y a la
  derecha un resumen con un anillo que muestra tu promedio, más los aciertos, la mejor nota y el
  total. "Mi evolución por materia" quedó abajo de ese resumen y ahora se abre y se cierra con
  una animación suave.
- En cada examen del Historial, las correctas, las incorrectas y las salteadas se ven como
  pastillas de color en lugar de una línea de texto. Al pasar el mouse por un examen su
  contenido se corre apenas, y los botones se tiñen despacio del color de lo que hacen.

- La ficha de un libro en Libros ahora usa toda la pantalla: arriba el nombre del libro con su
  materia y sus datos, y debajo lo que se edita a la izquierda y lo que se lee a la derecha. Los
  módulos ya vienen abiertos y se ven en dos columnas, cada uno con su etiqueta (M1, M2...) y
  sus páginas.
- En "De qué trata" hay un botón "Volver a generar" para pedir un resumen nuevo cuando ya
  tenés uno guardado.
- El fondo con la luz violeta tenue y las animaciones al pasar el mouse pasaron a ser lo mismo
  en toda la app. La pantalla donde cargás la clave de Gemini queda con su diseño propio.

- La pantalla de rendir un examen se ve más calma. El aviso de que se puede usar el teclado
  dejó de ser un cartel arriba de la pregunta y pasó a una línea chica al pie. En qué pregunta
  vas, cuántas respondiste y cuántas salteaste ahora se leen en un solo renglón, con una barra
  fina debajo. Los números de pregunta son pastillas discretas y solo la que estás mirando se
  pinta. Las opciones tienen más aire, y la que elegís se marca con un violeta apenas
  insinuado en lugar de un recuadro grueso.
- Al corregir, la nota se muestra en un anillo fino como el del promedio del Historial, en vez
  de un círculo rojo lleno. Cada pregunta dice "Correcta" o "Incorrecta" con un ícono al lado,
  y el color queda como un tinte suave de la tarjeta en lugar de una franja de punta a punta.
- El reloj, los botones de tamaño de letra y el de cerrar el examen quedaron alineados en una
  sola fila arriba a la derecha. Hacen exactamente lo mismo que antes.

- Nuevo examen se rehizo entero. Cada grupo de opciones ("¿De dónde salen las preguntas?",
  "¿De qué materia?", "Elegí uno o más documentos", "Capítulos", "Páginas", "Eje temático",
  "¿Cuántas preguntas?" y "Tiempo") ahora vive en su propia tarjeta con título, en vez de
  quedar suelto sobre el fondo.
- Debajo del nombre de cada paso ahora dice lo que elegiste ahí ("Tp2 Endocrino · 32 pág.",
  "libro completo", "10 preguntas · con gráficos"), así que se ve de un vistazo cómo va
  quedando el examen sin tener que volver atrás.
- A la derecha hay un panel "Tu examen" que te acompaña en los tres pasos y va mostrando la
  materia, el material, el alcance y el formato a medida que los elegís. Lo que todavía no
  definiste se ve atenuado y te dice en qué paso se define. El panel no se edita: para cambiar
  algo se sigue usando el paso que corresponde.
- "Generar examen" pasó a estar al final de ese panel, cuando llegás al paso Formato. Abajo
  quedaron solo "Atrás" y "Continuar".
- Nada de lo que elegís cambió: las mismas fuentes, los mismos capítulos y páginas, los mismos
  formatos y límites de tiempo. Lo único distinto es cómo está agrupado y acompañado.

### Arreglos
- La ayuda de atajos de teclado del examen mostraba las teclas en blanco: se veían cuatro
  pastillas violetas vacías en vez de "1-4", "Enter" y las demás. Ahora dice qué hace cada una.
- El anillo del resumen del Historial estaba mal dibujado: el arco violeta se metía adentro del
  círculo y se superponía con el número, y además marcaba una porción que no tenía nada que ver
  con tu porcentaje de aciertos. Ahora es una banda fina sobre el borde y cubre exactamente el
  porcentaje que dice el texto.
- En el paso Alcance, con un PDF aparecía igual la aclaración "este material se toma completo",
  que es para Word, Excel e imágenes. Ya no.
- Y al revés: cuando un libro no tiene capítulos cargados, el aviso que lo explica y ofrece
  detectarlos desde el índice del PDF no aparecía nunca. La tarjeta "Capítulos" se veía vacía
  sin decir por qué.
- En la corrección, el rótulo "Salteada / Pendiente" estaba escrito en un marrón tan oscuro que
  no se leía sobre la tarjeta.
- Se sacó la sección "Escala UBA y datos guardados" del final del Historial: "Borrar todo
  historial" ahora está a la vista y en rojo, para que se note que borra todo. Sigue pidiendo
  confirmación antes de hacerlo.
- El menú de inicio se ve distinto: el ícono de cada una de las cuatro tarjetas ahora va en un
  cuadrado violeta, con el título y la descripción al lado. Las cuatro siguen en el mismo lugar
  y hacen exactamente lo mismo que antes.
- "Generar examen" se destaca del resto con un fondo más violeta, porque es lo que más se usa.
- Al pasar el mouse por una tarjeta del menú o por un examen de la lista, el elemento crece
  apenas y se le enciende un borde violeta alrededor. Al sacar el mouse vuelve solo, sin saltos.
  Si tenés activado "reducir movimiento" en Windows, se resalta igual pero sin crecer.
- Los últimos exámenes pasaron a estar al costado de las tarjetas en vez de abajo, cada uno con
  una barra del color de su materia y su nota a la derecha.
- Arriba del menú ahora dice tu promedio ("7,5 de promedio en 4 exámenes") en lugar de la cuenta
  de materiales.

## 1.2.0 — 4 de septiembre de 2026

### Nuevo
- Podés superponer varias materias en el gráfico de evolución del Historial, cada una con su
  propio color, para comparar tu progreso entre materias de un vistazo.
- Botón "Explicame mejor" al corregir una pregunta: le pedís a Gemini una explicación más larga
  de por qué la respuesta correcta es esa, y queda guardada para la próxima vez que la mires.
- Tiempo total de estudio acumulado en el Historial (en total y de esta semana), sumando la
  duración de todos tus exámenes.
- Esta pantalla de notas de versión, para ver qué cambió en cada actualización.

### Cambios
- Los atajos de teclado del examen ya están activos apenas entrás, sin tener que tocar nada
  antes para habilitarlos.

### Arreglos
- "1 modulos" ahora dice "1 módulo" (y cualquier otro texto con una cantidad usa la forma
  correcta en singular o plural).
- Los círculos que muestran los atajos de teclado ahora tienen el número o la letra adentro.
- El campo de minutos personalizados del cronómetro ya no aparece si elegís "Sin límite".
- La tarjeta de Materias en Biblioteca ahora muestra el color de cada una, igual que el resto
  de la app.

## 1.1.0 — 4 de septiembre de 2026

### Nuevo
- Ahora podés organizar tu material por materia y darle un color a cada una. Ese color aparece
  en el historial, en la biblioteca y en el examen, así reconocés de qué materia es cada cosa
  sin leer el título.
- Podés armar un examen combinando varios que ya rendiste, sin gastar cuota de Gemini y sin
  esperar: las preguntas ya estaban generadas.
- Repaso de lo que fallaste: la app junta las preguntas que erraste o salteaste y todavía no
  volviste a acertar, y arma un examen solo con esas. Si acertás una, deja de aparecer.
- Un compañero puede pasarte un examen suyo en un archivo y lo rendís en tu AutoExam sin gastar
  tu propia cuota. También podés exportar los tuyos. El archivo lleva las preguntas y nada de
  tus notas ni de tus respuestas.
- Gráfico de tu evolución por materia en el Historial: cómo te fue en cada intento a lo largo
  del tiempo, con la línea del 4 marcada para ver de un vistazo qué aprobaste.
- Modo cronómetro: le ponés un tiempo total al examen (20, 40, 60 minutos o el que quieras) y
  al acabarse se entrega solo con lo que hayas respondido. Los últimos dos minutos el reloj se
  pinta de rojo.
- Buscador en Biblioteca y en Historial, que filtra mientras escribís.
- Podés rendir con el teclado: 1 a 4 o A a D para elegir una opción, flechas para moverte entre
  preguntas y S para saltear. La primera vez que entrás a un examen te lo recuerda.
- Entrás a cualquier examen del historial y lo revisás pregunta por pregunta, con el análisis de
  cada opción, meses después de haberlo rendido.
- Un botón "¿Qué es AutoExam?" en el menú principal, con una explicación corta para quien abre
  la app por primera vez.

### Cambios
- El menú principal ahora es la pantalla con la que arranca la app, con las cuatro acciones más
  usadas —generar examen, subir material, ver exámenes anteriores y ajustes— y un resumen de
  los últimos exámenes que rendiste.
- La barra lateral se reemplazó por esa pantalla de inicio. Cada sección pasó a usar todo el
  ancho de la ventana, y los atajos Ctrl+1 a Ctrl+5 siguen llevando directo a cada una.
- Tipografía y colores nuevos en toda la app, con la letra del examen un poco más chica para
  que entre más contenido sin sentirse apretado.
- En la pantalla de examen la pregunta quedó separada de las opciones en su propia tarjeta, la
  barra de progreso ya no se va al hacer scroll, y la opción que elegís se marca con una barra
  de color que se ve de lejos.
- El historial pasó a tarjetas y la biblioteca quedó agrupada por materia, con grupos que se
  pueden plegar.
- Los botones tienen un zoom suave al pasar el mouse y muestran una descripción abajo de qué
  hacen.
- Cuando el contenido no llena la ventana, ahora queda centrado en vez de pegado arriba.

### Arreglos
- Las tarjetas del menú se quedaban en blanco al pasarles el mouse por encima. Pasaba lo mismo
  con las opciones del examen y con las fichas de la biblioteca.
- Las imágenes de los exámenes del historial se borraban a los siete días, así que al revisar un
  examen viejo la figura de la pregunta ya no estaba.
- La barra que muestra en qué sección estás se quedaba con el nombre de la sección anterior.
