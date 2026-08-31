using System.Collections.Generic;
using AutoExam.ViewModels;

namespace AutoExam.Tests.TestDoubles;

/// <summary>
/// Test double de <see cref="INavegacion"/> para instanciar ViewModels de pagina
/// (<see cref="ExamenViewModel"/>, <see cref="HistorialViewModel"/>...) sin levantar el shell ni
/// ninguna ventana. Registra las navegaciones y los textos de barra de estado para poder
/// asertarlos.
///
/// Vive en AutoExam.Tests (no en AutoExam) a proposito: es infraestructura de test. Se agrega
/// como doble compartido porque M5 (US-011/US-012/US-013) suma varias suites de ViewModel que lo
/// necesitan; hasta ahora cada test se armaba su propia clase privada.
/// </summary>
public sealed class NavegacionDeSimulacion : INavegacion
{
    public List<string> Destinos { get; } = new();
    public List<string> Estados { get; } = new();
    public int LlamadasRefrescarEstadoApi { get; private set; }

    public string? UltimoDestino => Destinos.Count == 0 ? null : Destinos[^1];
    public string? UltimoEstado => Estados.Count == 0 ? null : Estados[^1];

    public void IrA(string clave) => Destinos.Add(clave);

    public void Estado(string texto) => Estados.Add(texto);

    public void RefrescarEstadoApi() => LlamadasRefrescarEstadoApi++;
}
