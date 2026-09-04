using System.Windows.Input;

namespace AutoExam.Tests.TestDoubles;

/// <summary>
/// <see cref="ICommand"/> mínimo que solo registra invocaciones. Se usa para probar el
/// contrato de <c>{Binding ...Command}</c> de Views/ExamenView.xaml (AC-T11/NFR-09) sin
/// depender del estado interno real de <c>ExamenViewModel</c> (que exige un examen en curso
/// para que <c>Responder</c>/<c>Siguiente</c>/<c>Anterior</c> hagan algo) — el binding de WPF
/// resuelve <c>{Binding ResponderCommand}</c> por nombre de propiedad sobre el DataContext que
/// tenga la vista, sin importar su tipo real, así que este doble reemplaza a
/// <c>ExamenViewModel</c> por completo para esta suite.
/// </summary>
public sealed class RecordingCommand : ICommand
{
    public int Invocaciones { get; private set; }

    public object? UltimoParametro { get; private set; }

    /// <summary>Se agrega la lista completa (no solo el último) para poder detectar, en el
    /// mismo test, si un atajo dispara el comando más de una vez por error.</summary>
    public List<object?> Parametros { get; } = new();

    public bool PuedeEjecutar { get; set; } = true;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => PuedeEjecutar;

    public void Execute(object? parameter)
    {
        Invocaciones++;
        UltimoParametro = parameter;
        Parametros.Add(parameter);
    }

    /// <summary>No se usa en esta suite (no cambia CanExecute en medio de un test), pero
    /// completa la interfaz sin dejar el evento sin usar como advertencia del compilador.</summary>
    public void ForzarReevaluacionDeCanExecute() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Reemplazo de <c>ExamenViewModel</c> como DataContext de <c>ExamenView</c>, con un
/// <see cref="RecordingCommand"/> por cada comando que ExamenView.xaml referencia desde
/// <c>UserControl.InputBindings</c> (ver contrato en specs/03-architecture.md §4.4 —
/// los <c>KeyBinding</c> de esta vista, no los nuevos de MainWindow).
/// </summary>
public sealed class ExamenViewFakeViewModel : AutoExam.Models.IPantallaDeExamen
{
    /// <summary>
    /// Cuatro opciones, como una pregunta normal: es lo que hace que las teclas 1..4 y A..D
    /// cuenten como "opcion visible" y lleguen a disparar el comando (US-036).
    /// </summary>
    public int OpcionesVisibles { get; set; } = AutoExam.Models.AtajosExamen.MaximoDeOpciones;

    public RecordingCommand ResponderCommand { get; } = new();
    public RecordingCommand SiguienteCommand { get; } = new();
    public RecordingCommand AnteriorCommand { get; } = new();
    public RecordingCommand SaltearCommand { get; } = new();

    ICommand AutoExam.Models.IPantallaDeExamen.ResponderCommand => ResponderCommand;
    ICommand AutoExam.Models.IPantallaDeExamen.SiguienteCommand => SiguienteCommand;
    ICommand AutoExam.Models.IPantallaDeExamen.AnteriorCommand => AnteriorCommand;
    ICommand AutoExam.Models.IPantallaDeExamen.SaltearCommand => SaltearCommand;

    /// <summary>Todos los comandos, para poder recorrerlos genéricamente y asegurar que
    /// ningún atajo dispara un comando distinto al esperado.</summary>
    public IReadOnlyDictionary<string, RecordingCommand> Comandos => new Dictionary<string, RecordingCommand>
    {
        [nameof(ResponderCommand)] = ResponderCommand,
        [nameof(SiguienteCommand)] = SiguienteCommand,
        [nameof(AnteriorCommand)] = AnteriorCommand,
        [nameof(SaltearCommand)] = SaltearCommand,
    };
}
