using System.Windows.Input;

namespace AutoExam.Models;

/// <summary>Que hace un atajo de la pantalla de examen.</summary>
public enum AccionAtajo
{
    /// <summary>Elegir una opcion. El indice va en <see cref="Atajo.Opcion"/>.</summary>
    Responder,
    Siguiente,
    Anterior,
    Saltear,
}

/// <summary>
/// Lo minimo que el manejador de atajos necesita de la pantalla de examen.
///
/// Existe para que <see cref="AutoExam.Behaviors.AtajosDeExamen"/> no dependa del ViewModel
/// concreto: asi la suite de regresion de atajos puede seguir probando el comportamiento real
/// de las teclas contra un doble, sin tener que montar un examen en curso completo.
/// </summary>
public interface IPantallaDeExamen
{
    /// <summary>Cuantas opciones tiene la pregunta en pantalla. Fija que teclas son validas.</summary>
    int OpcionesVisibles { get; }

    System.Windows.Input.ICommand ResponderCommand { get; }
    System.Windows.Input.ICommand SiguienteCommand { get; }
    System.Windows.Input.ICommand AnteriorCommand { get; }
    System.Windows.Input.ICommand SaltearCommand { get; }
}

/// <summary>Una tecla y lo que hace.</summary>
/// <param name="Tecla">Tecla fisica.</param>
/// <param name="Accion">Que dispara.</param>
/// <param name="Opcion">Indice de opcion (0..3) cuando la accion es Responder; -1 si no aplica.</param>
public sealed record Atajo(Key Tecla, AccionAtajo Accion, int Opcion = -1)
{
    public bool EsDeOpcion => Accion == AccionAtajo.Responder;
}

/// <summary>
/// Mapeo de los atajos de teclado del examen (US-036 / RN-44).
///
/// RN-44 pide un mapeo "centralizado y documentado, no hardcodeado disperso por vista". Antes
/// las teclas vivian escritas a mano como dieciseis KeyBinding en ExamenView.xaml: agregar una
/// tecla o corregir una equivocada era editar XAML, y no habia forma de que un test verificara
/// que 1..4 y A..D apuntaran a la misma opcion. Ahora la lista vive aca y la vista la lee.
///
/// Tres familias, y las tres importan:
/// · 1..4 y el pad numerico, que es lo que uno tipea sin mirar;
/// · A..D, porque las opciones se rotulan con letras en pantalla y esa es la correspondencia
///   que el alumno tiene delante;
/// · flechas para moverse, mas Enter (avanzar) y S (saltear).
///
/// Que los atajos no se disparen mientras se escribe en un campo de texto no se resuelve aca
/// sino en <see cref="AutoExam.Behaviors.AtajosDeExamen"/>, que es quien conoce el foco.
/// </summary>
public static class AtajosExamen
{
    /// <summary>Cuantas opciones puede tener una pregunta. Fija el rango de 1..4 y A..D.</summary>
    public const int MaximoDeOpciones = 4;

    private static readonly Key[] Numeros = { Key.D1, Key.D2, Key.D3, Key.D4 };
    private static readonly Key[] Pad = { Key.NumPad1, Key.NumPad2, Key.NumPad3, Key.NumPad4 };
    private static readonly Key[] Letras = { Key.A, Key.B, Key.C, Key.D };

    /// <summary>Todos los atajos, en un solo lugar.</summary>
    public static IReadOnlyList<Atajo> Todos { get; } = Construir();

    private static List<Atajo> Construir()
    {
        var lista = new List<Atajo>();

        for (int i = 0; i < MaximoDeOpciones; i++)
        {
            lista.Add(new Atajo(Numeros[i], AccionAtajo.Responder, i));
            lista.Add(new Atajo(Pad[i], AccionAtajo.Responder, i));
            lista.Add(new Atajo(Letras[i], AccionAtajo.Responder, i));
        }

        lista.Add(new Atajo(Key.Right, AccionAtajo.Siguiente));
        lista.Add(new Atajo(Key.Enter, AccionAtajo.Siguiente));
        lista.Add(new Atajo(Key.Left, AccionAtajo.Anterior));
        lista.Add(new Atajo(Key.S, AccionAtajo.Saltear));

        return lista;
    }

    /// <summary>El atajo de una tecla, o null si esa tecla no hace nada en el examen.</summary>
    public static Atajo? De(Key tecla) => Todos.FirstOrDefault(a => a.Tecla == tecla);

    /// <summary>
    /// La referencia que se le muestra al alumno la primera vez (US-036). Se arma desde la
    /// misma lista, asi que no puede quedar desactualizada respecto de lo que las teclas
    /// hacen de verdad — que es el modo habitual en que una ayuda de atajos miente.
    /// </summary>
    public static IReadOnlyList<(string Teclas, string Que)> Referencia { get; } = new[]
    {
        ("1 - 4  ·  A - D", "Elegir una opción"),
        ("→  o  Enter", "Pregunta siguiente"),
        ("←", "Pregunta anterior"),
        ("S", "Saltear la pregunta"),
    };
}
