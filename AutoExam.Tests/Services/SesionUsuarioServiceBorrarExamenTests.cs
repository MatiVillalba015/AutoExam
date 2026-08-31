using System;
using System.IO;
using System.Linq;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-012 — <c>SesionUsuarioService.BorrarExamen(string id)</c> (specs/03-architecture.md §4.6,
/// specs/02-tech-spec.md "US-012 — Borrado individual del historial", AC-T57 / AC-T58 / NFR-49).
///
/// Contract-first: este archivo referencia <c>BorrarExamen</c> directamente tal como lo fija el
/// contrato (<c>Perfil.Historial.RemoveAll(e =&gt; e.Id == id)</c> -&gt; <c>GuardarPerfil()</c> -&gt;
/// <c>RefrescarHistorial()</c>), aunque el metodo todavia no exista al escribirlo — el build del
/// proyecto es el gate que confirma que M5 implemento el contrato exacto (mismo criterio que
/// <c>DiagnosticoGeneracionTests</c>, Incremento 3).
///
/// Nivel: servicio / persistencia. La recalculacion de estadisticas se prueba aca (no en el
/// ViewModel) porque las 6+ propiedades de <see cref="PerfilUsuario"/> son getters calculados
/// sobre <c>Historial</c> — el unico punto donde "borrar recalcula" puede romperse es que
/// <c>BorrarExamen</c> no quite la entrada de <c>Perfil.Historial</c> o no persista.
///
/// Comparte <see cref="RutasAisladasCollection"/> y ademas redirige <c>RutasApp.Raiz</c> a una
/// carpeta propia por test: <c>GuardarPerfil()</c> escribe <c>perfil.json</c> bajo esa raiz
/// (static mutable de todo el proceso).
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class SesionUsuarioServiceBorrarExamenTests
{
    [Fact]
    public void BorrarExamen_QuitaSoloEseExamen_YDejaElResto()
    {
        ConRaizAislada(sesion =>
        {
            var a = Examen("a", nota: 8, porcentaje: 82, aprobado: true);
            var b = Examen("b", nota: 4, porcentaje: 61, aprobado: true);
            var c = Examen("c", nota: 2, porcentaje: 30, aprobado: false);
            SembrarHistorial(sesion, a, b, c);

            sesion.BorrarExamen("b");

            Assert.Equal(new[] { "a", "c" }, sesion.Perfil.Historial.Select(e => e.Id).OrderBy(x => x));
            Assert.DoesNotContain(sesion.Historial, e => e.Id == "b");
            Assert.Contains(sesion.Historial, e => e.Id == "a");
            Assert.Contains(sesion.Historial, e => e.Id == "c");
        });
    }

    [Fact]
    public void BorrarExamen_RecalculaLasEstadisticasAgregadasSinElExamenBorrado_AC_T57()
    {
        ConRaizAislada(sesion =>
        {
            // El examen 'top' es el de mejor nota y unico aprobado: al borrarlo deben moverse
            // MejorNota, PromedioNota, PromedioAciertos, Aprobados y Aplazos.
            var top = Examen("top", nota: 10, porcentaje: 100, aprobado: true);
            var medio = Examen("medio", nota: 3, porcentaje: 45, aprobado: false);
            var bajo = Examen("bajo", nota: 1, porcentaje: 10, aprobado: false);
            SembrarHistorial(sesion, top, medio, bajo);

            sesion.BorrarExamen("top");

            var p = sesion.Perfil;
            Assert.Equal(2, p.TotalExamenes);
            Assert.Equal(3, p.MejorNota);                 // antes 10
            Assert.Equal(2.0, p.PromedioNota, precision: 3); // (3 + 1) / 2
            Assert.Equal(27.5, p.PromedioAciertos, precision: 3); // (45 + 10) / 2
            Assert.Equal(0, p.Aprobados);                 // antes 1
            Assert.Equal(2, p.Aplazos);                   // antes 2 (medio + bajo)
        });
    }

    [Fact]
    public void BorrarElUltimoExamen_DejaElHistorialVacio_AC_T58()
    {
        ConRaizAislada(sesion =>
        {
            SembrarHistorial(sesion, Examen("unico", nota: 7, porcentaje: 75, aprobado: true));

            sesion.BorrarExamen("unico");

            Assert.Empty(sesion.Perfil.Historial);
            Assert.Empty(sesion.Historial);
            Assert.Equal(0, sesion.Perfil.TotalExamenes);
            Assert.Equal(0, sesion.Perfil.MejorNota);
            Assert.Equal(0d, sesion.Perfil.PromedioNota);
        });
    }

    [Fact]
    public void BorrarExamen_ConIdInexistente_EsNoOp_SinExcepcion()
    {
        ConRaizAislada(sesion =>
        {
            SembrarHistorial(sesion, Examen("a", nota: 6, porcentaje: 70, aprobado: true));

            var ex = Record.Exception(() => sesion.BorrarExamen("no-existe"));

            Assert.Null(ex);
            Assert.Single(sesion.Perfil.Historial);
        });
    }

    [Fact]
    public void BorrarExamen_Persiste_ElExamenSigueAusenteAlReabrirLaApp_AC_T57_NFR49()
    {
        string raiz = RaizTemporal();
        string raizOriginal = RutasApp.Raiz;

        try
        {
            RutasApp.RedirigirRaiz(raiz);

            var primera = new SesionUsuarioService();
            primera.Cargar();
            SembrarHistorial(primera, Examen("a", 8, 82, true), Examen("b", 4, 61, true));
            primera.BorrarExamen("a");

            // "Reabrir la app": instancia nueva que relee perfil.json de disco.
            var segunda = new SesionUsuarioService();
            segunda.Cargar();

            Assert.DoesNotContain(segunda.Perfil.Historial, e => e.Id == "a");
            Assert.Contains(segunda.Perfil.Historial, e => e.Id == "b");
        }
        finally
        {
            RutasApp.RedirigirRaiz(raizOriginal);
            BorrarDirectorio(raiz);
        }
    }

    [Fact]
    public void BorrarHistorial_Global_SigueVaciandoTodo_SinCambios_AC_T58()
    {
        ConRaizAislada(sesion =>
        {
            SembrarHistorial(sesion, Examen("a", 8, 82, true), Examen("b", 3, 40, false));

            sesion.BorrarHistorial();

            Assert.Empty(sesion.Perfil.Historial);
            Assert.Empty(sesion.Historial);
        });
    }

    // ------------------------------------------------------------------
    // Revancha que termina con su examen original ya borrado (NFR-51 / AC-T59)
    // ------------------------------------------------------------------

    [Fact]
    public void ActualizarExamen_DeUnRegistroYaBorrado_NoLoRecrea_NiLanza_NFR51()
    {
        ConRaizAislada(sesion =>
        {
            var original = Examen("orig", nota: 8, porcentaje: 82, aprobado: true);
            SembrarHistorial(sesion, original);
            sesion.BorrarExamen("orig");

            // La ronda de revancha, al terminar, hace ActualizarExamen(registro) con el mismo Id.
            var registroConRevancha = Examen("orig", nota: 8, porcentaje: 82, aprobado: true);
            registroConRevancha.Revanchas.Add(new RondaRevancha { Numero = 1, TotalPreguntas = 5, Correctas = 5 });

            var ex = Record.Exception(() => sesion.ActualizarExamen(registroConRevancha));

            Assert.Null(ex);
            Assert.Empty(sesion.Perfil.Historial); // FindIndex == -1 -> no-op, no se recrea
        });
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static void ConRaizAislada(Action<SesionUsuarioService> prueba)
    {
        string raiz = RaizTemporal();
        string raizOriginal = RutasApp.Raiz;

        try
        {
            RutasApp.RedirigirRaiz(raiz);
            var sesion = new SesionUsuarioService();
            sesion.Cargar();
            prueba(sesion);
        }
        finally
        {
            RutasApp.RedirigirRaiz(raizOriginal);
            BorrarDirectorio(raiz);
        }
    }

    private static void SembrarHistorial(SesionUsuarioService sesion, params ExamenRendido[] examenes)
    {
        foreach (var e in examenes)
        {
            sesion.RegistrarExamen(e);
        }
    }

    private static ExamenRendido Examen(string id, int nota, double porcentaje, bool aprobado) => new()
    {
        Id = id,
        Fecha = new DateTime(2026, 1, 1).AddMinutes(id.GetHashCode() & 0xffff),
        LibroTitulo = $"Libro {id}",
        TotalPreguntas = 10,
        Correctas = (int)Math.Round(porcentaje / 10),
        PorcentajeAciertos = porcentaje,
        NotaUBA = nota,
        Aprobado = aprobado,
    };

    private static string RaizTemporal() =>
        Path.Combine(Path.GetTempPath(), "AutoExam.Tests", "M5-" + Guid.NewGuid().ToString("N"));

    private static void BorrarDirectorio(string ruta)
    {
        try
        {
            if (Directory.Exists(ruta))
            {
                Directory.Delete(ruta, recursive: true);
            }
        }
        catch
        {
            // Limpieza best-effort: no puede tumbar la corrida.
        }
    }
}
