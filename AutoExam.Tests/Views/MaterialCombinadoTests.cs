using System.IO;
using System.Xml.Linq;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// US-023 y US-024 en la interfaz: la biblioteca agrupada por materia, y el paso "Material"
/// del asistente con seleccion multiple acotada a una sola materia.
///
/// El detalle que fijan estos tests y que es facil de romper sin darse cuenta es que filtrar
/// por materia no es comodidad: es lo que hace cumplir RN-23 por construccion. Si la lista
/// del asistente volviera a mostrar todos los libros, se podrian tildar dos de materias
/// distintas y el examen combinaria material que la regla prohibe combinar.
/// </summary>
public class MaterialCombinadoTests
{
    private static XDocument Vista(string ruta) => XDocument.Load(ArchivoFuenteHelper.RutaFuente(ruta));

    private static XDocument Biblioteca() => Vista("AutoExam/Views/BibliotecaView.xaml");

    private static XDocument Asistente() => Vista("AutoExam/Views/AsistenteView.xaml");

    // ------------------------------------------------------------------
    // US-023 — la biblioteca se ve agrupada, no como una tira plana
    // ------------------------------------------------------------------

    [Fact]
    public void LaListaDeLibros_SeMuestraAgrupadaPorMateria()
    {
        var lista = Biblioteca().Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ListBox" &&
                                 (e.Attribute("ItemsSource")?.Value ?? string.Empty).Contains("LibrosPorMateria"));

        Assert.True(lista is not null,
            "BibliotecaView.xaml sigue enlazando la lista plana de libros: US-023 pide verlos agrupados por materia.");

        bool tieneGrupos = lista!.Descendants().Any(e => e.Name.LocalName == "GroupStyle");

        Assert.True(tieneGrupos, "La lista no define GroupStyle, asi que los grupos no se veran separados.");
    }

    [Fact]
    public void CadaGrupo_MuestraElNombreDeLaMateria()
    {
        string xaml = File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/BibliotecaView.xaml"));

        // Un grupo sin encabezado agrupa pero no se nota: es visualmente igual a la tira plana.
        //
        // Desde US-030 el encabezado se dibuja con un ContainerStyle y no con un
        // HeaderTemplate: el Expander tiene que envolver también a los items para poder
        // plegarlos, y un HeaderTemplate solo puede dibujar el encabezado.
        Assert.Contains("GroupStyle.ContainerStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("Binding Name", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CadaGrupoDeMateria_SePuedeColapsarYExpandir_US030()
    {
        var grupo = Biblioteca().Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "GroupStyle");

        Assert.True(grupo is not null, "La lista de libros ya no agrupa por materia.");

        var expander = grupo!.Descendants().FirstOrDefault(e => e.Name.LocalName == "Expander");

        Assert.True(expander is not null,
            "Los grupos de materia no se pueden plegar: US-030 pide poder colapsar y expandir cada uno.");

        // Arranca expandido para que la biblioteca se siga viendo entera de entrada.
        Assert.Equal("True", expander!.Attribute("IsExpanded")?.Value);
    }

    [Fact]
    public void CadaGrupoDeMateria_LlevaElColorDeSuMateria_US027_RN34()
    {
        // RN-34: el color sale del mismo esquema que identifica materias en todos lados, no
        // de uno propio de la biblioteca.
        string xaml = File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/BibliotecaView.xaml"));

        Assert.Contains("ColorDeMateriaPorNombre", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void HayAccionesParaCrearRenombrarYEliminarMaterias()
    {
        var comandos = Biblioteca().Descendants()
            .Select(e => e.Attribute("Command")?.Value ?? string.Empty)
            .ToList();

        foreach (string esperado in new[] { "CrearMateriaCommand", "RenombrarMateriaCommand", "EliminarMateriaCommand" })
        {
            Assert.Contains(comandos, c => c.Contains(esperado, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void RenombrarYEliminar_SeDeshabilitanEnLaMateriaPorDefecto()
    {
        // "Sin materia" es el cajon de RN-22 y el destino al borrar otra materia: dejar los
        // botones activos ofreceria una accion que el servicio despues rechaza.
        var botones = Biblioteca().Descendants()
            .Where(e =>
            {
                string comando = e.Attribute("Command")?.Value ?? string.Empty;
                return comando.Contains("RenombrarMateriaCommand", StringComparison.Ordinal) ||
                       comando.Contains("EliminarMateriaCommand", StringComparison.Ordinal);
            })
            .ToList();

        Assert.Equal(2, botones.Count);

        Assert.All(botones, b =>
            Assert.Contains("EsMateriaEditable", b.Attribute("IsEnabled")?.Value ?? string.Empty, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // US-024 — seleccion multiple dentro de una materia
    // ------------------------------------------------------------------

    [Fact]
    public void ElPasoMaterial_PermiteTildarVariosDocumentos()
    {
        bool hayCasilla = Asistente().Descendants()
            .Any(e => e.Name.LocalName == "CheckBox" &&
                      (e.Attribute("IsChecked")?.Value ?? string.Empty).Contains("Seleccionado"));

        Assert.True(hayCasilla,
            "AsistenteView.xaml no tiene casillas para marcar documentos: US-024 pide poder elegir mas de uno.");
    }

    [Fact]
    public void LaListaDelAsistente_EstaFiltradaPorMateria_RN23()
    {
        // Es la garantia estructural de RN-23: si volviera a enlazar todos los libros, se
        // podrian tildar dos de materias distintas.
        var lista = Asistente().Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ListBox" &&
                                 (e.Attribute("ItemsSource")?.Value ?? string.Empty).Contains("LibrosDeLaMateria"));

        Assert.True(lista is not null,
            "El paso Material del asistente no esta filtrado por materia: se podrian combinar materias distintas (RN-23).");
    }

    [Fact]
    public void HayUnSelectorDeMateriaAntesDeLosDocumentos()
    {
        bool hay = Asistente().Descendants()
            .Any(e => (e.Attribute("Command")?.Value ?? string.Empty).Contains("ElegirMateriaCommand"));

        Assert.True(hay, "El asistente no ofrece elegir la materia antes de los documentos (US-024).");
    }

    [Fact]
    public void ElTildeYLaFilaResaltada_SonControlesDistintos()
    {
        // Son dos cosas distintas: el tilde decide que entra al examen, la fila resaltada
        // decide de cual documento se ven los capitulos en el paso Alcance. Unificarlos
        // haria imposible acotar un documento sin cambiar la seleccion.
        var lista = Asistente().Descendants()
            .First(e => e.Name.LocalName == "ListBox" &&
                        (e.Attribute("ItemsSource")?.Value ?? string.Empty).Contains("LibrosDeLaMateria"));

        string seleccion = lista.Attribute("SelectedItem")?.Value ?? string.Empty;

        Assert.Contains("Libro", seleccion, StringComparison.Ordinal);
        Assert.DoesNotContain("Seleccionado", seleccion, StringComparison.Ordinal);
    }

    [Fact]
    public void SeExplicaQueElAlcancePorCapitulos_EsDelDocumentoResaltado()
    {
        // Sin este cartel, con tres documentos tildados los capitulos de un solo documento
        // parecen aplicarse a todos.
        string xaml = File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/AsistenteView.xaml"));

        Assert.Contains("EsExamenCombinado", xaml, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // RN-24 en el prompt: el modelo tiene que devolver el documento
    // ------------------------------------------------------------------

    [Fact]
    public void ElEsquemaDeRespuesta_PideElDocumentoSoloAlCombinar_RN24()
    {
        string codigo = File.ReadAllText(ArchivoFuenteHelper.RutaFuente("AutoExam/Services/GeminiApiService.cs"));

        Assert.Contains("DocumentoOrigen", codigo, StringComparison.Ordinal);

        // Con una sola fuente el campo seria el titulo del examen repetido en cada pregunta,
        // y sumarlo al esquema costaria tokens de salida en TODOS los examenes para nada.
        Assert.Contains("EsquemaPreguntas(bool combinado)", codigo, StringComparison.Ordinal);
        Assert.Contains("EsquemaPreguntas(solicitud.EsCombinado)", codigo, StringComparison.Ordinal);
    }

    [Fact]
    public void LaSolicitud_SoloSeConsideraCombinadaConMasDeUnDocumento()
    {
        var solicitud = new SolicitudGeneracion();
        Assert.False(solicitud.EsCombinado);

        solicitud.Documentos.Add("Guyton");
        Assert.False(solicitud.EsCombinado);

        solicitud.Documentos.Add("Lehninger");
        Assert.True(solicitud.EsCombinado);
    }
}
