namespace AutoExam.Models;

/// <summary>
/// De donde salen las preguntas de un examen. Es lo que el paso Material del asistente
/// pregunta primero, porque condiciona todo lo que viene despues.
///
/// Solo el primero genera con IA. Los otros tres arman el examen localmente con preguntas que
/// ya existen: son instantaneos, no gastan cuota y no necesitan conexion (RN-27 / RN-40).
/// Los cuatro comparten el paso Formato, que es lo que hace que el cronometro de US-034 sea
/// una opcion del formato y no de un tipo de generacion puntual (RN-43).
/// </summary>
public enum OrigenPreguntas
{
    /// <summary>Material propio: PDF, Office o fotos, con preguntas nuevas generadas por IA.</summary>
    Material,

    /// <summary>Mezcla de examenes ya rendidos (US-026).</summary>
    ExamenesAnteriores,

    /// <summary>Solo lo que el alumno viene fallando (US-032).</summary>
    PreguntasFalladas,

    /// <summary>Un examen que un compañero compartio y este alumno importo (US-037).</summary>
    Importado,
}
