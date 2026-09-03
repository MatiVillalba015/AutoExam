using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoExam.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoExam.ViewModels;

/// <summary>
/// Un acceso del menu principal: una de las cuatro tarjetas grandes (US-030 / US-031).
///
/// Desde US-031 la tarjeta representa una ACCION, no una seccion. El cambio importa: el
/// spec pedia los cuatro botones de navegacion mas cuatro accesos directos, pero tres de
/// esos cuatro atajos llevan exactamente a la misma pantalla que el boton del mismo nombre,
/// y el ultimo criterio de la misma historia pide que "no quede duplicado ni confuso".
/// Resuelto con el usuario: las tarjetas pasan a ser las acciones, y la navegacion por
/// seccion queda en la barra de arriba y en Ctrl+1..5 (US-004), que no cambiaron.
///
/// <paramref name="seccion"/> no es a donde lleva la tarjeta —eso lo decide su comando—:
/// es de donde saca su insignia, el dato vivo que la tarjeta muestra abajo ("3 materiales",
/// "12 rendidos"). Se refleja tal cual desde la pagina que ya lo calcula, sin duplicarlo.
/// </summary>
public partial class AccesoDeInicio : ObservableObject
{
    private readonly PaginaViewModel _seccion;

    public AccesoDeInicio(
        PaginaViewModel seccion,
        string titulo,
        string icono,
        string descripcion,
        ICommand comando)
    {
        _seccion = seccion;
        Titulo = titulo;
        Icono = icono;
        Descripcion = descripcion;
        Comando = comando;

        seccion.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PaginaViewModel.Insignia))
            {
                OnPropertyChanged(nameof(Insignia));
            }
        };
    }

    /// <summary>Que hace la tarjeta, en dos o tres palabras.</summary>
    public string Titulo { get; }

    /// <summary>Nombre del simbolo de WPF-UI.</summary>
    public string Icono { get; }

    /// <summary>Para que sirve, en una linea.</summary>
    public string Descripcion { get; }

    /// <summary>La accion. RN-36: siempre es un atajo a un flujo que ya existe.</summary>
    public ICommand Comando { get; }

    /// <summary>Dato vivo de la seccion relacionada. Vacio cuando todavia no hay nada que contar.</summary>
    public string Insignia => _seccion.Insignia;
}

/// <summary>
/// Una linea del resumen de actividad reciente del menu (US-031).
///
/// Es un valor de solo lectura y no una vista de <see cref="ExamenRendido"/>: RN-37 pide
/// explicitamente que desde aca no se pueda corregir ni tocar el examen, solo mirarlo e ir a
/// la pantalla donde si se puede. Copiando los cuatro datos que se muestran, no hay forma de
/// que un binding descuidado termine escribiendo sobre el historial.
/// </summary>
public sealed class ActividadReciente
{
    public ActividadReciente(ExamenRendido examen)
    {
        Titulo = examen.TituloTexto;
        Materia = examen.Materia;
        Nota = examen.NotaUBA.ToString();
        Aprobado = examen.Aprobado;
        Detalle = $"{examen.Correctas} de {examen.TotalPreguntas} · {examen.Fecha:dd/MM/yyyy}";
    }

    public string Titulo { get; }

    public string Materia { get; }

    /// <summary>Color de la materia, resuelto al construir la linea (US-027 / RN-30).</summary>
    public string ColorMateria => PaletaMaterias.ColorDe(Materia);

    public string Nota { get; }

    public bool Aprobado { get; }

    public string Detalle { get; }

    public string Accesible => $"{Titulo}, nota {Nota}, {Detalle}";
}

/// <summary>
/// Menu principal: cuatro acciones grandes y un resumen de lo ultimo que se hizo
/// (US-030 / US-031).
///
/// Reemplaza a la barra lateral que estaba pegada al borde izquierdo. El cambio no es solo
/// estetico: sacando la columna fija de 228 px, el contenido de cada seccion pasa a disponer
/// del ancho completo de la ventana, que es lo que hace que el centrado de US-017 (RN-35)
/// tenga espacio real con el que trabajar en un monitor ancho.
///
/// El examen no tiene tarjeta a proposito: no es un lugar al que uno decide ir, es donde la
/// app te deja cuando hay un examen para rendir. Sigue siendo alcanzable con Ctrl+3 y se abre
/// solo al generar uno.
/// </summary>
public partial class InicioViewModel : PaginaViewModel
{
    /// <summary>
    /// Cuantos examenes muestra el resumen. Tres es lo que entra en una franja de una linea
    /// por examen sin empujar las tarjetas fuera de la ventana en un portatil de 768 px.
    /// </summary>
    private const int ExamenesEnElResumen = 3;

    /// <summary>
    /// Explicacion de la app para quien la abre por primera vez (US-031).
    ///
    /// RN-39: es texto fijo, definido una sola vez y aca. No pasa por Gemini a proposito —
    /// alguien que todavia no entiende que hace la app es exactamente quien puede no tener
    /// cargada la clave, y una explicacion que a veces aparece y a veces no es peor que
    /// ninguna. Ademas seria absurdo gastar cuota en un texto que nunca cambia.
    ///
    /// Escrito para un estudiante, no para alguien que ya sabe lo que es un "material" o un
    /// "alcance": nombra las cosas por lo que uno hace con ellas.
    /// </summary>
    public const string QueEsAutoExam =
        "AutoExam te toma examen de lo que estás estudiando, con tu propio material.\n\n" +
        "Subís lo que tengas —un PDF, un Word, una presentación o fotos de tus apuntes escritos " +
        "a mano— y la app genera preguntas de opción múltiple sobre ese contenido. No trae " +
        "preguntas de ningún lado: salen de lo que vos subiste.\n\n" +
        "Respondés el examen y te lo corrige al instante con la escala de la UBA (del 1 al 10, " +
        "se aprueba con 4), te muestra en qué te equivocaste y por qué cada opción era correcta " +
        "o no. Todo queda guardado, así que meses después podés volver a mirar un examen viejo " +
        "pregunta por pregunta.\n\n" +
        "Para empezar te alcanza con subir un material y tocar «Generar examen».";

    public InicioViewModel(IEnumerable<AccesoDeInicio> accesos)
        : base("inicio", "Inicio", "Home24")
    {
        Accesos = new ObservableCollection<AccesoDeInicio>(accesos);
    }

    /// <summary>Texto de <see cref="QueEsAutoExam"/>, para enlazarlo desde la vista.</summary>
    public string Explicacion => QueEsAutoExam;

    [ObservableProperty]
    private bool _mostrarQueEs;

    /// <summary>
    /// Abre y cierra la explicacion. Es un panel dentro del menu y no una ventana aparte:
    /// quien no entiende que hace la app no deberia tener que cerrar algo para volver a verla.
    /// </summary>
    [RelayCommand]
    private void AlternarQueEs() => MostrarQueEs = !MostrarQueEs;

    public ObservableCollection<AccesoDeInicio> Accesos { get; }

    /// <summary>Ultimos examenes rendidos, del mas nuevo al mas viejo. Solo lectura (RN-37).</summary>
    public ObservableCollection<ActividadReciente> Actividad { get; } = new();

    [ObservableProperty]
    private string _saludo = "Hola";

    [ObservableProperty]
    private string _bajada = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySinActividad))]
    private bool _hayActividad;

    public bool HaySinActividad => !HayActividad;

    /// <summary>
    /// Que hacer primero cuando todavia no hay nada. El criterio pide que el menu "invite a la
    /// primera accion" en vez de mostrar la franja vacia: una tarjeta de resumen en blanco se
    /// lee como algo roto, no como "todavia no empezaste".
    /// </summary>
    [ObservableProperty]
    private string _invitacion = string.Empty;

    /// <summary>
    /// Actualiza el saludo, la bajada y el resumen con el estado real de la biblioteca y del
    /// historial. Se llama al entrar, para que la pantalla diga algo distinto el primer dia
    /// que el mes siguiente.
    /// </summary>
    public void Actualizar(int libros, IEnumerable<ExamenRendido> historial)
    {
        var ultimos = historial
            .OrderByDescending(e => e.Fecha)
            .Take(ExamenesEnElResumen)
            .ToList();

        Actividad.Clear();

        foreach (var examen in ultimos)
        {
            Actividad.Add(new ActividadReciente(examen));
        }

        HayActividad = Actividad.Count > 0;

        int examenes = historial.Count();

        Saludo = libros == 0 && examenes == 0 ? "Empezá por acá" : "¿Qué estudiamos hoy?";

        Bajada = (libros, examenes) switch
        {
            (0, _) => "Subí tu primer material y armá un examen con él.",
            (_, 0) => $"Tenés {Plural(libros, "material", "materiales")} listo para generar tu primer examen.",
            _ => $"{Plural(libros, "material", "materiales")} · {Plural(examenes, "examen rendido", "examenes rendidos")}."
        };

        Invitacion = libros == 0
            ? "Subí tu primer material para empezar: un PDF, un Word o fotos de tus apuntes."
            : "Todavía no rendiste ningún examen. Generá el primero con el material que ya subiste.";
    }

    private static string Plural(int cantidad, string singular, string plural) =>
        cantidad == 1 ? $"1 {singular}" : $"{cantidad} {plural}";
}
