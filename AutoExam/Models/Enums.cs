namespace AutoExam.Models;

/// <summary>Estado de interaccion del usuario con la pregunta durante el examen.</summary>
public enum EstadoPreguntaEnum
{
    SinResponder = 0,
    Respondida = 1,
    Salteada = 2
}

/// <summary>Resultado de la correccion local, calculado recien al finalizar el examen.</summary>
public enum ResultadoPreguntaEnum
{
    Pendiente = 0,
    Correcta = 1,
    Incorrecta = 2,
    Salteada = 3
}

/// <summary>Modalidades de seleccion del alcance del examen. Son combinables entre si.</summary>
[Flags]
public enum ModoAlcance
{
    Ninguno = 0,
    Modulos = 1,
    RangoPaginas = 2,
    TemaLibre = 4
}
