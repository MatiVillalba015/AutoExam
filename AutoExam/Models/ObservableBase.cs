using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoExam.Models;

/// <summary>Base minima de notificacion de cambios para los modelos que se enlazan a la UI.</summary>
public abstract class ObservableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? nombre = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));

    protected bool Set<T>(ref T campo, T valor, [CallerMemberName] string? nombre = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
        {
            return false;
        }

        campo = valor;
        OnPropertyChanged(nombre);
        return true;
    }
}
