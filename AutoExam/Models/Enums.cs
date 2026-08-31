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

/// <summary>
/// Familia de la fuente/material de estudio. Determina como se copia dentro de la
/// Biblioteca (archivo unico vs. carpeta de imagenes ordenada) y que camino de
/// extraccion usa (contrato <c>IExtractorContenido</c>, arquitectura Inc-4 §4.1).
/// <para>
/// SINCRONIZACION (arquitectura Inc-4 §5): el owner de este enum es M1
/// (extraccion-multiformato). Lo declara M3 en modo contract-first para desbloquear
/// la compilacion; los valores y el orden estan cerrados en §4.1 — al integrar M1,
/// esta declaracion y la de M1 deben ser identicas (conflicto trivial de una unica
/// definicion duplicada, mismo criterio que Inc-1 §5 con AppConfig).
/// </para>
/// </summary>
public enum TipoFuente
{
    Pdf = 0,
    Word = 1,
    Excel = 2,
    PowerPoint = 3,
    SetImagenes = 4
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
