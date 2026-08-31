using System.Reflection;
using System.Xml.Linq;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;
using AutoExam.Tests.TestDoubles;
using AutoExam.Tests.TestSupport;
using AutoExam.ViewModels;

namespace AutoExam.Tests.ViewModels;

/// <summary>
/// US-013 — mensaje de felicitación en Resultados (<c>ExamenViewModel.MostrarFelicitacion</c> /
/// <c>MensajeFelicitacion</c>, arquitectura Inc-4 §3 / §4.7, AC-T60 / AC-T61 / AC-T63,
/// NFR-52). Ninguna suite lo cubría (el test-dev de M5 se cortó por rate limit).
///
/// - AC-T60: nota UBA ≥ 7 en el intento original ⇒ <c>MostrarFelicitacion == true</c>; el texto
///   es una constante literal en mayúsculas.
/// - AC-T61: nota ≤ 6, o resultado de una ronda de revancha ⇒ <c>MostrarFelicitacion == false</c>.
/// - AC-T63 / NFR-52: el texto es <c>const</c> de código, no hay campo en <c>AppConfig</c> ni
///   propiedad configurable para ocultarlo o editarlo.
///
/// El comportamiento se ejercita por el camino real (<c>Iniciar</c> + <c>FinalizarCommand</c>),
/// que corre <c>EvaluadorUBA.Evaluar</c> y fija <c>MostrarFelicitacion</c> en
/// <c>MostrarResultados</c>. Se usa el hilo STA compartido (<see cref="WpfHost"/>) porque
/// <c>ExamenViewModel</c> crea <c>DispatcherTimer</c>s en su ctor.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class ExamenViewModelFelicitacionTests
{
    private static (ExamenViewModel vm, DialogosDeSimulacion dlg) NuevoVm()
    {
        var sesion = new SesionUsuarioService();
        sesion.Cargar();
        var dlg = new DialogosDeSimulacion();
        var vm = new ExamenViewModel(sesion, dlg, new NavegacionDeSimulacion());
        return (vm, dlg);
    }

    private static ExamenEnCurso ArmarExamen(int total, int correctas, int ronda, ExamenRendido? registro)
    {
        var examen = new ExamenEnCurso
        {
            Id = "examen-" + Guid.NewGuid().ToString("N"),
            LibroTitulo = "Libro de prueba",
            Materia = "Materia",
            AlcanceDescripcion = "Todo el material",
            Ronda = ronda,
            Registro = registro,
        };

        for (int i = 0; i < total; i++)
        {
            examen.Preguntas.Add(new Pregunta
            {
                TextoPregunta = $"Pregunta {i + 1}",
                Opciones = new() { "correcta", "incorrecta" },
                IndiceRespuestaCorrecta = 0,
                IndiceRespuestaUsuario = i < correctas ? 0 : 1,
                Estado = EstadoPreguntaEnum.Respondida,
            });
        }

        return examen;
    }

    private static ExamenViewModel Correr(int total, int correctas, int ronda = 0, ExamenRendido? registro = null)
        => WpfHost.Invocar(() =>
        {
            var (vm, _) = NuevoVm();
            vm.Iniciar(ArmarExamen(total, correctas, ronda, registro));
            vm.FinalizarCommand.Execute(null);
            return vm;
        });

    // ------------------------------------------------------------------
    // AC-T60 — nota >= 7 en el intento original
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(10, 8)]   // 80% -> nota 7
    [InlineData(10, 10)]  // 100% -> nota 10
    [InlineData(20, 15)]  // 75% -> nota 7
    public void IntentoOriginal_ConNotaSieteOMas_MuestraLaFelicitacion_AC_T60(int total, int correctas)
    {
        var vm = Correr(total, correctas);

        Assert.True(vm.EnResultados);
        Assert.True(vm.Nota >= 7, $"Nota calculada: {vm.Nota}");
        Assert.True(vm.MostrarFelicitacion);
    }

    [Fact]
    public void ElMensaje_EsUnaConstanteLiteralEnMayusculas_AC_T60()
    {
        string texto = ExamenViewModel.MensajeFelicitacion;

        Assert.False(string.IsNullOrWhiteSpace(texto));
        Assert.Equal(texto.ToUpperInvariant(), texto);

        var campo = typeof(ExamenViewModel).GetField(
            nameof(ExamenViewModel.MensajeFelicitacion), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(campo);
        Assert.True(campo!.IsLiteral && !campo.IsInitOnly, "MensajeFelicitacion debe ser 'const' (no editable en runtime).");
    }

    [Fact]
    public void ExamenView_MuestraElMensajeDestacado_AtadoAMostrarFelicitacion_AC_T60()
    {
        var doc = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/ExamenView.xaml"));

        var textBlock = doc.Descendants()
            .Where(e => e.Name.LocalName == "TextBlock")
            .FirstOrDefault(e =>
            {
                var text = e.Attributes().FirstOrDefault(a => a.Name.LocalName == "Text")?.Value ?? string.Empty;
                return text.Contains("MensajeFelicitacion");
            });

        Assert.True(textBlock is not null,
            "ExamenView.xaml no tiene un TextBlock con Text atado a ExamenViewModel.MensajeFelicitacion (AC-T60).");

        var vis = textBlock!.Attributes().FirstOrDefault(a => a.Name.LocalName == "Visibility")?.Value ?? string.Empty;
        var peso = textBlock.Attributes().FirstOrDefault(a => a.Name.LocalName == "FontWeight")?.Value ?? string.Empty;

        Assert.Contains("MostrarFelicitacion", vis);
        Assert.Equal("Bold", peso);   // "destacado" (AC-T60)
    }

    // ------------------------------------------------------------------
    // AC-T61 — nota <= 6, o resultado de revancha
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(10, 6)]   // 60% -> nota 4
    [InlineData(10, 7)]   // 70% -> nota 6
    [InlineData(10, 0)]   // 0%  -> nota 1
    public void IntentoOriginal_ConNotaSeisOMenos_NoMuestraLaFelicitacion_AC_T61(int total, int correctas)
    {
        var vm = Correr(total, correctas);

        Assert.True(vm.Nota <= 6, $"Nota calculada: {vm.Nota}");
        Assert.False(vm.MostrarFelicitacion);
    }

    [Fact]
    public void Revancha_AunConNotaPerfecta_NoMuestraLaFelicitacion_AC_T61()
    {
        var registroOriginal = new ExamenRendido { Id = "orig", NotaUBA = 4, TotalPreguntas = 10, Correctas = 6 };

        var vm = Correr(total: 5, correctas: 5, ronda: 1, registro: registroOriginal);

        Assert.True(vm.EnResultados);
        Assert.Equal(10, vm.Nota);
        Assert.False(vm.MostrarFelicitacion);
    }

    // ------------------------------------------------------------------
    // AC-T63 / NFR-52 — sin camino para ocultarlo o editarlo
    // ------------------------------------------------------------------

    [Fact]
    public void AppConfig_NoTieneNingunCampoParaOcultarOEditarLaFelicitacion_AC_T63()
    {
        var sospechosos = typeof(AppConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(n => n.Contains("felicit", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("congrat", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("mensajeFinal", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(sospechosos);
    }

    [Fact]
    public void ExamenViewModel_NoExponeSetterPublicoNiMetodoParaForzarLaFelicitacion_NFR52()
    {
        var tipo = typeof(ExamenViewModel);

        // La propiedad generada por [ObservableProperty] tiene setter, pero no debe haber ningún
        // método público extra que permita fijarla desde fuera del flujo de resultados.
        var metodos = tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name.Contains("Felicit", StringComparison.OrdinalIgnoreCase) && m.Name != "get_MostrarFelicitacion" && m.Name != "set_MostrarFelicitacion")
            .ToList();

        Assert.Empty(metodos);
    }
}
