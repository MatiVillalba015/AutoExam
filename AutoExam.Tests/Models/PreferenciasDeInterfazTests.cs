using AutoExam.Models;

namespace AutoExam.Tests.Models;

/// <summary>
/// US-047 / US-048 / US-049 — las preferencias de interfaz como datos, sin pantalla de por
/// medio: qué se resetea, qué NO se resetea, y los límites del zoom y de la paleta.
/// </summary>
public class PreferenciasDeInterfazTests
{
    // ------------------------------------------------------------------
    // US-047 / RN-55 — restaurar valores de fábrica
    // ------------------------------------------------------------------

    private static AppConfig ConfigConTodoCambiado()
    {
        var config = new AppConfig
        {
            TemaDeColor = "verde",
            TemaOscuro = false,
            Zoom = 1.4,
            Notificaciones = false,
            ReducirMovimiento = true,
            VentanaAncho = 1600,
            VentanaAlto = 900,
            VentanaX = 40,
            VentanaY = 60,
            VentanaEstado = System.Windows.WindowState.Maximized,
            Modelo = "gemini-3.7-flash",
            PreguntasPorLote = 9,
        };

        config.EstablecerClaves("AQ.clave-uno,AQ.clave-dos");

        return config;
    }

    [Fact]
    public void RestaurarValoresDeFabrica_DevuelveLasPreferenciasDeApariencia_US047()
    {
        var config = ConfigConTodoCambiado();
        var fabrica = new AppConfig();

        config.RestaurarValoresDeFabrica();

        Assert.Equal(fabrica.TemaDeColor, config.TemaDeColor);
        Assert.Equal(fabrica.TemaOscuro, config.TemaOscuro);
        Assert.Equal(fabrica.Zoom, config.Zoom);
        Assert.Equal(fabrica.Notificaciones, config.Notificaciones);
        Assert.Equal(fabrica.ReducirMovimiento, config.ReducirMovimiento);
    }

    /// <summary>
    /// RN-63: el tamaño y la posición de ventana son una preferencia de interfaz más, aunque no
    /// tengan control visible, así que se resetean junto con el resto.
    /// </summary>
    [Fact]
    public void RestaurarValoresDeFabrica_TambienOlvidaLaGeometriaDeVentana_RN63()
    {
        var config = ConfigConTodoCambiado();

        config.RestaurarValoresDeFabrica();

        Assert.False(GeometriaGuardada(config),
            "Después de restaurar, la ventana tiene que volver al centrado por defecto (RN-63).");
    }

    private static bool GeometriaGuardada(AppConfig config) =>
        AutoExam.Services.GeometriaVentanaService.HayGeometriaGuardada(config.VentanaAncho, config.VentanaAlto);

    /// <summary>
    /// El criterio central de US-047, y el que hace que la acción sea usable sin miedo: esto
    /// resetea preferencias, nunca contenido ni credenciales (RN-55).
    /// </summary>
    [Fact]
    public void RestaurarValoresDeFabrica_NoTocaLasClavesNiElModelo_RN55()
    {
        var config = ConfigConTodoCambiado();

        config.RestaurarValoresDeFabrica();

        Assert.Equal(new[] { "AQ.clave-uno", "AQ.clave-dos" }, config.ClavesDisponibles);
        Assert.Equal("gemini-3.7-flash", config.Modelo);
    }

    [Fact]
    public void RestaurarValoresDeFabrica_NoTocaLasPerillasDeGeneracion_RN55()
    {
        var config = ConfigConTodoCambiado();

        config.RestaurarValoresDeFabrica();

        // "Preguntas por lote" es de generación, no de apariencia: US-056 dice explícitamente
        // que ninguna historia nueva cambia su comportamiento.
        Assert.Equal(9, config.PreguntasPorLote);
    }

    // ------------------------------------------------------------------
    // US-049 / RN-57 — paleta cerrada
    // ------------------------------------------------------------------

    [Fact]
    public void HayVariosTemas_YElVioletaActualEsElPorDefecto_US049()
    {
        Assert.True(PaletaDeApp.Temas.Count >= 3,
            "El criterio pide el violeta actual y al menos una o dos alternativas más.");

        Assert.Equal(PaletaDeApp.PorDefecto, PaletaDeApp.Temas[0].Clave);
        Assert.Equal("Violeta", PaletaDeApp.Temas[0].Nombre);
        Assert.Equal(PaletaDeApp.PorDefecto, new AppConfig().TemaDeColor);
    }

    [Fact]
    public void CadaTema_TieneNombreYClavePropios_US049()
    {
        Assert.Equal(PaletaDeApp.Temas.Count,
            PaletaDeApp.Temas.Select(t => t.Clave).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.All(PaletaDeApp.Temas, t => Assert.False(string.IsNullOrWhiteSpace(t.Nombre)));
    }

    /// <summary>
    /// Un tema con acento propio tiene que traer los cinco colores: si faltara uno, la app
    /// quedaría con la mitad de los acentos en violeta y la otra mitad en el color elegido.
    /// </summary>
    [Fact]
    public void UnTemaConAcentoPropio_TraeTodosSusColores_US049()
    {
        foreach (var tema in PaletaDeApp.Temas.Where(t => !t.UsaElAcentoDeLosTokens))
        {
            Assert.All(
                new[] { tema.Marca, tema.MarcaFuerte, tema.MarcaSuave, tema.SobreMarca, tema.IconoDesde, tema.IconoHasta },
                color => Assert.Matches("^#[0-9A-Fa-f]{6}$", color));
        }
    }

    [Theory]
    [InlineData("azul", "azul")]
    [InlineData("AZUL", "azul")]
    [InlineData("inventado", PaletaDeApp.PorDefecto)]
    [InlineData("", PaletaDeApp.PorDefecto)]
    [InlineData(null, PaletaDeApp.PorDefecto)]
    public void Resolver_CaeEnElVioleta_AnteUnaClaveDesconocida(string? clave, string esperada)
    {
        // Un config.json editado a mano, o el de una versión futura, no puede dejar la app sin
        // paleta.
        Assert.Equal(esperada, PaletaDeApp.Resolver(clave).Clave);
    }

    [Fact]
    public void DesdeTemaOscuro_MigraLaEleccionAnteriorDeClaroUOscuro_US049()
    {
        // Antes de US-049 lo único elegible era claro u oscuro. Un config.json viejo tiene que
        // abrir con el mismo fondo que tenía.
        Assert.True(PaletaDeApp.Resolver(PaletaDeApp.DesdeTemaOscuro(true)).Oscuro);
        Assert.False(PaletaDeApp.Resolver(PaletaDeApp.DesdeTemaOscuro(false)).Oscuro);
    }

    /// <summary>
    /// El caso que hace falta cubrir de verdad: un config.json anterior a US-049 no trae
    /// <c>TemaDeColor</c>, así que al deserializar queda con el default (violeta, que es
    /// oscuro). Si esa persona venía usando el tema claro, abriría en oscuro sin haber tocado
    /// nada. La contradicción entre el tema resuelto y el campo viejo es lo que lo delata.
    /// </summary>
    [Fact]
    public void UnConfigViejoConTemaClaro_SeReconocePorLaContradiccion_US049()
    {
        var viejo = new AppConfig { TemaOscuro = false };

        Assert.Equal(PaletaDeApp.PorDefecto, viejo.TemaDeColor);
        Assert.True(PaletaDeApp.Resolver(viejo.TemaDeColor).Oscuro);

        // Oscuro por el tema y claro por el campo viejo: manda el campo viejo, que es lo que el
        // usuario efectivamente eligió.
        Assert.False(PaletaDeApp.Resolver(PaletaDeApp.DesdeTemaOscuro(viejo.TemaOscuro)).Oscuro);
    }

    // ------------------------------------------------------------------
    // US-048 — límites del zoom
    // ------------------------------------------------------------------

    [Fact]
    public void ElZoom_ArrancaEnCienPorCiento()
    {
        Assert.Equal(1.0, ZoomDeLaApp.Normal);
        Assert.Equal("100%", ZoomDeLaApp.ComoPorcentaje(ZoomDeLaApp.Normal));
        Assert.Equal(ZoomDeLaApp.Normal, new AppConfig().Zoom);
    }

    [Fact]
    public void ElZoom_NoSaleDeSusLimites_US048()
    {
        Assert.Equal(ZoomDeLaApp.Maximo, ZoomDeLaApp.Acotar(99));
        Assert.Equal(ZoomDeLaApp.Minimo, ZoomDeLaApp.Acotar(0.1));
        Assert.Equal(ZoomDeLaApp.Normal, ZoomDeLaApp.Acotar(double.NaN));
    }

    [Fact]
    public void EnElLimite_ElControlSeApaga_EnVezDeSeguirAchicando_US048()
    {
        Assert.False(ZoomDeLaApp.PuedeReducir(ZoomDeLaApp.Minimo));
        Assert.False(ZoomDeLaApp.PuedeAumentar(ZoomDeLaApp.Maximo));

        Assert.True(ZoomDeLaApp.PuedeReducir(ZoomDeLaApp.Normal));
        Assert.True(ZoomDeLaApp.PuedeAumentar(ZoomDeLaApp.Normal));
    }

    [Fact]
    public void AumentarYReducir_SeMuevenDeAUnPaso_YVuelvenAlMismoLugar()
    {
        double ida = ZoomDeLaApp.Aumentar(ZoomDeLaApp.Normal);

        Assert.Equal(ZoomDeLaApp.Normal + ZoomDeLaApp.Paso, ida, 3);
        Assert.Equal(ZoomDeLaApp.Normal, ZoomDeLaApp.Reducir(ida), 3);
    }

    [Fact]
    public void ElPorcentaje_SeMuestraSiempreConSuSignoYSinDecimales_US048()
    {
        // El criterio pide "el porcentaje actual siempre visible (por ejemplo 100%)".
        Assert.Equal("80%", ZoomDeLaApp.ComoPorcentaje(ZoomDeLaApp.Minimo));
        Assert.Equal("150%", ZoomDeLaApp.ComoPorcentaje(ZoomDeLaApp.Maximo));
        Assert.Equal("110%", ZoomDeLaApp.ComoPorcentaje(1.1));
    }

    [Fact]
    public void UnValorIntermedioDelSlider_SeRedondeaAlPaso()
    {
        // El slider entrega cualquier valor; el porcentaje no puede terminar en "103%".
        Assert.Equal(1.0, ZoomDeLaApp.Acotar(1.03), 3);
        Assert.Equal(1.1, ZoomDeLaApp.Acotar(1.07), 3);
    }

    // ------------------------------------------------------------------
    // US-050 / US-054 — defaults
    // ------------------------------------------------------------------

    [Fact]
    public void LasNotificacionesVienenEncendidas_YReducirMovimientoApagado()
    {
        var fabrica = new AppConfig();

        Assert.True(fabrica.Notificaciones, "US-050: los avisos son el comportamiento de siempre.");
        Assert.False(fabrica.ReducirMovimiento, "US-054: el toggle propio arranca en no.");
    }
}
