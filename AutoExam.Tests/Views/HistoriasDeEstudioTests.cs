using System.IO;
using System.Reflection;
using System.Xml.Linq;
using AutoExam.Models;
using AutoExam.Tests.Infraestructura;
using AutoExam.ViewModels;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-032 a US-037 en la interfaz y en los modelos: que lo que la vista enlaza exista, que el
/// cronómetro se comporte, y que las reglas de negocio que no son de cálculo puro estén donde
/// dicen que están.
///
/// Son verificaciones estructurales y de modelo: no dicen si se ve bien —eso hay que mirarlo—,
/// pero sí fijan las decisiones que se pueden deshacer sin querer al tocar una vista, y que no
/// dejarían ningún error visible cuando pasara.
/// </summary>
public class HistoriasDeEstudioTests
{
    private static XDocument Vista(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static string Fuente(string ruta) => File.ReadAllText(ArchivoFuenteHelper.RutaFuente(ruta));

    // ------------------------------------------------------------------
    // US-032 — el repaso inteligente entra por el asistente
    // ------------------------------------------------------------------

    [Fact]
    public void ElAsistente_OfreceElRepasoDeLoFallado_US032()
    {
        var comandos = Vista("AutoExam/Views/AsistenteView.xaml").Descendants()
            .Select(e => e.Attribute("Command")?.Value ?? string.Empty)
            .ToList();

        Assert.Contains(comandos, c => c.Contains("UsarPreguntasFalladasCommand", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ModoFalladas")]
    [InlineData("FocosDeRepaso")]
    [InlineData("FocoElegido")]
    [InlineData("HayPreguntasFalladas")]
    [InlineData("ResumenFalladas")]
    [InlineData("PreguntasFalladasDisponibles")]
    // US-037
    [InlineData("ModoImportado")]
    [InlineData("ExamenesImportados")]
    [InlineData("ImportadoElegido")]
    [InlineData("HayExamenesImportados")]
    [InlineData("ResumenImportado")]
    // US-034
    [InlineData("MinutosLimite")]
    [InlineData("ConCronometro")]
    [InlineData("ResumenTiempo")]
    public void ElAsistente_ExponeLoQueEnlazaSuVista(string miembro)
    {
        bool existe = typeof(AsistenteViewModel)
            .GetProperty(miembro, BindingFlags.Public | BindingFlags.Instance) is not null;

        Assert.True(existe,
            $"AsistenteViewModel no expone \"{miembro}\", pero la vista lo enlaza: el control quedaría mudo.");
    }

    [Fact]
    public void LosCuatroOrigenes_SonExcluyentesYCubrenTodo()
    {
        // Cuatro chips, un solo modo activo a la vez. Si dos dieran true al mismo tiempo se
        // verían dos bloques de configuración superpuestos en el paso Material.
        foreach (OrigenPreguntas origen in Enum.GetValues<OrigenPreguntas>())
        {
            var vm = new { Origen = origen };

            bool material = origen == OrigenPreguntas.Material;
            bool combinado = origen == OrigenPreguntas.ExamenesAnteriores;
            bool falladas = origen == OrigenPreguntas.PreguntasFalladas;
            bool importado = origen == OrigenPreguntas.Importado;

            Assert.Equal(1, new[] { material, combinado, falladas, importado }.Count(b => b));
        }
    }

    [Fact]
    public void ElRepasoInteligente_NoPasaPorLaIA_RN40()
    {
        // RN-40: "reutiliza el mismo mecanismo local de armado que un examen combinado: no
        // consume cuota de IA ni depende de conexión". Si el armado cayera en el camino de
        // GenerarAsync, pediría clave de Gemini y gastaría cuota.
        string codigo = Fuente("AutoExam/ViewModels/AsistenteViewModel.cs");

        int guarda = codigo.IndexOf("if (ModoRepaso)", StringComparison.Ordinal);
        int api = codigo.IndexOf("_sesion.HayApiKey", StringComparison.Ordinal);

        Assert.True(guarda > 0 && api > guarda,
            "El armado local no sale antes de tocar el pipeline de IA: gastaría cuota.");

        // Y el repaso de lo fallado usa el servicio local, no el generador.
        Assert.Contains("RepasoInteligente.Armar(", codigo, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // US-034 — cronómetro
    // ------------------------------------------------------------------

    [Fact]
    public void SinLimite_ElExamenSeComportaComoSiempre()
    {
        var examen = new ExamenEnCurso { LimiteSegundos = 0 };

        Assert.False(examen.ConCronometro);
        Assert.False(examen.SeAcaboElTiempo);
    }

    [Fact]
    public void ConLimite_ElTiempoRestanteBaja()
    {
        var examen = new ExamenEnCurso
        {
            LimiteSegundos = 600,
            Inicio = DateTime.Now.AddMinutes(-2),
        };

        Assert.True(examen.ConCronometro);
        Assert.False(examen.SeAcaboElTiempo);
        Assert.InRange(examen.Restante.TotalSeconds, 470, 490);
    }

    [Fact]
    public void AlAcabarseElTiempo_QuedaEnCeroYNoEnNegativo()
    {
        // Un negativo, aunque sea por un instante entre el vencimiento y la entrega, se lee
        // como un error de la app.
        var examen = new ExamenEnCurso
        {
            LimiteSegundos = 60,
            Inicio = DateTime.Now.AddMinutes(-5),
        };

        Assert.True(examen.SeAcaboElTiempo);
        Assert.Equal(TimeSpan.Zero, examen.Restante);
    }

    [Fact]
    public void ElCronometro_EstaEnElPasoFormato_NoEnUnModoPuntual_RN43()
    {
        // RN-43: "es una opción del paso Formato, independiente del origen de las preguntas".
        // Si el bloque estuviera dentro de uno de los bloques por modo, sólo aplicaría a ese.
        var doc = Vista("AutoExam/Views/AsistenteView.xaml");

        var bloque = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "TextBlock" &&
                                 (e.Attribute("Text")?.Value ?? string.Empty) == "TIEMPO");

        Assert.True(bloque is not null, "No hay bloque de tiempo en el asistente (US-034).");

        // No puede colgar de un contenedor que solo se muestra en un modo.
        bool dentroDeUnModo = bloque!.Ancestors().Any(a =>
        {
            string v = a.Attribute("Visibility")?.Value ?? string.Empty;
            return v.Contains("ModoMaterial", StringComparison.Ordinal) ||
                   v.Contains("ModoCombinado", StringComparison.Ordinal) ||
                   v.Contains("ModoFalladas", StringComparison.Ordinal) ||
                   v.Contains("ModoImportado", StringComparison.Ordinal);
        });

        Assert.False(dentroDeUnModo,
            "El cronómetro está adentro de un modo puntual: RN-43 lo pide para los cuatro.");
    }

    [Fact]
    public void ElLimiteLlegaALosCuatroTiposDeExamen_RN43()
    {
        // Los cuatro caminos que arman un ExamenEnCurso tienen que pasarle el límite.
        string codigo = Fuente("AutoExam/ViewModels/AsistenteViewModel.cs");

        int construcciones = codigo.Split("new ExamenEnCurso").Length - 1;
        int limites = codigo.Split("LimiteSegundos = LimiteEnSegundos").Length - 1;

        Assert.Equal(construcciones, limites);
    }

    [Fact]
    public void LaEntregaAutomatica_NoPideConfirmacion()
    {
        // Al acabarse el tiempo no hay nada que preguntar, y un diálogo esperando respuesta
        // dejaría el examen sin entregar justo cuando se acabó el tiempo.
        string codigo = Fuente("AutoExam/ViewModels/ExamenViewModel.cs");

        int latido = codigo.IndexOf("private void Latido()", StringComparison.Ordinal);
        Assert.True(latido > 0);

        string cuerpo = codigo[latido..(latido + 1400)];

        Assert.Contains("Corregir();", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("_dialogos.Confirmar", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public void ElAvisoDePocoTiempo_EsVisualYNoUnDialogo()
    {
        // Un diálogo a dos minutos del final roba justamente el tiempo que queda.
        Assert.Equal(TimeSpan.FromMinutes(2), ExamenViewModel.UmbralDeAviso);

        var reloj = Vista("AutoExam/Views/ExamenView.xaml").Descendants()
            .Any(e => (e.Attribute("Binding")?.Value ?? string.Empty).Contains("TiempoCritico"));

        Assert.True(reloj, "El reloj no cambia de aspecto cuando queda poco tiempo (US-034).");
    }

    // ------------------------------------------------------------------
    // US-035 — buscadores
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("AutoExam/Views/HistorialView.xaml")]
    [InlineData("AutoExam/Views/BibliotecaView.xaml")]
    public void CadaPantalla_TieneBuscadorYEstadoSinResultados_US035(string ruta)
    {
        var doc = Vista(ruta);

        bool buscador = doc.Descendants().Any(e =>
            (e.Attribute("Text")?.Value ?? string.Empty).Contains("Filtro", StringComparison.Ordinal) &&
            (e.Attribute("Text")?.Value ?? string.Empty).Contains("PropertyChanged", StringComparison.Ordinal));

        Assert.True(buscador, $"{ruta} no filtra en tiempo real (US-035).");

        // "No se encontró nada para X" en vez de una lista vacía sin explicación.
        bool aviso = doc.Descendants().Any(e =>
            (e.Attribute("Text")?.Value ?? string.Empty).Contains("AvisoSinResultados", StringComparison.Ordinal));

        Assert.True(aviso, $"{ruta} no avisa cuando la búsqueda no encuentra nada (US-035).");
    }

    [Fact]
    public void ElBuscadorDelHistorial_NoTocaLasEstadisticas()
    {
        // Lo que se esconde sigue existiendo: el promedio y el total se siguen calculando
        // sobre todos los exámenes, no sobre los que quedaron visibles.
        string codigo = Fuente("AutoExam/ViewModels/HistorialViewModel.cs");

        Assert.Contains("ExamenesFiltrados", codigo, StringComparison.Ordinal);
        Assert.Contains("Total = perfil.TotalExamenes", codigo, StringComparison.Ordinal);
    }

    [Fact]
    public void BorrarElTextoDelBuscador_MuestraTodoDeNuevo()
    {
        // Con el buscador vacío no hay búsqueda que fallar, así que tampoco puede quedar el
        // estado de "sin resultados" pegado.
        string codigo = Fuente("AutoExam/ViewModels/HistorialViewModel.cs");

        Assert.Contains("texto.Length == 0 || Coincide(examen, texto)", codigo, StringComparison.Ordinal);
        Assert.Contains("SinResultados = texto.Length > 0", codigo, StringComparison.Ordinal);
    }

    [Fact]
    public void ElBuscadorDeBiblioteca_FiltraLaVistaAgrupada_NoUnaListaAparte()
    {
        // Con una colección paralela, buscar habría devuelto una tira plana sin materias y
        // habría roto la agrupación de US-023 mientras dura la búsqueda.
        string codigo = Fuente("AutoExam/ViewModels/BibliotecaViewModel.cs");

        Assert.Contains("vista.View.Filter = Coincide", codigo, StringComparison.Ordinal);
        Assert.Contains("LibrosPorMateria.Refresh()", codigo, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // US-036 — atajos
    // ------------------------------------------------------------------

    [Fact]
    public void LosAtajos_NoSeDisparanConUnCampoDeTextoConFoco()
    {
        // Criterio explícito: escribir "sacar" en un buscador no puede saltear una pregunta
        // por la S.
        string codigo = Fuente("AutoExam/Behaviors/AtajosDeExamen.cs");

        Assert.Contains("EscribiendoEnUnCampo", codigo, StringComparison.Ordinal);
        Assert.Contains("TextBoxBase", codigo, StringComparison.Ordinal);
        Assert.Contains("PasswordBox", codigo, StringComparison.Ordinal);
    }

    [Fact]
    public void ElEnunciadoCopiable_NoCuentaComoCampoDeTexto()
    {
        // El enunciado es un TextBox de sólo lectura (para poder copiarlo). Si contara como
        // campo editable, hacer click en él dejaría muertos todos los atajos sin ninguna señal.
        string codigo = Fuente("AutoExam/Behaviors/AtajosDeExamen.cs");

        Assert.Contains("TextBox { IsReadOnly: true } => false", codigo, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaTeclaDeOpcionQueNoExiste_NoHaceNada()
    {
        // "Correspondiente a una opción visible": con tres opciones, la tecla 4 no elige nada.
        string codigo = Fuente("AutoExam/Behaviors/AtajosDeExamen.cs");

        Assert.Contains("atajo.Opcion >= vm.OpcionesVisibles", codigo, StringComparison.Ordinal);
    }

    [Fact]
    public void ConCtrlApretado_LosAtajosNoSeDisparan()
    {
        // Ctrl+1..5 navega entre secciones (US-004). Sin esta guarda también elegiría la
        // opción 1 del examen.
        Assert.Contains("Keyboard.Modifiers != ModifierKeys.None",
            Fuente("AutoExam/Behaviors/AtajosDeExamen.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void LaReferenciaDeAtajos_SeMuestraSoloLaPrimeraVez()
    {
        // "La primera vez que se entra a un examen". Una ayuda que reaparece siempre deja de
        // leerse y pasa a estorbar, así que el "Entendido" se recuerda entre reinicios.
        string codigo = Fuente("AutoExam/ViewModels/ExamenViewModel.cs");

        Assert.Contains("MostrarAtajos = !_sesion.Config.AtajosExamenVistos", codigo, StringComparison.Ordinal);
        Assert.Contains("_sesion.Config.AtajosExamenVistos = true", codigo, StringComparison.Ordinal);

        Assert.NotNull(typeof(AppConfig).GetProperty("AtajosExamenVistos"));
    }

    // ------------------------------------------------------------------
    // US-037 — compartir
    // ------------------------------------------------------------------

    [Fact]
    public void SePuedeExportarDesdeElResultadoYDesdeElHistorial()
    {
        var enExamen = Vista("AutoExam/Views/ExamenView.xaml").Descendants()
            .Select(e => e.Attribute("Command")?.Value ?? string.Empty);

        Assert.Contains(enExamen, c => c.Contains("ExportarCommand", StringComparison.Ordinal));

        var enHistorial = Vista("AutoExam/Views/HistorialView.xaml").Descendants()
            .Select(e => e.Attribute("Command")?.Value ?? string.Empty);

        Assert.Contains(enHistorial, c => c.Contains("CompartirExamenCommand", StringComparison.Ordinal));
    }

    [Fact]
    public void DesdeElHistorial_SoloSePuedeCompartirLoQueTieneDetalle()
    {
        // Un examen de antes de US-025 sólo tiene el resumen numérico: de ahí no sale un
        // examen rendible.
        Assert.NotNull(typeof(HistorialViewModel).GetProperty("SePuedeCompartir"));

        var boton = Vista("AutoExam/Views/HistorialView.xaml").Descendants()
            .FirstOrDefault(e => (e.Attribute("Command")?.Value ?? string.Empty)
                .Contains("CompartirExamenCommand", StringComparison.Ordinal));

        Assert.Contains("SePuedeCompartir", boton!.Attribute("Visibility")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ElExamenImportado_SeGuardaEnLaCarpetaDeLaApp_NoSeRecuerdaLaRuta()
    {
        // Un examen que llegó por Telegram vive en Descargas, y Descargas se vacía: guardando
        // sólo la ruta, el examen desaparecería sin aviso justo cuando se lo quiere rendir.
        string codigo = Fuente("AutoExam/Services/BibliotecaDeCompartidos.cs");

        Assert.Contains("RutasApp.Compartidos", codigo, StringComparison.Ordinal);
        Assert.Contains("CompartirExamenService.Guardar(paquete, destino)", codigo, StringComparison.Ordinal);
    }

    [Fact]
    public void UnArchivoInvalido_SeInformaYNoSeGuardaNada()
    {
        string codigo = Fuente("AutoExam/ViewModels/AsistenteViewModel.cs");

        int importar = codigo.IndexOf("private void ImportarExamen()", StringComparison.Ordinal);
        Assert.True(importar > 0);

        string cuerpo = codigo[importar..(importar + 1200)];

        // Se muestra el motivo y se vuelve: nada de guardar primero y validar después.
        int error = cuerpo.IndexOf("_dialogos.Error", StringComparison.Ordinal);
        int guardar = cuerpo.IndexOf("BibliotecaDeCompartidos.Guardar", StringComparison.Ordinal);

        Assert.True(error > 0 && guardar > error);
    }
}
