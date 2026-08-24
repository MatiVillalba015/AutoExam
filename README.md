# AutoExam

Aplicación de escritorio para estudiar con exámenes generados a partir del propio material
de la materia. Se carga un PDF (apuntes, libros de cientos de páginas), se elige el alcance
—capítulos sueltos, un eje temático o el libro entero— y la app arma un examen multiple
choice con esa fuente. Después se rinde dentro del programa, se corrige solo con la escala
de calificaciones de la UBA y se pueden reintentar en bucle las preguntas erradas y
salteadas hasta llegar al 100 % de aciertos.

Sirve para preparar finales y parciales sin tener que escribirse uno mismo las preguntas:
el contenido sale del material que uno ya usa para cursar, no de un banco genérico.

## Lenguaje

**C#** (C# 12, .NET 8), con las vistas escritas en **XAML**.

## Stack

| Capa | Tecnología |
|---|---|
| Plataforma | .NET 8 (`net8.0-windows`), Windows x64 |
| UI | WPF + [WPF-UI](https://github.com/lepoco/wpfui) 4.3.0 (Fluent / Windows 11) |
| Arquitectura | MVVM con [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.0 |
| PDF | [PdfPig](https://github.com/UglyToad/PdfPig) 0.1.15 (texto, figuras y rescate de escaneos) |
| IA | Google Gemini (`generateContent` y Files API) vía `HttpClient` |
| Actualizaciones | [AutoUpdater.NET](https://github.com/ravibpatel/AutoUpdater.NET) 1.9.3 |
| Persistencia | Archivos JSON en `%LOCALAPPDATA%\AppEstudioUBA` |

Se distribuye como un único `.exe` self-contained: no hace falta instalar .NET en la
máquina donde se usa.
