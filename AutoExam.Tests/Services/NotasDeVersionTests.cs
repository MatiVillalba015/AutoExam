using System.IO;
using System.Reflection;
using System.Xml.Linq;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;
using AutoExam.ViewModels;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-040 — notas de versión en Ajustes.
///
/// Dos mitades: que el CHANGELOG.md se parsee bien (con textos armados a mano, para no atar los
/// tests a lo que diga el archivo real), y que el archivo real cumpla el formato y viaje
/// adentro del build (RN-51).
/// </summary>
public class NotasDeVersionTests
{
    private const string Ejemplo = """
        # Notas de versión de AutoExam

        Un párrafo de introducción que no es una versión.

        <!--
        ## X.Y.Z — D de mes de AAAA

        ### Nuevo
        - plantilla para copiar
        -->

        ## 1.1.0 — 4 de septiembre de 2026

        ### Nuevo
        - Podés ponerle color a cada materia.
        - Repaso de lo que fallaste.

        ### Arreglos
        - Las tarjetas del menú se quedaban en blanco.

        ## 1.0.5 — 2 de agosto de 2026

        ### Cambios
        - El historial se ve como tarjetas.
        """;

    // ------------------------------------------------------------------
    // Parseo
    // ------------------------------------------------------------------

    [Fact]
    public void LasVersionesSalenDeLaMasNuevaALaMasVieja()
    {
        // El criterio pide "orden cronológico, la más reciente arriba". Se respeta el orden del
        // archivo, que es como se escribe a mano.
        var versiones = NotasDeVersion.Parsear(Ejemplo);

        Assert.Equal(new[] { "1.1.0", "1.0.5" }, versiones.Select(v => v.Version));
    }

    [Fact]
    public void CadaVersionTraeSuFechaYSusGrupos()
    {
        var versiones = NotasDeVersion.Parsear(Ejemplo);

        var actual = versiones[0];

        Assert.Equal("4 de septiembre de 2026", actual.Fecha);
        Assert.Equal(new[] { "Nuevo", "Arreglos" }, actual.Grupos.Select(g => g.Titulo));
        Assert.Equal(2, actual.Grupos[0].Puntos.Count);
        Assert.Equal("Podés ponerle color a cada materia.", actual.Grupos[0].Puntos[0]);
        Assert.Equal(3, actual.Cantidad);
    }

    [Fact]
    public void LaPlantillaComentada_NoSeCuelaComoUnaVersion()
    {
        // El archivo lleva un bloque comentado para copiar y pegar al agregar una versión. Sin
        // saltear los comentarios, aparecería una versión fantasma llamada "X.Y.Z".
        var versiones = NotasDeVersion.Parsear(Ejemplo);

        Assert.DoesNotContain(versiones, v => v.Version.Contains('X', StringComparison.Ordinal));
        Assert.DoesNotContain(versiones,
            v => v.Grupos.Any(g => g.Puntos.Any(p => p.Contains("plantilla", StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public void ElTextoIntroductorio_NoSeCuelaComoNota()
    {
        var versiones = NotasDeVersion.Parsear(Ejemplo);

        Assert.DoesNotContain(versiones,
            v => v.Grupos.Any(g => g.Puntos.Any(p => p.Contains("introducción", StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public void UnPuntoCortadoEnVariasLineas_LlegaEntero()
    {
        // Los puntos largos se cortan a mano para que la línea no se vaya de ancho. Sin unir
        // las continuaciones, la mitad de la frase desaparecería de la pantalla.
        const string partido = """
            ## 1.0.0 — hoy

            ### Nuevo
            - Una frase que sigue
              en la línea de abajo.
            """;

        var punto = NotasDeVersion.Parsear(partido)[0].Grupos[0].Puntos[0];

        Assert.Equal("Una frase que sigue en la línea de abajo.", punto);
    }

    [Fact]
    public void UnaVersionSinFecha_SeLeeIgual()
    {
        var versiones = NotasDeVersion.Parsear("## 2.0.0\n\n### Nuevo\n- algo\n");

        Assert.Equal("2.0.0", versiones[0].Version);
        Assert.Equal(string.Empty, versiones[0].Fecha);
        Assert.Equal(string.Empty, versiones[0].Subtitulo);
    }

    [Fact]
    public void UnaVersionSinPuntos_NoSeListaComoVersionVacia()
    {
        // Un encabezado suelto sin nada abajo no aporta nada y se vería como una tarjeta vacía.
        var versiones = NotasDeVersion.Parsear("## 3.0.0 — hoy\n\n## 2.9.0 — ayer\n\n### Nuevo\n- algo\n");

        Assert.Equal(new[] { "2.9.0" }, versiones.Select(v => v.Version));
    }

    [Fact]
    public void UnArchivoVacioOIlegible_DevuelveListaVacia_NoTira()
    {
        Assert.Empty(NotasDeVersion.Parsear(null));
        Assert.Empty(NotasDeVersion.Parsear(string.Empty));
        Assert.Empty(NotasDeVersion.Parsear("   \n\n  "));
    }

    // ------------------------------------------------------------------
    // RN-51 — viajan adentro del build
    // ------------------------------------------------------------------

    [Fact]
    public void ElChangelog_ViajaEmbebidoEnElEjecutable_RN51()
    {
        // RN-51: "no se descargan de internet en tiempo de ejecución". Si el recurso dejara de
        // embeberse, la pantalla quedaría vacía en la máquina del usuario y verde acá, porque
        // el archivo sigue existiendo en el repo.
        var recursos = typeof(NotasDeVersion).Assembly.GetManifestResourceNames();

        Assert.Contains("AutoExam.CHANGELOG.md", recursos);
    }

    [Fact]
    public void ElCsproj_EmbebeElArchivoDeLaRaizDelRepo_RN50()
    {
        // RN-50 pide un archivo propio del repositorio. Se embebe con un vínculo al de la raíz
        // en vez de copiarlo adentro del proyecto: con dos copias, la que se edita a mano y la
        // que viaja en el build se separan y nadie se entera.
        var csproj = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/AutoExam.csproj"));

        var recurso = csproj.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "EmbeddedResource" &&
                                 (e.Attribute("Include")?.Value ?? string.Empty)
                                     .Contains("CHANGELOG.md", StringComparison.Ordinal));

        Assert.True(recurso is not null, "El CHANGELOG dejó de embeberse en el build (RN-51).");
        Assert.Contains("..", recurso!.Attribute("Include")!.Value, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // El archivo real
    // ------------------------------------------------------------------

    [Fact]
    public void ElChangelogReal_SeParseaYTieneAlMenosUnaVersion()
    {
        Assert.NotEmpty(NotasDeVersion.Todas);
        Assert.All(NotasDeVersion.Todas, v => Assert.NotEmpty(v.Grupos));
    }

    [Fact]
    public void LaVersionInstalada_TieneSusNotas()
    {
        // El caso "todavía no hay notas" existe y está contemplado, pero no puede ser el estado
        // normal del repo: si esto se pone rojo es porque se subió la versión en el csproj sin
        // agregarle su entrada al CHANGELOG.
        Assert.False(NotasDeVersion.FaltanLasDeLaInstalada,
            $"La versión instalada ({NotasDeVersion.VersionDeEsteBuild}) no tiene entrada en " +
            "CHANGELOG.md. Cada versión nueva agrega la suya antes del release (RN-50).");

        Assert.True(NotasDeVersion.DeLaInstalada!.EsLaInstalada);
    }

    [Fact]
    public void LasNotasReales_NoTienenJergaTecnicaNiMensajesDeCommit()
    {
        // El criterio es explícito: "lenguaje simple orientado al alumno que usa la app, no
        // jerga técnica ni mensajes de commit de git copiados tal cual". Esta lista no atrapa
        // todo, pero sí lo que se cuela cuando alguien copia del historial de git.
        string[] sospechosas =
        {
            "commit", "merge", "refactor", "ViewModel", "XAML", "nullable",
            "excepción", "stacktrace", "binding", "async", "csproj",
        };

        foreach (var version in NotasDeVersion.Todas)
        {
            foreach (var punto in version.Grupos.SelectMany(g => g.Puntos))
            {
                foreach (string jerga in sospechosas)
                {
                    Assert.False(punto.Contains(jerga, StringComparison.OrdinalIgnoreCase),
                        $"La nota de {version.Version} usa jerga técnica (\"{jerga}\"): {punto}");
                }
            }
        }
    }

    [Fact]
    public void CadaGrupo_UsaUnoDeLosTresTitulosAcordados()
    {
        // Tres categorías y no más: son las que nombra el criterio ("qué se agregó, cambió o
        // arregló") y las que la plantilla del archivo ofrece.
        string[] permitidos = { "Nuevo", "Cambios", "Arreglos" };

        foreach (var version in NotasDeVersion.Todas)
        {
            Assert.All(version.Grupos, g => Assert.Contains(g.Titulo, permitidos));
        }
    }

    // ------------------------------------------------------------------
    // La pantalla
    // ------------------------------------------------------------------

    [Fact]
    public void ElBoton_EstaJuntoAlNumeroDeVersion_US040()
    {
        // El criterio pide el botón "cerca de donde ya se muestra el número de versión". Se
        // verifica que compartan la misma tarjeta: es lo que hace que quien acaba de leer
        // "AutoExam 1.1.0" lo encuentre sin buscar.
        var vista = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/AjustesView.xaml"));

        var boton = vista.Descendants()
            .FirstOrDefault(e => (e.Attribute("Command")?.Value ?? string.Empty)
                .Contains("AlternarNotasCommand", StringComparison.Ordinal));

        Assert.True(boton is not null, "No hay botón de notas de versión en Ajustes (US-040).");

        var tarjeta = boton!.Ancestors().First(a => a.Name.LocalName == "Border");

        bool conLaVersion = tarjeta.Descendants().Any(e =>
            (e.Attribute("Text")?.Value ?? string.Empty).Contains("VersionActual", StringComparison.Ordinal));

        Assert.True(conLaVersion,
            "El botón de notas no está en la misma tarjeta que el número de versión (US-040).");
    }

    [Fact]
    public void SinNotasParaLaVersionInstalada_SeAvisa_NoQuedaVacio()
    {
        var vista = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/AjustesView.xaml"));

        bool aviso = vista.Descendants().Any(e =>
            (e.Attribute("Message")?.Value ?? string.Empty).Contains("AvisoSinNotas", StringComparison.Ordinal));

        Assert.True(aviso, "No se avisa cuando la versión instalada no tiene notas (US-040).");

        Assert.Contains("Todavía no hay notas", NotasDeVersion.AvisoSinNotas, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Versiones")]
    [InlineData("MostrarNotas")]
    [InlineData("FaltanNotasDeEstaVersion")]
    [InlineData("AvisoSinNotas")]
    [InlineData("HayNotas")]
    public void Ajustes_ExponeLoQueEnlazaLaPantalla(string miembro)
    {
        bool existe = typeof(AjustesViewModel)
            .GetProperty(miembro, BindingFlags.Public | BindingFlags.Instance) is not null;

        Assert.True(existe, $"AjustesViewModel no expone \"{miembro}\": el control quedaría mudo.");
    }

    [Fact]
    public void LasNotasArrancanCerradas_YElMismoBotonLasAbreYCierra()
    {
        // Ajustes es una pantalla de configuración: las notas se abren cuando se las pide.
        var vm = new AjustesViewModel(
            new SesionUsuarioService(),
            new GeminiApiService(),
            new TestDoubles.DialogosDeSimulacion(),
            new TestDoubles.NavegacionDeSimulacion());

        Assert.False(vm.MostrarNotas);

        vm.AlternarNotasCommand.Execute(null);
        Assert.True(vm.MostrarNotas);

        vm.AlternarNotasCommand.Execute(null);
        Assert.False(vm.MostrarNotas);
    }
}
