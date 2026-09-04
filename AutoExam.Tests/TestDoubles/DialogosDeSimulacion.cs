using System.Collections.Generic;
using AutoExam.Services;

namespace AutoExam.Tests.TestDoubles;

/// <summary>
/// Test double de <see cref="IDialogos"/> para usar en cualquier suite (ViewModel, servicio)
/// que necesite un diálogo sin levantar una ventana real. Registra cada invocación para poder
/// asertarla después — en particular, sirve para AC-T7 (specs/02-tech-spec.md): probar que las
/// acciones irreversibles (salir de examen sin terminar, borrar historial, quitar un libro)
/// efectivamente pasan por <see cref="Confirmar"/> antes de ejecutarse.
///
/// Vive en AutoExam.Tests (no en AutoExam) a propósito: es infraestructura de test, no del
/// proyecto principal.
/// </summary>
public class DialogosDeSimulacion : IDialogos
{
    /// <summary>Valor que devuelve <see cref="Confirmar"/> en la próxima llamada.</summary>
    public bool RespuestaConfirmar { get; set; } = true;

    /// <summary>Valor que devuelve <see cref="ElegirFuentes"/> (null = el usuario cancelo).</summary>
    public string[]? RutasFuentesAElegir { get; set; }

    /// <summary>Valor que devuelve <see cref="ElegirExamenCompartido"/> (US-037).</summary>
    public string? RutaExamenAImportar { get; set; }

    /// <summary>Valor que devuelve <see cref="ElegirDondeGuardarExamen"/> (US-037).</summary>
    public string? RutaDondeExportar { get; set; }

    public List<string> NombresSugeridosParaExportar { get; } = new();

    public int LlamadasConfirmar { get; private set; }
    public int LlamadasAviso { get; private set; }
    public int LlamadasError { get; private set; }
    public int LlamadasAbrirCarpeta { get; private set; }

    public List<(string Mensaje, string Titulo)> ConfirmacionesPedidas { get; } = new();
    public List<(string Titulo, string Mensaje)> AvisosMostrados { get; } = new();
    public List<(string Titulo, string Mensaje)> ErroresMostrados { get; } = new();
    public List<string> CarpetasAbiertas { get; } = new();

    public bool Confirmar(string mensaje, string titulo = "AutoExam")
    {
        LlamadasConfirmar++;
        ConfirmacionesPedidas.Add((mensaje, titulo));
        return RespuestaConfirmar;
    }

    public void Aviso(string titulo, string mensaje)
    {
        LlamadasAviso++;
        AvisosMostrados.Add((titulo, mensaje));
    }

    public void Error(string titulo, string mensaje)
    {
        LlamadasError++;
        ErroresMostrados.Add((titulo, mensaje));
    }

    public string[]? ElegirFuentes() => RutasFuentesAElegir;

    public string? ElegirExamenCompartido() => RutaExamenAImportar;

    public string? ElegirDondeGuardarExamen(string nombreSugerido)
    {
        NombresSugeridosParaExportar.Add(nombreSugerido);
        return RutaDondeExportar;
    }

    public void AbrirCarpeta(string ruta)
    {
        LlamadasAbrirCarpeta++;
        CarpetasAbiertas.Add(ruta);
    }
}
