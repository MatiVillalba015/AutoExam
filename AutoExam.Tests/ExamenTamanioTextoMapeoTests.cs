using AutoExam.Services;
using AutoExam.Tests.Infraestructura;
using AutoExam.ViewModels;

namespace AutoExam.Tests;

/// <summary>
/// US-005 — mapeo nivel -&gt; puntos y persistencia inmediata (specs/03-architecture.md §4.5).
///
/// Doble de <see cref="IDialogos"/>/<see cref="INavegacion"/> minimo, solo para poder instanciar
/// <see cref="ExamenViewModel"/> sin levantar ninguna ventana real (mismo motivo por el que esas
/// dos interfaces existen, ver comentario en <c>DialogoService.IDialogos</c>).
///
/// Comparte <see cref="RutasAisladasCollection"/>: <c>SesionUsuarioService.GuardarConfig()</c>
/// (usado abajo) escribe contra <c>RutasApp.Raiz</c>, un static mutable de todo el proceso — sin
/// esta colección, estos tests corrían en paralelo contra la raíz real del usuario (o contra la
/// de otra clase que la hubiera redirigido en simultáneo), con choques intermitentes de archivo
/// ("config.json.tmp ... being used by another process").
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class ExamenTamanioTextoMapeoTests
{
    public const int NivelMinimo = 0;
    public const int NivelMaximo = 4;
    public const int NivelPorDefecto = 2;

    private static ExamenViewModel CrearViewModel(SesionUsuarioService? sesion = null) =>
        new(sesion ?? new SesionUsuarioService(), new DialogosFalsos(), new NavegacionFalsa());

    [Fact]
    public void El_nivel_por_defecto_es_2_y_reproduce_el_tamanio_actual()
    {
        var vm = CrearViewModel();

        Assert.Equal(NivelPorDefecto, vm.NivelTextoExamen);
        // specs/03-architecture.md §4.5: "2 debe mapear a los tamaños actuales
        // — 17pt pregunta / 14pt opciones — para no romper el look por defecto".
        Assert.Equal(17.0, vm.TamanioTextoPregunta);
        Assert.Equal(14.0, vm.TamanioTextoOpciones);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Cada_uno_de_los_5_niveles_da_un_tamanio_de_pregunta_y_opciones_positivo(int nivel)
    {
        var vm = CrearViewModel();
        vm.NivelTextoExamen = nivel;

        Assert.True(vm.TamanioTextoPregunta > 0);
        Assert.True(vm.TamanioTextoOpciones > 0);
    }

    [Fact]
    public void La_tabla_de_pregunta_es_estrictamente_creciente_entre_los_5_niveles()
    {
        // Si dos niveles consecutivos dieran el mismo pt, "ajustar el tamaño" no tendría
        // efecto perceptible en ese salto — no es obvio desde el criterio de aceptación en
        // si, pero rompe el propósito de la funcionalidad si pasa.
        var vm = CrearViewModel();

        double anterior = double.MinValue;
        for (int nivel = NivelMinimo; nivel <= NivelMaximo; nivel++)
        {
            vm.NivelTextoExamen = nivel;
            Assert.True(vm.TamanioTextoPregunta > anterior,
                $"Nivel {nivel} ({vm.TamanioTextoPregunta}pt) deberia ser mayor que el anterior ({anterior}pt).");
            anterior = vm.TamanioTextoPregunta;
        }
    }

    [Fact]
    public void La_tabla_de_opciones_es_estrictamente_creciente_entre_los_5_niveles()
    {
        var vm = CrearViewModel();

        double anterior = double.MinValue;
        for (int nivel = NivelMinimo; nivel <= NivelMaximo; nivel++)
        {
            vm.NivelTextoExamen = nivel;
            Assert.True(vm.TamanioTextoOpciones > anterior,
                $"Nivel {nivel} ({vm.TamanioTextoOpciones}pt) deberia ser mayor que el anterior ({anterior}pt).");
            anterior = vm.TamanioTextoOpciones;
        }
    }

    [Fact]
    public void Cambiar_el_nivel_persiste_de_inmediato_en_AppConfig_sin_esperar_a_otra_accion()
    {
        // ExamenViewModel.OnNivelTextoExamenChanged escribe en Config y llama GuardarConfig()
        // en cada cambio (auto-guardado), no solo al cerrar el examen o la app.
        var sesion = new SesionUsuarioService();
        var vm = CrearViewModel(sesion);

        vm.NivelTextoExamen = NivelMaximo;

        Assert.Equal(NivelMaximo, sesion.Config.TamanioTextoExamen);
    }

    [Fact]
    public void No_se_puede_aumentar_mas_alla_del_nivel_maximo()
    {
        var vm = CrearViewModel();
        vm.NivelTextoExamen = NivelMaximo;

        Assert.False(vm.AumentarTextoExamenCommand.CanExecute(null));
    }

    [Fact]
    public void No_se_puede_disminuir_mas_alla_del_nivel_minimo()
    {
        var vm = CrearViewModel();
        vm.NivelTextoExamen = NivelMinimo;

        Assert.False(vm.DisminuirTextoExamenCommand.CanExecute(null));
    }

    [Fact]
    public void CargarDesdeConfig_trae_el_nivel_guardado_y_lo_clampea_si_esta_fuera_de_rango()
    {
        var sesion = new SesionUsuarioService();
        sesion.Config.TamanioTextoExamen = 99; // valor invalido, como si viniera de un JSON tocado a mano
        var vm = CrearViewModel(sesion);

        vm.CargarDesdeConfig();

        Assert.Equal(NivelMaximo, vm.NivelTextoExamen);
    }

    private sealed class DialogosFalsos : IDialogos
    {
        public bool Confirmar(string mensaje, string titulo = "AutoExam") => true;
        public void Aviso(string titulo, string mensaje) { }
        public void Error(string titulo, string mensaje) { }
        public string? ElegirPdf() => null;
        public void AbrirCarpeta(string ruta) { }
    }

    private sealed class NavegacionFalsa : INavegacion
    {
        public void IrA(string clave) { }
        public void Estado(string texto) { }
        public void RefrescarEstadoApi() { }
    }
}
