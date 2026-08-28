# 01 — Spec: Confiabilidad de generación con Gemini + housekeeping de repo + rediseño visual (morado, más suave)

## Contexto de negocio

Incrementos 1 y 2 (US-001 a US-008) ya están implementados y firmados (`specs/uat-signoff.md`);
este documento no los reabre. Incremento 3: el usuario reporta que generar un examen de hasta 30
preguntas falla o "se satura" contra la API de Gemini antes de completarse; además quiere dejar el
repositorio sin rastro de que un asistente de IA participó (antes de resubirlo a GitHub con su
autoría exclusiva) y renovar la identidad visual del frontend hacia una paleta más suave y variada
con el morado como color predominante. Son tres frentes independientes entre sí, agrupados en este
incremento por venir en el mismo pedido.

## Historias de usuario

**US-009 — Generar un examen de hasta 30 preguntas sin que falle por saturación**
Como usuario que arma un examen para estudiar, quiero poder pedir un examen de hasta 30 preguntas
y que se genere completo de forma confiable, para no toparme con que el proceso se corta o falla
antes de entregarme las preguntas pedidas.

**US-010 — Repositorio sin rastro de asistencia de IA antes de resubir a GitHub**
Como único autor del proyecto, quiero que el repositorio que voy a resubir a GitHub no contenga
archivos, configuración o metadata que dejen constancia de que un asistente de IA participó en su
desarrollo, para que quede publicado como trabajo de mi autoría exclusiva.

**US-011 — Paleta visual más suave y variada, con el morado como color predominante**
Como usuario de AutoExam, quiero que la interfaz se vea con colores más suaves (menos contraste
duro) y una paleta más variada donde predomine el morado, en toda la aplicación y en ambos modos
(claro/oscuro), para tener una experiencia visual más agradable que la actual.

## Criterios de aceptación

**US-009**
- Given que pido un examen de una cantidad de preguntas entre 1 y 30, When el sistema genera el
  examen, Then recibo exactamente la cantidad de preguntas pedida, sin que el proceso termine en
  error por saturación/límite de la API antes de completarlo.
- Given que pido un examen de 30 preguntas, When el proceso está en curso, Then puedo ver que
  avanza (no queda "colgado" ni sin ningún indicio de progreso) aunque tarde más que un examen
  chico.
- Given que la API de Gemini responde con un límite o error transitorio durante la generación
  (cuota del minuto, respuesta cortada, etc.), When eso ocurre, Then el sistema reintenta o ajusta
  el pedido automáticamente en vez de fallar de inmediato, y solo termina en error si, agotados los
  reintentos razonables, de verdad no puede completar el examen.
- Given que, agotados los reintentos razonables, el sistema no logra completar la cantidad pedida,
  When falla, Then el mensaje que ve el usuario explica en términos entendibles por qué no se pudo
  (no un error técnico críptico) y qué puede probar (cambiar de modelo, achicar el lote, etc.).
- Given que ya tengo un examen de 30 preguntas generado con éxito una vez arreglado esto, When lo
  repito en las mismas condiciones (mismo material, misma clave), Then vuelve a completarse sin
  fallar por el mismo motivo — no es una mejora de una sola vez, es un comportamiento repetible.

**US-010**
- Given el estado final del repositorio antes de resubir a GitHub, When se revisa el contenido
  versionado, Then no queda ningún archivo de configuración, carpeta o metadata que identifique a
  un asistente de IA como participante (por ejemplo carpetas de configuración de herramientas de
  IA que hoy están sin trackear, como `.claude/`), salvo que sea necesario para el funcionamiento
  o la trazabilidad histórica del proyecto (ver regla de negocio de "no vital" abajo).
- Given el código fuente y los archivos de configuración que sí quedan en el repo, When se
  inspeccionan comentarios y metadata editable, Then no contienen menciones a Claude ni a que un
  asistente de IA generó o participó en ese archivo.
- Given el historial de commits ya existente en la rama, When se resube el repositorio, Then ese
  historial no se reescribe como parte de esta limpieza (ver "Fuera de alcance") — la limpieza
  aplica al estado actual de archivos hacia adelante, no a commits pasados.
- Given que se identifica un archivo con rastro de IA que sí es necesario para que la app compile,
  corra o se publique, When se hace la limpieza, Then ese archivo no se borra ni se vacía — se le
  quita únicamente la mención a la IA si la tiene, sin romper su función.

**US-011**
- Given el tema claro y el tema oscuro de la aplicación, When se aplica la nueva paleta, Then en
  ambos modos el morado es el color predominante de la identidad visual (acentos, elementos de
  marca, estados activos/seleccionados), y los colores en general se perciben más suaves que la
  paleta actual (menos contraste duro entre fondo y elementos).
- Given cualquier pantalla de la aplicación (Libros, Nuevo examen, Examen, Historial, Ajustes,
  diálogos), When se navega por ella con la paleta nueva, Then se ve consistente con el resto de
  la app — no hay pantallas que quedaron con la paleta vieja y otras con la nueva.
- Given la paleta nueva, When se usa la app para tareas normales (leer una pregunta, distinguir una
  opción correcta de una incorrecta, leer un estado de error), Then el contraste sigue siendo
  suficiente para leer cómodo — "más suave" no significa perder legibilidad.
- Given que la app ya distingue estados con color (ej. correcto/incorrecto en la corrección de un
  examen, error/aviso en diálogos), When se aplica la paleta nueva, Then esos estados se siguen
  distinguiendo entre sí con claridad, aunque ahora convivan con más variedad de color en el resto
  de la interfaz.

## Reglas de negocio

- "Hasta 30 preguntas" es el techo de este requerimiento: no se exige que exámenes de más de 30
  preguntas funcionen sin fallas (aunque tampoco se prohíbe intentarlo); el compromiso de US-009
  es sobre el rango 1-30.
- Para US-010, se considera "no vital" (limpiable) todo archivo o metadata que: (a) no es
  necesario para que la app compile, corra, se publique o se pueda operar/mantener, y (b) su único
  propósito es dejar constancia de participación de una herramienta de IA (configuración de la
  herramienta, comentarios que la mencionan, metadata de sesión). Se considera "vital" (no tocar)
  todo lo que el proyecto necesita para funcionar aunque haya sido generado con ayuda de IA:
  código de la aplicación, tests, workflows de CI/CD, scripts de publicación, specs técnicas ya
  congeladas — a esos solo se les quita la mención a la IA si la tuvieran, nunca se borran.
- El historial de commits ya empujado a `main` no se reescribe ni se fuerza-pushea como parte de
  esta limpieza; "autoría exclusiva del usuario" en US-010 aplica al estado del código y archivos
  al momento de resubir, no a una reescritura retroactiva del historial (ver "Fuera de alcance").
- El morado predominante de US-011 se aplica sobre el mecanismo de tematizado ya existente de la
  app (tokens de color intercambiables entre modo claro y oscuro); no se introduce una paleta que
  solo funcione en uno de los dos modos.
- Todo código nuevo que se escriba para resolver US-009, US-010 y US-011 sigue un lineamiento
  transversal de estilo: simple y legible, sin abstracciones, capas ni patrones que no sean
  imprescindibles para resolver el problema — el criterio de referencia es "como lo escribiría un
  programador trainee". No es una historia de usuario en sí misma (no tiene criterio de aceptación
  funcional propio) sino una restricción de calidad que aplica a las tres de arriba por igual.

## Fuera de alcance

- Cambiar el stack tecnológico actual (.NET 8, WPF, WPF-UI, integración con la API de Gemini como
  tal, mecanismo de actualización automática) — este incremento no reabre esas decisiones.
- Exámenes de más de 30 preguntas: no forman parte del compromiso de confiabilidad de US-009.
- Cambiar el motor de generación de exámenes por otro proveedor de IA distinto a Gemini.
- Reescribir o purgar el historial de commits ya publicado (`git filter-branch`, `rebase` masivo,
  force-push reescribiendo commits pasados): US-010 limpia el estado actual de archivos, no el
  historial. Si el usuario quisiera además un historial limpio de raíz, es un pedido aparte y más
  invasivo (reescribe hashes, rompe cualquier clon existente) que no fue pedido explícitamente acá.
- Borrar o vaciar archivos vitales para el funcionamiento del proyecto en nombre de "sacar rastro
  de IA" — ver regla de negocio de "vital vs. no vital".
- Redefinir la navegación, estructura de pantallas o funcionalidad de la aplicación: US-011 es
  estrictamente paleta de color (más suave, morado predominante), no un rediseño de layout,
  iconografía o tipografía.
- Migrar el mecanismo de tematizado a uno distinto del ya existente (tokens intercambiables
  claro/oscuro): US-011 cambia los valores de color dentro de ese mecanismo, no lo reemplaza.
- Animaciones o interacciones nuevas de interfaz: ya cubiertas en el incremento anterior
  (US-007/US-008), no forman parte de este pedido.

## Supuestos

- "Se satura antes de generar un examen de 30 preguntas" se interpreta como: el proceso de
  generación no logra completar de forma confiable la cantidad pedida dentro del rango 1-30,
  ya sea por error explícito o por entregar un examen incompleto sin avisar con claridad por qué.
  Si el síntoma real fuera otro (por ejemplo, un tope duro distinto en la configuración que impide
  siquiera pedir 30), el analista/arquitecto técnico lo ajusta al relevar el estado real del
  código, sin que eso cambie la intención de negocio de este US.
- "Sacar rastro de Claude en los archivos" se interpreta como limpieza del estado actual del
  repositorio (working tree a resubir), no como reescritura del historial de Git ya publicado —
  ver regla de negocio y "Fuera de alcance". Si el usuario quisiera además el historial reescrito,
  es una pregunta que sí bloquearía y debería confirmarse antes de ejecutar (acción irreversible
  para cualquiera que ya haya clonado el repo).
- "Como si lo programara un programador trainee" se toma como lineamiento de estilo de código
  (simplicidad, legibilidad, sin sobre-ingeniería), no como una degradación deliberada de la
  calidad funcional o de la robustez que pide US-009 — confiable y simple no son contradictorios
  acá: se prioriza que ande bien con el mecanismo más simple posible, no el mecanismo más simple
  a costa de que ande peor.
- "Más variedad de colores, predominando el morado" se interpreta como una paleta con distintos
  tonos/matices de morado (y algún color de apoyo) donde el morado es el que más se percibe, no
  como una app monocromática ni como agregar colores sin relación entre sí.

## Preguntas abiertas

Ninguna bloquea el arranque del trabajo técnico. Un solo punto queda señalado, no bloqueante: si
al analizar el código el equipo técnico encuentra que el síntoma de US-009 tiene un componente que
excede lo que la app controla (por ejemplo, un límite de cuota de la cuenta/clave de Gemini del
usuario, no de la app), eso se documenta como limitación externa en la spec técnica en vez de
tratarse como bug de la app — no bloquea este spec, pero sí puede acotar qué tan "confiable" puede
llegar a ser el resultado.
