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
