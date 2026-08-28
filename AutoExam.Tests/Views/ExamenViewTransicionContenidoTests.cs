using System.Xml.Linq;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Views;

/// <summary>
/// AC-T22 (specs/02-tech-spec.md Incremento 2): ningún <c>ContentControl</c> de
/// <c>Views/ExamenView.xaml</c> usa <c>TransicionContenido.Activa</c> — verificación
/// estructural, no de comportamiento en runtime (así lo pide el propio criterio de
/// aceptación).
///
/// El único <c>ContentControl</c> alcanzado por US-007 es el de <c>MainWindow.xaml</c> que
/// bindea <c>{Binding Pagina}</c> (specs/03-architecture.md Incremento 2, "Restricciones
/// técnicas" y §3.3): eso es lo que hace que la navegación pregunta-a-pregunta de
/// <c>ExamenView</c> (que cambia por <c>Visibility</c>/binding dentro del mismo
/// <c>UserControl</c>, nunca reemplazando el <c>Content</c> de un <c>ContentControl</c>) quede
/// intacta por construcción, no por disciplina de código — este test es la red de seguridad de
/// esa construcción.
///
/// Parsea el XAML directo del checkout con <c>System.Xml.Linq</c> (sin runtime WPF), mismo
/// criterio que <c>EstilosXamlAnimacionesHoverPresionTests</c> — team-roster.yaml,
/// <c>test-dev-animaciones-shell</c>.
/// </summary>
public class ExamenViewTransicionContenidoTests
{
    private static readonly Lazy<XDocument> Documento = new(() =>
        XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/ExamenView.xaml")));

    [Fact]
    public void NingunContentControlUsaTransicionContenidoActiva_AC_T22()
    {
        var contentControlsConActiva = Documento.Value.Descendants()
            .Where(e => e.Name.LocalName is "ContentControl" or "ContentPresenter")
            .SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName == "TransicionContenido.Activa")
            .ToList();

        Assert.Empty(contentControlsConActiva);
    }

    [Fact]
    public void NingunElementoDelArchivoUsaElAtributoTransicionContenidoActiva_ControlMasAmplio_AC_T22()
    {
        // Mas amplio que el test de arriba: cubre tambien el caso (poco probable, pero real si
        // alguien copia/pega el wiring de MainWindow.xaml sin fijarse el tipo del elemento) de
        // que TransicionContenido.Activa termine puesto sobre CUALQUIER elemento de este
        // archivo, no solo uno tipado explicitamente como ContentControl/ContentPresenter.
        var cualquierUsoDelAtributo = Documento.Value.Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName == "TransicionContenido.Activa")
            .ToList();

        Assert.Empty(cualquierUsoDelAtributo);
    }
}
