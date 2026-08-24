namespace AutoExam.Models;

/// <summary>
/// Una fila del desglose "por que esta opcion es correcta / incorrecta".
/// Expone si acierta o no, nunca un color: el pincel lo elige la vista contra el
/// tema activo, para que el desglose se lea igual de bien en claro y en oscuro.
/// </summary>
public class LineaAnalisis
{
    public string Encabezado { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
    public bool EsCorrecta { get; init; }
    public bool EsElegidaPorUsuario { get; init; }

    public string Prefijo => EsCorrecta ? "✔" : "✘";

    public string Marca => EsElegidaPorUsuario ? "  (tu respuesta)" : string.Empty;
}
