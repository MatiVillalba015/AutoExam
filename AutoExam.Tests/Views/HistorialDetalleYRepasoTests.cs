using System.IO;
using System.Reflection;
using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;
using AutoExam.ViewModels;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-025 y US-026 en la interfaz: entrar al detalle de un examen del historial, y armar un
/// repaso combinando varios.
///
/// Ademas de la estructura, estos tests verifican por reflexion que cada propiedad y comando
/// que enlaza la vista existe de verdad. Un Binding a un nombre mal escrito no rompe la
/// compilacion ni tira excepcion: WPF lo anota en la traza de depuracion y deja el control
/// mudo — aca eso significa un boton de "armar repaso" que nunca se habilita, sin un solo
/// error visible.
/// </summary>
public class HistorialDetalleYRepasoTests
{
    private static XDocument Vista() =>
        XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/HistorialView.xaml"));

    private static string Fuente() =>
        File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/HistorialView.xaml"));

    private static IEnumerable<string> Comandos() => Vista().Descendants()
        .Select(e => e.Attribute("Command")?.Value ?? string.Empty);

    // ------------------------------------------------------------------
    // US-025 — el detalle
    // ------------------------------------------------------------------

    [Fact]
    public void CadaExamenDeLaLista_TieneUnaAccionParaVerSuDetalle()
    {
        Assert.Contains(Comandos(), c => c.Contains("VerDetalleCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void ElDetalle_UsaLaMismaTarjetaQueLaPantallaDeCorreccion()
    {
        // El criterio lo pide literalmente ("igual que en la pantalla de correccion"). Con
        // dos copias del XAML esa igualdad dura hasta el primer retoque.
        Assert.Contains("TarjetaCorreccion", Fuente(), StringComparison.Ordinal);
    }

    [Fact]
    public void ElPanelDelDetalle_SeMuestraSoloCuandoHayUnExamenAbierto()
    {
        bool hay = Vista().Descendants()
            .Any(e => (e.Attribute("Visibility")?.Value ?? string.Empty).Contains("HayDetalleAbierto"));

        Assert.True(hay, "El panel de detalle no esta atado a HayDetalleAbierto.");
    }

    [Fact]
    public void HayUnaAccionParaVolverAlHistorial()
    {
        Assert.Contains(Comandos(), c => c.Contains("CerrarDetalleCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void UnExamenSinDetalle_MuestraUnAvisoYNoUnaListaVacia_RN26()
    {
        // RN-26 es explicita: nunca una lista vacia sin explicacion ni un error. Una pantalla
        // en blanco se lee como una falla de la app, no como "este examen es viejo".
        var aviso = Vista().Descendants()
            .FirstOrDefault(e => e.Name.LocalName.Contains("InfoBar", StringComparison.Ordinal) &&
                                 (e.Attribute("Message")?.Value ?? string.Empty).Contains("AvisoSinDetalle"));

        Assert.True(aviso is not null,
            "No hay un aviso para los examenes sin detalle guardado (RN-26).");

        // Y en ese caso la lista de preguntas no se muestra: si no, quedaria el aviso encima
        // de un vacio que igual parece roto.
        bool listaEscondida = Vista().Descendants()
            .Any(e => e.Name.LocalName == "ScrollViewer" &&
                      (e.Attribute("Visibility")?.Value ?? string.Empty).Contains("HayAvisoSinDetalle"));

        Assert.True(listaEscondida, "La lista de preguntas no se esconde cuando no hay detalle.");
    }

    // ------------------------------------------------------------------
    // US-026 — el repaso combinado
    // ------------------------------------------------------------------

    [Fact]
    public void CadaExamenDeLaLista_SePuedeTildarParaElRepaso()
    {
        bool hayCasilla = Vista().Descendants()
            .Any(e => e.Name.LocalName == "CheckBox" &&
                      (e.Attribute("IsChecked")?.Value ?? string.Empty).Contains("Seleccionado"));

        Assert.True(hayCasilla, "No hay casillas para elegir examenes: US-026 pide tildar dos o mas.");
    }

    [Fact]
    public void LaCasilla_SoloApareceEnLosExamenesQuePuedenAportarPreguntas()
    {
        // Un examen de antes de US-025 no guardo el detalle, y un repaso no alimenta otro
        // repaso. Dejar la casilla visible pero muerta seria peor que no mostrarla.
        var casilla = Vista().Descendants()
            .First(e => e.Name.LocalName == "CheckBox" &&
                        (e.Attribute("IsChecked")?.Value ?? string.Empty).Contains("Seleccionado"));

        Assert.Contains("PuedeAlimentarRepaso",
            casilla.Attribute("Visibility")?.Value ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void ElHistorial_EsUnAtajoAlAsistente_NoUnSegundoFormulario_RN29()
    {
        // RN-29: "el punto de entrada principal es el asistente de Nuevo examen... si además
        // se ofrece un acceso alternativo desde Historial, es un atajo al mismo flujo, no una
        // pantalla distinta".
        //
        // El primer intento tenía acá un formulario completo —presets de cantidad y botón de
        // generar— que duplicaba el del asistente. Dos copias de la misma lógica se separan
        // al primer retoque, y el alumno terminaría viendo dos formularios parecidos que se
        // comportan distinto. Acá se tilda y se salta; el armado vive en un solo lugar.
        string xaml = Fuente();

        Assert.DoesNotContain("PresetCantidadRepaso", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("CantidadRepasoPersonalizada", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void HayUnAtajoQueLlevaAlAsistenteConLoTildado()
    {
        Assert.Contains(Comandos(), c => c.Contains("IrAlRepasoCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void ElAtajo_LlevaAlAsistenteEnModoRepaso_RN29()
    {
        // La selección no viaja en el evento: está marcada en los propios ExamenRendido, que
        // son las mismas instancias que las dos pantallas listan.
        string shell = File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/ViewModels/ShellViewModel.cs"));

        Assert.Contains("RepasoPedido", shell, StringComparison.Ordinal);
        Assert.Contains("EntrarEnModoRepaso", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void LaTarjetaDeRepaso_SeEscondeSiNoHayConQueArmarlo()
    {
        // Con menos de dos examenes elegibles no se puede armar nada: mejor esconderla que
        // dejarla visible e inerte.
        bool hay = Vista().Descendants()
            .Any(e => (e.Attribute("Visibility")?.Value ?? string.Empty).Contains("HayElegiblesParaRepaso"));

        Assert.True(hay, "La tarjeta de repaso no esta atada a HayElegiblesParaRepaso.");
    }

    [Fact]
    public void UnRepaso_SeDistingueEnLaLista()
    {
        // No es un intento nuevo del examen original: tiene que verse distinto para que el
        // historial siga siendo legible.
        Assert.Contains("EsRepaso", Fuente(), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Los enlaces apuntan a miembros que existen
    // ------------------------------------------------------------------

    [Theory]
    // US-025
    [InlineData("HayDetalleAbierto")]
    [InlineData("DetallePreguntas")]
    [InlineData("AvisoSinDetalle")]
    [InlineData("HayAvisoSinDetalle")]
    [InlineData("TituloDetalle")]
    [InlineData("ResumenDetalle")]
    [InlineData("VerDetalleCommand")]
    [InlineData("CerrarDetalleCommand")]
    // US-026: el Historial solo tilda y salta al asistente (RN-29).
    [InlineData("HayElegiblesParaRepaso")]
    [InlineData("ResumenRepaso")]
    [InlineData("SeleccionadosParaRepaso")]
    [InlineData("PreguntasDisponibles")]
    [InlineData("IrAlRepasoCommand")]
    [InlineData("DestildarExamenesCommand")]
    public void ElHistorial_ExponeLoQueEnlazaSuVista(string miembro)
    {
        bool existe = typeof(HistorialViewModel)
            .GetProperty(miembro, BindingFlags.Public | BindingFlags.Instance) is not null;

        Assert.True(existe,
            $"HistorialViewModel no expone \"{miembro}\", pero la vista lo enlaza: el control " +
            "quedaria mudo sin ningun error visible.");
    }

    [Theory]
    [InlineData("Seleccionado")]
    [InlineData("PuedeAlimentarRepaso")]
    [InlineData("EsRepaso")]
    [InlineData("EtiquetaTipo")]
    [InlineData("TieneDetalle")]
    public void ElExamenRendido_ExponeLoQueEnlazaLaLista(string miembro)
    {
        bool existe = typeof(AutoExam.Models.ExamenRendido)
            .GetProperty(miembro, BindingFlags.Public | BindingFlags.Instance) is not null;

        Assert.True(existe, $"ExamenRendido no expone \"{miembro}\".");
    }

    [Fact]
    public void DestildarTodos_LlegaALasCasillas()
    {
        // "Destildar todos" lo cambia desde codigo, y para que la casilla se entere el modelo
        // tiene que notificar. Sin esto el estado interno se limpia pero los tildes quedan
        // dibujados, que es la peor combinacion posible.
        Assert.True(
            typeof(System.ComponentModel.INotifyPropertyChanged)
                .IsAssignableFrom(typeof(AutoExam.Models.ExamenRendido)),
            "ExamenRendido no notifica cambios: al destildar todos, las casillas quedarian marcadas.");
    }
}
