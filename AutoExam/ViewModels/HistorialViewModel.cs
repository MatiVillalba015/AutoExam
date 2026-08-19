using System.Collections.ObjectModel;
using AutoExam.Models;
using AutoExam.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoExam.ViewModels;

/// <summary>Historial de intentos y estadisticas acumuladas.</summary>
public partial class HistorialViewModel : PaginaViewModel
{
    private readonly SesionUsuarioService _sesion;
    private readonly IDialogos _dialogos;
    private readonly INavegacion _nav;

    public HistorialViewModel(SesionUsuarioService sesion, IDialogos dialogos, INavegacion nav)
        : base("historial", "Historial", "History24")
    {
        _sesion = sesion;
        _dialogos = dialogos;
        _nav = nav;

        Escala = new ObservableCollection<string>(EvaluadorUBA.DescribirEscala());
    }

    public ObservableCollection<ExamenRendido> Examenes => _sesion.Historial;

    public ObservableCollection<string> Escala { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayExamenes))]
    private int _total;

    [ObservableProperty]
    private string _resumen = "Todavia no rendiste ningun examen.";

    [ObservableProperty]
    private string _detalle = string.Empty;

    [ObservableProperty]
    private string _promedio = "-";

    [ObservableProperty]
    private string _aciertos = "-";

    [ObservableProperty]
    private string _mejorNota = "-";

    public bool HayExamenes => Total > 0;

    public void Refrescar()
    {
        var perfil = _sesion.Perfil;
        Total = perfil.TotalExamenes;
        Insignia = Total == 0 ? string.Empty : $"{Total} rendidos";

        if (Total == 0)
        {
            Resumen = "Todavia no rendiste ningun examen.";
            Detalle = "Cuando rindas el primero vas a ver aca tu promedio y tu evolucion.";
            Promedio = Aciertos = MejorNota = "-";
            return;
        }

        Promedio = perfil.PromedioNota.ToString("0.0");
        Aciertos = $"{perfil.PromedioAciertos:0}%";
        MejorNota = perfil.MejorNota.ToString();

        Resumen = $"{Total} examenes rendidos · {perfil.Aprobados} aprobados · {perfil.Aplazos} aplazos";

        Detalle = string.Join(Environment.NewLine, new[]
        {
            $"Preguntas: {perfil.TotalCorrectas} correctas de {perfil.TotalPreguntas}",
            $"Salteadas en total: {perfil.TotalSalteadas}"
        });
    }

    public override void AlEntrar() => Refrescar();

    [RelayCommand]
    private void Borrar()
    {
        if (!_dialogos.Confirmar("¿Borrar todo el historial de examenes?\n\nEsta accion no se puede deshacer."))
        {
            return;
        }

        _sesion.BorrarHistorial();
        Refrescar();
        _nav.Estado("Historial borrado.");
    }
}
