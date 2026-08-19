# AutoExam · Generador de exámenes estilo UBA

Aplicación de escritorio WPF (.NET 8) que carga PDFs extensos de libros (1.000+ páginas),
genera exámenes multiple choice con la API de Google Gemini, los corrige localmente con la
escala de calificaciones de la UBA y permite reintentar en bucle las preguntas incorrectas
y salteadas hasta llegar al 100 % de aciertos.

## Stack

| Capa | Tecnología |
|---|---|
| UI | WPF (.NET 8.0) + [WPF-UI](https://github.com/lepoco/wpfui) 4.3.0 (Fluent / Windows 11) |
| PDF | [PdfPig](https://github.com/UglyToad/PdfPig) 0.1.15 (texto por bloques de páginas, figuras y rescate de páginas escaneadas) |
| IA | Google Gemini `generateContent` vía `HttpClient` |
| Persistencia | JSON en `%LOCALAPPDATA%\AppEstudioUBA` |

## Estructura

La app sigue **MVVM**: los modelos no conocen a WPF, los ViewModels no conocen a los
controles, y las vistas casi no tienen código detrás.

```
AutoExam/
├─ Models/                  Datos puros: ni un Brush ni un using de System.Windows
│  ├─ Enums.cs              EstadoPreguntaEnum, ResultadoPreguntaEnum, ModoAlcance
│  ├─ ObservableBase.cs     INotifyPropertyChanged mínimo
│  ├─ Pregunta.cs           Pregunta + AnalisisOpciones
│  ├─ LineaAnalisis.cs      Fila del desglose opción por opción
│  ├─ Libro.cs              Libro + Modulo
│  ├─ ExamenRendido.cs      ExamenRendido + RondaRevancha
│  ├─ PerfilUsuario.cs      PerfilUsuario + AppConfig
│  ├─ ExamenEnCurso.cs      Estado en memoria del intento
│  └─ NavegadorItem.cs      Baldosa del navegador de preguntas
├─ ViewModels/              Toda la lógica de pantalla, testeable sin abrir ventanas
│  ├─ ShellViewModel.cs     Raíz: crea las páginas y las conecta por eventos
│  ├─ PaginaViewModel.cs    Base de página + interfaz INavegacion
│  ├─ OnboardingViewModel.cs   Verificación de la clave antes de entrar
│  ├─ BibliotecaViewModel.cs   Alta de libros y módulos
│  ├─ AsistenteViewModel.cs    Asistente de 3 pasos
│  ├─ ExamenViewModel.cs       Rendir, corregir y revancha
│  ├─ HistorialViewModel.cs    Estadísticas
│  └─ AjustesViewModel.cs      Clave, modelo y opciones avanzadas
├─ Views/                   Un UserControl por página, sólo enlaces
├─ Theme/
│  ├─ Tokens.Oscuro.xaml    Paleta oscura (pinceles + sombra)
│  ├─ Tokens.Claro.xaml     Paleta clara, mismas claves
│  └─ Estilos.xaml          Tipografía, tarjetas, chips, navegación
├─ Behaviors/
│  └─ SoltarArchivo.cs      Drag and drop de archivos hacia un comando
├─ Services/
│  ├─ RutasApp.cs           Rutas de datos + log de errores
│  ├─ JsonStore.cs          Load/Save atómico y tolerante a corrupción
│  ├─ PdfExtractorService.cs   Extracción por lotes de páginas + rescate de escaneos
│  ├─ ImagenUtil.cs         Reescalado, compresión para lectura y carga sin bloquear archivos
│  ├─ GeminiApiService.cs   Prompts, lotes, multimodal, parseo tolerante
│  ├─ BibliotecaService.cs  libros.json + copia interna de los PDF
│  ├─ SesionUsuarioService.cs  config.json + perfil.json
│  ├─ EvaluadorUBA.cs       Corrección local y escala 1–10
│  ├─ TemaService.cs        Intercambio de paleta claro/oscuro
│  └─ DialogoService.cs     IDialogos: diálogos y sistema operativo, mockeable
├─ Converters.cs
├─ App.xaml / App.xaml.cs   Recursos + raíz de composición
└─ MainWindow.xaml          Cascarón: navegación lateral + página activa
```

### Sistema de diseño

Ningún color literal vive en una vista: todo sale de `Theme/Tokens.*.xaml`, y cambiar
de tema es reemplazar ese diccionario. La paleta reserva **verde, rojo y ámbar** para
la corrección (correcta / incorrecta / salteada), así que el acento de marca es violeta:
cualquier otro color competiría con el significado de la nota. El fondo oscuro es tinta
azulada y el texto no llega al blanco, para aguantar lecturas largas.

## Datos locales

```
%LOCALAPPDATA%\AppEstudioUBA\
├─ Biblioteca\<id>.pdf     copias internas de los libros
├─ Imagenes\<examenId>\    figuras extraídas (se limpian a los 7 días)
├─ libros.json
├─ perfil.json
├─ config.json
└─ errores.log
```

Definiendo la variable de entorno `AUTOEXAM_DATOS` la app usa esa carpeta en lugar de
`%LOCALAPPDATA%`: sirve para llevar la biblioteca en un pendrive o para probar una
versión nueva sin tocar los datos de siempre.

## Puesta en marcha

1. Obtené una API Key gratuita en <https://aistudio.google.com/app/apikey>.
2. Al abrir la app aparece primero la **pantalla de inicio**: pegá la clave y tocá
   **Verificar y empezar**. La app consulta qué modelos habilita tu clave, elige el mejor
   *flash* disponible y hace una llamada real de prueba. Recién con eso en verde se abre
   el resto. Si ya guardaste una clave, se verifica sola y entra directo.
3. **Libros** → soltá un PDF sobre la zona punteada (o hacé click para elegirlo). El título
   sale del nombre del archivo y la materia se ofrece como chip de las que ya usaste.
   *Módulos y capítulos* se despliega sólo si querés dividir el libro.
4. **Nuevo examen** → asistente de tres pasos:
   **Material** (qué libro) → **Alcance** (chips de capítulos, presets de páginas, eje
   temático opcional) → **Formato** (10 / 30 / 60 / otra, gráficos sí o no). Bajo cada paso
   se ve lo que ya elegiste, y al final hay un resumen antes de gastar cuota.
5. Rendí el examen. Podés **salir en cualquier momento** con la ✕ del encabezado (el intento
   se descarta), saltear con el botón discreto, o dejar que avance sola al responder.
   La tira de baldosas de arriba es a la vez el navegador y el indicador de avance:
   cada baldosa es una pregunta y se pinta con su estado.
6. En la corrección, **Reintentar pendientes** arranca el Modo Revancha con las opciones
   reordenadas al azar. Repetilo hasta el 100 %.

### Atajos durante el examen

`1`-`4` o `A`-`D` eligen opción · `Enter` o `→` avanza · `←` vuelve · `S` saltea.

## Escala UBA aplicada

| Aciertos | Nota |
|---|---|
| 95 – 100 % | 10 Sobresaliente |
| 88 – 94 % | 9 Distinguido |
| 81 – 87 % | 8 Muy bueno |
| 74 – 80 % | 7 Bueno |
| 68 – 73 % | 6 Bueno |
| 64 – 67 % | 5 Aprobado |
| 60 – 63 % | 4 Aprobado (mínimo) |
| 40 – 59 % | 3 Aplazo |
| 20 – 39 % | 2 Aplazo |
| 0 – 19 % | 1 Aplazo |

Las preguntas **salteadas cuentan como no respondidas**: restan igual que un error.
Las rondas de revancha **no modifican** la nota del intento original; quedan anotadas aparte.

## Compilar y ejecutar en desarrollo

```bash
dotnet run --project AutoExam/AutoExam.csproj
```

## Generar el .exe portátil (single-file, self-contained, win-x64)

Desde la carpeta raíz del proyecto:

```bash
dotnet publish AutoExam/AutoExam.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

El ejecutable queda en:

```
AutoExam\bin\Release\net8.0-windows\win-x64\publish\AutoExam.exe
```

Es un único archivo de ~67 MB que **no requiere instalar .NET** en la PC de destino.
El `AutoExam.pdb` que aparece al lado es solo para depuración: se puede borrar.

> **Nota:** `PublishTrimmed` queda desactivado a propósito — WPF no es compatible con
> IL trimming y recortar el ensamblado rompe la app en tiempo de ejecución.

## Actualizaciones automáticas

Pensado para repartir la app: quien la tenga instalada recibe las mejoras sin volver a
pedirte el `.exe`. Al arrancar, AutoExam consulta
[`update.xml`](update.xml) en la rama `main` del repo y, si hay una versión más nueva,
ofrece descargarla e instalarla sola. Lo maneja **AutoUpdater.NET**.

**Si no hay nada nuevo, no se muestra absolutamente nada** — tampoco si no hay internet o el
manifiesto todavía no existe. Esos casos quedan solo en `errores.log`: quien recibe la app no
tiene por qué ver un error técnico cada vez que la abre sin conexión. La única comprobación
que contesta siempre es la del botón **Buscar actualizaciones** en *Ajustes*, porque ahí el
usuario preguntó.

### Publicar una versión nueva

**El orden importa.** `update.xml` se commitea *último*, cuando el ZIP ya está arriba.

1. Subí `<Version>` en `AutoExam/AutoExam.csproj` (ej. `1.0.1`).
2. Publicá el `.exe` con el comando de la sección anterior. **Después de bumpear la versión,
   no antes** — ver el aviso de abajo.
3. **Comprimí el `.exe` en un ZIP**, con el ejecutable en la raíz del ZIP:
   ```bash
   powershell Compress-Archive -Path publish/AutoExam.exe -DestinationPath AutoExam-v1.0.1.zip
   ```
4. Creá el Release en GitHub con el tag `v1.0.1` y adjuntá el ZIP.
5. Actualizá `<version>`, `<url>` y `<changelog>` en `update.xml` y commiteá a `main`.

> ### ⚠ El binario del ZIP tiene que reportar la versión del manifiesto
>
> AutoUpdater compara el `<version>` del manifiesto contra la versión del **ensamblado**, no
> contra el nombre del archivo ni el tag. Si publicás `AutoExam-v1.0.1.zip` con un `.exe`
> compilado cuando el `.csproj` todavía decía `1.0.0`, pasa esto:
>
> la app ve 1.0.1 → descarga 63 MB → extrae → reinicia → **el binario nuevo sigue reportando
> 1.0.0** → ve 1.0.1 otra vez → vuelve a descargar. En bucle. Y con `<mandatory>true</mandatory>`
> no hay botón para salir, porque *Más tarde* y *Omitir* están ocultos.
>
> Para comprobarlo antes de publicar:
> ```bash
> powershell -Command "[Diagnostics.FileVersionInfo]::GetVersionInfo('publish/AutoExam.exe').FileVersion"
> ```
> Tiene que coincidir con el `<version>` del manifiesto.

Tiene que ser un **ZIP y no el `.exe` suelto**: AutoUpdater trata un `.exe` como instalador,
así que ejecutarlo abriría una segunda copia de AutoExam en vez de reemplazar la que está
corriendo. Con un ZIP descomprime sobre la carpeta, cierra la app y la vuelve a abrir ya
actualizada.

Sobre `<mandatory>`: en `true` se ocultan *Más tarde* y *Omitir*, así que la actualización es
la única salida. Sirve para una versión que arregla algo que rompe el uso normal; para el resto
conviene `false`, que además deja escapatoria si el paquete publicado sale con algún problema.

### Corte del bucle de actualización

Pasó de verdad: la `v1.0.1` se publicó con un `.exe` compilado cuando el `.csproj` todavía
decía `1.0.0`, y con `mandatory=true`. La app se actualizaba, reiniciaba, se veía
desactualizada otra vez y volvía a pedir la misma actualización — sin botón para cerrarla.

La app ya no puede quedar así, aunque lo publicado esté mal. Anota en `config.json` qué versión
intentó instalar y cuántas veces:

- **1.º intento fallido:** se reintenta. Pudo cortarse la descarga o la conexión.
- **2.º intento fallido de la misma versión:** el paquete no trae lo que anuncia. Deja de
  ofrecerla al arrancar, lo anota en `errores.log` y no molesta más.
- **Otra versión distinta:** se ofrece normalmente. Dar una por rota no bloquea las siguientes.
- **Botón *Buscar actualizaciones*:** ignora el corte a propósito. Cuando el paquete esté
  corregido, es la forma de traerlo sin esperar nada.

La versión instalada se muestra **en la barra de estado, abajo a la derecha**, al lado del
modelo de Gemini. Es la única forma de ver a simple vista si una actualización se aplicó de
verdad: el número del release y el del binario pueden no coincidir.

> `raw.githubusercontent.com` cachea unos minutos: entre el commit y que se vea el cambio
> puede haber demora. No es que falló.

### Decisiones de configuración

| Ajuste | Valor | Por qué |
|---|---|---|
| `RunUpdateAsAdmin` | `false` | Es una app portable en carpeta de usuario; pedir UAC solo agrega una pantalla de permisos que asusta. |
| `ReportErrors` (arranque) | `false` | Sin internet no se muestra nada. |
| `ClearAppDirectory` | `false` | Si alguien dejó un PDF al lado del `.exe`, no es asunto del actualizador borrarlo. |
| `DownloadPath` | `%TEMP%\AutoExam` | El directorio de la app puede no tener permiso de escritura. |
| `Mandatory` | `false` | Se puede posponer o saltear. |

La ventana de actualización es de **WinForms** y no sigue el tema de la app: es la que trae la
librería. A cambio resuelve la descarga con barra de progreso, el reemplazo del ejecutable en
uso y el reinicio, que es la parte difícil.

## Elegir capítulos sueltos

Al agregar un libro, AutoExam lee el **índice interno del PDF** (los marcadores que muestra el
panel lateral de cualquier visor) y arma un módulo por capítulo, con su rango de páginas
calculado: cada capítulo termina donde empieza el siguiente. Si el libro ya estaba cargado,
el botón **Detectar capítulos del PDF** en *Libros* hace lo mismo, y también está a mano dentro
del asistente cuando la lista sale vacía.

Con los capítulos cargados, el paso *Alcance* muestra un chip por capítulo y podés tocar los
que quieras, **salteados incluidos**: capítulos 1, 2, 5, 7, 8 y 15 se traducen en seis rangos de
páginas separados, y el extractor lee solo esas páginas.

**Si el PDF no trae índice** —lo habitual en los escaneados— no se inventa nada: se avisa y
quedan las otras dos vías, dividir en partes iguales o cargar los capítulos a mano.

## Eje temático

El eje no se le delega al modelo. Antes se le mandaba una ventana cualquiera del libro con la
instrucción de "priorizar" el tema, y con páginas sobre otra cosa delante el examen terminaba
hablando de otra cosa.

Ahora el material **se filtra localmente antes de enviarlo**: cada fragmento se puntúa por
cuántas veces aparecen los términos del eje (sin tildes y por raíz, así "arritmia" encuentra
"arritmias" y "arrítmico"), y si hay material suficiente se manda **solo** ese. La instrucción del
prompt pasó a ser terminante, sin la escapatoria de "usá el contenido más cercano" que el modelo
aprovechaba para ignorar el eje. Si el eje casi no aparece, se avisa en la barra de estado y el
examen sale del material más cercano en vez de fallar.

## Figuras del PDF en las preguntas

Las figuras se extraen siempre que tengan tamaño de figura real (220×180 px o más) y no sean
repetidas. Que terminen en el examen depende de que el modelo complete el campo
`ImagenReferencia`, y ahí estaba el problema: sin una cuota explícita casi nunca lo hacía.
Ahora el prompt le exige que **al menos un tercio de las preguntas** sean sobre las figuras
adjuntas, con el identificador exacto, y se adjuntan 5 figuras por petición en lugar de 3.

Al terminar la generación se informa cuántas preguntas quedaron con figura sobre cuántas
figuras se extrajeron. Es la diferencia entre "este PDF no tenía figuras" y "el modelo no las
usó", que antes se veían igual: un examen sin imágenes y ninguna explicación.

## Manejo de PDFs extensos

- `PdfExtractorService` abre el PDF una sola vez y recorre **solo** las páginas del alcance
  elegido, en bloques configurables (15 por defecto). Nunca materializa el documento entero.
- Si el alcance supera las 400 páginas, se toman **muestras representativas** repartidas
  parejo a lo largo de toda la selección, en vez de leer solo el principio.
- El texto se recorta a un presupuesto de caracteres (90.000 por defecto) repartido entre
  todos los fragmentos, para no desbordar la ventana de contexto de Gemini.
- La generación se reparte en **3 peticiones HTTP como máximo**, sin importar cuán largo sea el
  PDF ni cuántas preguntas pidas. Cada una recibe una ventana distinta del material para que el
  examen cubra todo el temario.

## Cuota de la API y errores 429

El nivel gratuito de Gemini limita por **día**, no solo por minuto. El mensaje que devuelve
Google lo dice literalmente:

```
Quota exceeded for metric: generativelanguage.googleapis.com/generate_content_free_tier_requests,
limit: 20, model: gemini-3.5-flash
```

**20 generaciones por día y por clave.** Eso cambia el problema: no alcanza con espaciar las
peticiones, hay que gastar menos. El tamaño del PDF no influye —la extracción es local—; lo que
cuenta es la cantidad de requests.

| | Peticiones | Exámenes/día · 1 clave | Exámenes/día · 3 claves |
|---|---|---|---|
| Examen de 15 preguntas | 1 | 20 | 60 |
| Examen de 30 preguntas | 2 | 10 | 30 |
| Examen de 60 preguntas | **4** | **5** | **15** |
| Peor caso con reintentos | 3 por lote | — | — |

**Los lotes son un intercambio, no una mejora gratis.** Partir el examen en lotes de 15
protege contra la respuesta truncada —60 preguntas con su análisis opción por opción es
mucho JSON para una sola respuesta, y si se corta se pierde el lote entero— pero multiplica
el consumo: un examen de 60 gasta 4 de las 20 generaciones diarias. **Con una sola clave son
5 exámenes largos por día.** Cargar una segunda y una tercera clave es lo que devuelve el
margen.

Las cuatro reglas que lo sostienen:

1. **Lotes de 15, con tope de 4 por examen.** El esquema JSON estricto (`responseSchema`)
   más las explicaciones de una sola oración mantienen cada respuesta holgada dentro del techo
   de tokens. El tope de 4 lotes existe para que bajar *Preguntas por petición* no dispare la
   cantidad de llamadas.
2. **Rotación de claves.** Podés cargar varias claves separadas por comas. Ante un `429`, en vez
   de esperar los 40 segundos que pide Google, se cambia a la siguiente clave y se reintenta al
   instante: cada clave tiene su propia cuota. Solo cuando no queda ninguna aparece el error.
3. **Reintento que hace caso al servidor.** Si no hay otra clave a la que rotar, se lee el
   `retryDelay` del cuerpo del error (o la cabecera `Retry-After`) y se espera eso, con backoff
   exponencial como piso y 90 s de tope. Hasta 3 intentos, porque cada uno también descuenta de
   la cuota diaria.
4. **Un turno a la vez, con 2,5 s entre lotes.** Un `SemaphoreSlim` serializa los envíos, y la
   separación mínima se lleva **por clave**, que es como Google mide el límite. Es a propósito
   lo contrario de paralelizar: mandar peticiones simultáneas contra un límite por minuto solo
   adelanta el `429`. En un examen de 60 preguntas la pausa suma 7,5 s al total.

### Material por lote

Cada lote recibe **su propia ventana de 10 a 20 páginas** del alcance, no el libro entero. Sirve
para tres cosas a la vez: cada petición pesa poco, el examen cubre todo el material en vez de
insistir sobre las primeras páginas, y dos lotes no pueden preguntar lo mismo porque ni
siquiera vieron las mismas páginas. Un examen de 60 sobre un capítulo de 60 páginas reparte
1–15, 16–30, 31–45 y 46–60.

### Que un lote entre completo

Un lote que rinde 3 preguntas de 15 casi siempre es lo mismo: la respuesta se cortó por el
techo de tokens y solo se rescataron los objetos JSON completos. Tres cosas lo evitan.

**El techo pedido es el que el modelo admite.** Lo informa `ListModels` en `outputTokenLimit`
y se guarda por modelo. Antes se calculaba a ojo (900 tokens por pregunta) y podía pedir 17.500
a `gemini-1.5-flash`, que topea en **8.192**: pedir de más no amplía nada, el modelo corta
igual donde tiene su límite. Mientras no se haya consultado se asume 8.192, que es el mínimo
común y nunca se pasa.

**El presupuesto es ajustado, así que la salida es corta.** Con 8.192 tokens entran unas 15
preguntas *si* son concisas, y no entran si el modelo se explaya. Por eso `systemInstruction`
pone límites por campo: 12 palabras por opción, 18 por análisis, 20 por justificación — y
explica *por qué*, que un lote truncado se pierde entero. También exige la cantidad exacta en
un único array JSON cerrado.

**La temperatura baja a 0.35.** Con 0.75 el modelo variaba tanto la extensión como el
contenido; para una salida estructurada donde la cantidad importa, la variación sobra.

El rol y el estilo viajan en `systemInstruction` y no en el prompt: son idénticos en los 4
lotes de un examen y repetirlos en cada uno solo gasta tokens de entrada.

### Reparto de los lotes

El tamaño de lote se calcula al revés de lo natural: primero cuántos lotes hacen falta como
mínimo (30 ÷ 15 = 2), y recién después se reparte el total entre ellos (30 ÷ 2 = **15 + 15**).
Usar el valor configurado como tamaño daba repartos desparejos — con 12, un examen de 30 salía
en **12 + 12 + 6**, y esa tercera petición es cuota diaria tirada. El ajuste *Preguntas por
petición* es un **mínimo**: puede pedir lotes más grandes, nunca más chicos.

La **cuota diaria** se distingue de la del minuto por el `quotaId` del `QuotaFailure`, y las dos
se tratan distinto: la diaria quema la clave para toda la sesión, la del minuto solo la posterga.

### Varias API Keys

En *Ajustes → API Keys de Google Gemini*, pegalas separadas por comas. Debajo del campo se ve
cuántas quedaron cargadas. Son gratis: se sacan de [Google AI Studio](https://aistudio.google.com/app/apikey),
una por proyecto de Google Cloud.

Con tres claves el presupuesto pasa de 20 a 60 exámenes por día, y el cambio es invisible: la
barra de estado avisa *"La clave 1 agotó su cuota diaria. Continuando con la clave 2 de 3"* y el
examen sigue.

## Subida del PDF con la Files API

Para alcances grandes o sin texto extraíble, el PDF viaja **una sola vez** a Google en vez de
mandarse como texto dentro de cada petición:

1. Se **recorta al alcance elegido** con `PdfDocumentBuilder` —capítulos 1, 2, 5 y 7 dan un PDF
   de solo esas páginas—, porque subir 900 páginas para preguntar sobre 60 es lento y caro.
2. Se sube por el protocolo *resumable* (`/upload/v1beta/files`) y se espera a que Google lo pase
   a `ACTIVE`.
3. En `generateContent` va como una línea de JSON (`file_data.file_uri`), no como megabytes de
   Base64.

**No siempre conviene**, y por eso no es incondicional: para un alcance chico con texto limpio,
mandar 40.000 caracteres es más rápido y más barato que hacerle leer el PDF página por página
(cada página le cuesta ~258 tokens de entrada). Se sube cuando el alcance no tiene texto
extraíble o supera las 120 páginas. Si la subida falla, se sigue con el texto extraído: es una
optimización, no un requisito.

El `fileUri` se **reutiliza** mientras dure: Google conserva los archivos 48 h, así que el
segundo examen sobre el mismo capítulo arranca sin subir nada. Se puede apagar entero con
`UsarFilesApi` en `config.json`.

## PDFs escaneados (sin texto extraíble)

Un PDF que salió de un escáner o de una fotocopiadora no tiene caracteres: cada página es
una foto. PdfPig no encuentra nada que extraer y antes eso terminaba en
*"El alcance elegido no tiene texto extraíble"*.

Ahora, cuando una página deja menos de 40 caracteres útiles, la app **rescata su imagen** con
`page.GetImages()` y se la manda a Gemini como `inline_data`, aprovechando que el modelo es
multimodal y le puede leer el texto directamente. El flujo:

1. Se toman como máximo **10 páginas** por examen, repartidas (hasta 3 por bloque leído y
   espaciadas dentro del bloque) para que la muestra cubra todo el alcance y no solo el principio.
2. Cada página se baja a **1600 px** de lado y se reencoda en **JPEG**: a esa resolución el texto
   se sigue leyendo y el Base64 de un lote entra holgado en el request.
3. En el prompt las páginas van declaradas aparte de las figuras, con su número de página, y con
   la instrucción explícita de tratarlas como bibliografía — nunca como ilustración de una pregunta.
4. La cascada de reintentos puede soltar las **figuras**, pero nunca las páginas escaneadas:
   en un PDF escaneado son el material, no un adorno.
5. Terminada la generación, esas imágenes se borran del disco: ya cumplieron su función.

Esto es independiente de *"Incluir preguntas sobre gráficos e imágenes"*: destildar esa opción
significa "no me hagas preguntas sobre figuras", no "no leas mi PDF".

**Lo que sigue sin funcionar:** los escaneos comprimidos en **JBIG2** o **JPEG 2000**, que PdfPig
no puede decodificar. En ese caso el mensaje de error lo dice y sugiere pasar el PDF por un OCR
(Acrobat, Google Drive) antes de volver a agregarlo. Un PDF con páginas realmente en blanco
también sigue avisando que el alcance está vacío.

## Autenticación con la API de Gemini

La API Key **siempre** viaja en la cabecera HTTP `x-goog-api-key` y **nunca** como query
parameter `?key=`. Las claves del formato nuevo (`AQ.Ab8...`) que emite Google AI Studio son
rechazadas con `401` si se mandan en la URL.

Todo request a Gemini se arma en un único lugar, `GeminiApiService.CrearRequest(...)`, de modo
que no hay forma de que una llamada se saltee la cabecera. Antes de enviarla, la clave pasa por
`NormalizarApiKey(...)`, que descarta espacios, saltos de línea, controles y caracteres
invisibles (BOM, zero-width) — la causa habitual de un `401` engañoso al copiar y pegar
desde el navegador.

Si aun así recibís `401`, no es un problema de la app: revisá que la clave esté completa y que
el proyecto de Google AI Studio tenga habilitada la *Generative Language API*.

## Prueba de conexión y tokens de salida

La prueba de la clave (pantalla de inicio y botón *Probar conexión*) manda el pedido más chico
posible: el prompt `Responde OK`, sin esquema JSON ni material del PDF, con `maxOutputTokens: 100`
y `thinkingConfig.thinkingBudget: 0`. Un `HTTP 200` ya alcanza para darla por buena **aunque el
modelo no devuelva texto**: los modelos que razonan pueden gastar todo el cupo pensando, y eso no
dice nada malo de la clave.

La generación real de exámenes usa un techo alto (`maxOutputTokens: 32768`), porque el JSON de un
lote de 15 preguntas con el análisis opción por opción supera cómodo los 8.192 tokens y el
razonamiento del modelo se descuenta del mismo cupo. Si un modelo rechaza ese techo, la app baja
sola a 8.192 para el resto de la sesión.

## Sobre el modelo de Gemini

El modelo por defecto es **`gemini-1.5-flash`**.

> **Aviso.** Google retiró la familia 1.5 para los proyectos de API nuevos, así que en muchas
> claves este nombre contesta `404`. No rompe nada —el punto 3 de abajo lo resuelve solo— pero
> el modelo que termina generando puede no ser el configurado. Si querés saber cuál está
> corriendo: *Ajustes → Detectar*, o mirá `errores.log`. La evidencia de que 1.5 ya no está
> disponible aparece en el propio error de cuota de Google, que nombra el modelo con el que sí
> generó.

Google retira generaciones de modelos con cierta frecuencia, y cuando eso pasa la API empieza
a devolver `404 model not found`. La app se defiende de cuatro formas:

1. **Una sola fuente de verdad.** El nombre del modelo vive únicamente en
   `AppConfig.ModeloPorDefecto`. Ningún otro punto del código lo repite: un literal suelto en
   `SolicitudGeneracion` llegó a hacer parecer que la app apuntaba a un modelo retirado.
2. **Migración automática.** Si `config.json` apunta a una familia retirada (`gemini-1.0`,
   `gemini-pro`, `gemini-2.0`), al arrancar se reemplaza sola por el modelo por defecto y te
   avisa con un cartel en Ajustes. `gemini-1.5` **no** está en esa lista mientras sea el
   default: si estuviera, la migración lo reemplazaría por sí mismo y mostraría el cartel en
   cada arranque sin cambiar nada.
3. **Corrección sobre la marcha.** Si durante la generación el modelo devuelve `404`, la app
   consulta `GET /v1beta/models`, elige el *flash* estable que tu clave sí habilita, guarda el
   cambio y sigue generando. No hace falta ir a Ajustes ni volver a empezar.
4. **Botón "Detectar modelos de mi clave".** Lo mismo, a pedido: llena el desplegable con la
   lista **real** de tu API Key. Es la salida definitiva ante cualquier duda sobre qué modelo
   existe, porque no depende de ninguna lista escrita en el código —ni de la memoria de nadie.

Los modelos *flash* son los que mejor se llevan con el nivel gratuito. Se filtran
automáticamente del listado los modelos que no sirven para generar exámenes de texto
(embeddings, imagen, TTS, audio, Gemma).
