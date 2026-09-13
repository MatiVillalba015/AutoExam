using System.Windows.Controls;

namespace AutoExam.Views;

/// <summary>
/// Las claves de Gemini como pestanias (US-042). Su DataContext es un
/// <see cref="ViewModels.ClavesDeGemini"/>; lo comparten la configuracion inicial y Ajustes.
/// </summary>
public partial class CamposDeClaves : UserControl
{
    public CamposDeClaves() => InitializeComponent();
}
