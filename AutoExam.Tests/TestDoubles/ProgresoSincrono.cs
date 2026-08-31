using System.Collections.Generic;

namespace AutoExam.Tests.TestDoubles;

/// <summary>
/// <see cref="IProgress{T}"/> que acumula los reportes en una lista de forma sincrónica, en el
/// mismo hilo que llama a <c>Report</c>. A diferencia de <see cref="System.Progress{T}"/>, no
/// necesita un <c>SynchronizationContext</c> ni posterga la invocación al thread pool — el test
/// puede leer <see cref="Mensajes"/> apenas termina el <c>await</c> de la operación.
/// </summary>
public sealed class ProgresoSincrono : IProgress<string>
{
    public List<string> Mensajes { get; } = new();

    public void Report(string value) => Mensajes.Add(value);

    public bool Contiene(string fragmento) =>
        Mensajes.Exists(m => m.Contains(fragmento, StringComparison.OrdinalIgnoreCase));
}
