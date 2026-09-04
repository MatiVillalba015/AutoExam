using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AutoExam.Models;
using AutoExam.ViewModels;

namespace AutoExam.Behaviors;

/// <summary>
/// Conecta las teclas del examen (US-036) con los comandos del ViewModel, leyendo el mapeo de
/// <see cref="AtajosExamen"/>.
///
/// Existe por RN-44: "los atajos se definen con un mapeo centralizado y documentado, no
/// hardcodeado disperso por vista". Antes eran dieciseis <c>KeyBinding</c> escritos a mano en
/// ExamenView.xaml — agregar una tecla era editar XAML, y nada garantizaba que la tecla 1 y la
/// tecla A apuntaran a la misma opcion. Ahora la lista vive en un solo lugar y esto la aplica.
///
/// Lo que resuelve aca y no en la lista es el FOCO: los atajos no se disparan mientras se
/// escribe en un campo de texto. Escribir "sacar" en un buscador no puede saltear una pregunta
/// por la S. Es el criterio explicito de US-036, y es tambien la razon por la que esto es un
/// manejador de PreviewKeyDown y no un KeyBinding: un KeyBinding se resuelve antes de que el
/// control con foco vea la tecla, asi que desde ahi no hay forma de dejarla pasar.
/// </summary>
public static class AtajosDeExamen
{
    public static readonly DependencyProperty ActivosProperty =
        DependencyProperty.RegisterAttached(
            "Activos", typeof(bool), typeof(AtajosDeExamen),
            new PropertyMetadata(false, AlCambiar));

    public static bool GetActivos(DependencyObject d) => (bool)d.GetValue(ActivosProperty);

    public static void SetActivos(DependencyObject d, bool value) => d.SetValue(ActivosProperty, value);

    private static void AlCambiar(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement elemento)
        {
            return;
        }

        elemento.PreviewKeyDown -= AlBajarTecla;

        if (e.NewValue is true)
        {
            elemento.PreviewKeyDown += AlBajarTecla;
        }
    }

    private static void AlBajarTecla(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement elemento ||
            elemento.DataContext is not IPantallaDeExamen vm)
        {
            return;
        }

        // Con un campo de texto en foco la tecla es texto, no un atajo.
        if (EscribiendoEnUnCampo())
        {
            return;
        }

        // Con modificadores la tecla es otra cosa: Ctrl+1..5 navega entre secciones (US-004),
        // y sin esta guarda tambien elegiria la opcion 1.
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (AtajosExamen.De(e.Key) is not Atajo atajo)
        {
            return;
        }

        bool manejado = Ejecutar(vm, atajo);

        if (manejado)
        {
            e.Handled = true;
        }
    }

    private static bool Ejecutar(IPantallaDeExamen vm, Atajo atajo)
    {
        switch (atajo.Accion)
        {
            case AccionAtajo.Responder:
                // Solo si esa opcion existe en la pregunta que se esta viendo: el criterio
                // habla de "una opcion visible". Con tres opciones, la tecla 4 no hace nada.
                if (atajo.Opcion < 0 || atajo.Opcion >= vm.OpcionesVisibles)
                {
                    return false;
                }

                // El indice viaja como string, igual que lo hacian los KeyBinding que esto
                // reemplaza: ResponderCommand ya sabe interpretarlo y el contrato de NFR-09
                // (que tecla manda que parametro) queda intacto.
                vm.ResponderCommand.Execute(atajo.Opcion.ToString());
                return true;

            case AccionAtajo.Siguiente:
                return EjecutarSiPuede(vm.SiguienteCommand);

            case AccionAtajo.Anterior:
                return EjecutarSiPuede(vm.AnteriorCommand);

            case AccionAtajo.Saltear:
                return EjecutarSiPuede(vm.SaltearCommand);

            default:
                return false;
        }
    }

    private static bool EjecutarSiPuede(System.Windows.Input.ICommand comando)
    {
        if (!comando.CanExecute(null))
        {
            return false;
        }

        comando.Execute(null);
        return true;
    }

    private static bool EscribiendoEnUnCampo() => Keyboard.FocusedElement switch
    {
        // El enunciado de la pregunta es un TextBox de solo lectura (para poder copiarlo con
        // el mouse). Si contara como "campo de texto", hacer click en el enunciado dejaria
        // muertos todos los atajos del examen sin ninguna señal de por que.
        TextBox { IsReadOnly: true } => false,

        TextBoxBase or PasswordBox => true,
        ComboBox { IsEditable: true } => true,
        _ => false
    };
}
