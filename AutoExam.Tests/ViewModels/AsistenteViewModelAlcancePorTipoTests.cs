using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;
using AutoExam.Tests.TestDoubles;
using AutoExam.ViewModels;

namespace AutoExam.Tests.ViewModels;

/// <summary>
/// US-009 — el paso Alcance del asistente cambia según <c>Libro.Tipo</c>
/// (specs/03-architecture.md Inc-4 §3 / §4.5, specs/02-tech-spec.md AC-T45 / AC-T46 / AC-T47).
/// Cierra la brecha de test-developer: no había ninguna suite de <c>AsistenteViewModel</c>.
///
/// Cubre:
/// - AC-T45 — para Word/Excel/PowerPoint/SetImagenes, <c>EsFuentePdf == false</c>: el paso
///   Alcance no ofrece capítulos/módulos ni rango de páginas (la vista lo esconde con esa
///   propiedad). Sólo PDF ⇒ <c>EsFuentePdf == true</c> y se pueblan los módulos.
/// - AC-T46 — el eje temático libre sigue disponible y se refleja en <c>ResumenAlcance</c> para
///   cualquier tipo de fuente.
/// - AC-T47 — sin recorte estructural, el alcance de una fuente no-PDF es "material completo"
///   (<c>RecorteFuente.MaterialCompleto</c> a nivel modelo ya está cubierto en
///   <c>PdfExtractorTests</c>; acá se verifica que el VM no arma páginas para no-PDF).
///
/// El cableado de <c>GenerarAsync</c> hacia el extractor (recorte sin páginas para no-PDF;
/// <c>opciones.MaxPaginasEscaneadas = Config.MaxImagenesPorMaterial</c> para el set de imágenes)
/// se verifica por inspección de fuente: el camino completo exige la API de Gemini, fuera de
/// alcance de un test unitario.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class AsistenteViewModelAlcancePorTipoTests
{
    private static AsistenteViewModel NuevoVm()
    {
        var biblioteca = new BibliotecaService();
        var sesion = new SesionUsuarioService();
        sesion.Cargar();
        return new AsistenteViewModel(
            biblioteca, new PdfExtractorService(), new GeminiApiService(),
            sesion, new DialogosDeSimulacion(), new NavegacionDeSimulacion());
    }

    private static Libro LibroPdf() => new()
    {
        Tipo = TipoFuente.Pdf,
        Titulo = "Guyton",
        CantidadPaginas = 200,
        RutaArchivo = "C:/x/g.pdf",
        Archivos = { "C:/x/g.pdf" },
        Modulos =
        {
            new Modulo { Nombre = "Cap 1", DesdePagina = 1, HastaPagina = 20 },
            new Modulo { Nombre = "Cap 2", DesdePagina = 21, HastaPagina = 44 },
        },
    };

    private static Libro LibroNoPdf(TipoFuente tipo) => new()
    {
        Tipo = tipo,
        Titulo = "Apunte",
        MedidaTamanio = tipo == TipoFuente.SetImagenes ? "8 imagenes" : "documento unico",
        RutaArchivo = "C:/x/a.dat",
        Archivos = { "C:/x/a.dat" },
    };

    // ------------------------------------------------------------------
    // AC-T45
    // ------------------------------------------------------------------

    [Fact]
    public void FuentePdf_OfreceCapitulos_YPueblaLosModulos_AC_T45()
    {
        var vm = NuevoVm();

        vm.Libro = LibroPdf();

        Assert.True(vm.EsFuentePdf);
        Assert.Equal(2, vm.Modulos.Count);
        Assert.True(vm.HayModulos);
    }

    [Theory]
    [InlineData(TipoFuente.Word)]
    [InlineData(TipoFuente.Excel)]
    [InlineData(TipoFuente.PowerPoint)]
    [InlineData(TipoFuente.SetImagenes)]
    public void FuenteNoPdf_NoOfreceCapitulos_NiModulos_AC_T45(TipoFuente tipo)
    {
        var vm = NuevoVm();

        vm.Libro = LibroNoPdf(tipo);

        Assert.False(vm.EsFuentePdf);
        Assert.Empty(vm.Modulos);
        Assert.False(vm.HayModulos);
    }

    [Fact]
    public void CambiarDePdfANoPdf_LimpiaLosModulosDelPasoAlcance()
    {
        var vm = NuevoVm();
        vm.Libro = LibroPdf();
        Assert.NotEmpty(vm.Modulos);

        vm.Libro = LibroNoPdf(TipoFuente.Word);

        Assert.Empty(vm.Modulos);
    }

    // ------------------------------------------------------------------
    // AC-T46 — eje temático libre para cualquier tipo
    // ------------------------------------------------------------------

    [Fact]
    public void FuenteNoPdf_SinTema_ElAlcanceEsMaterialCompleto_AC_T47()
    {
        var vm = NuevoVm();
        vm.Libro = LibroNoPdf(TipoFuente.PowerPoint);

        Assert.Equal("material completo", vm.ResumenAlcance);
    }

    [Fact]
    public void FuenteNoPdf_ConTema_ElAlcanceLoIncluye_AC_T46()
    {
        var vm = NuevoVm();
        vm.Libro = LibroNoPdf(TipoFuente.Excel);

        vm.Tema = "  arritmias  ";

        Assert.Contains("material completo", vm.ResumenAlcance);
        Assert.Contains("arritmias", vm.ResumenAlcance);
        Assert.DoesNotContain("pag.", vm.ResumenAlcance);
    }

    [Fact]
    public void FuenteNoPdf_ElResumenDelPasoAlcance_NoHablaDePaginas_AC_T45()
    {
        var vm = NuevoVm();
        vm.Libro = LibroNoPdf(TipoFuente.SetImagenes);

        var pasoAlcance = vm.Pasos[1];
        Assert.DoesNotContain("pag.", pasoAlcance.Resumen);
        Assert.Equal(vm.ResumenAlcance, pasoAlcance.Resumen);
    }

    [Fact]
    public void FuentePdf_ElResumenDelPasoAlcance_SiHablaDePaginas()
    {
        var vm = NuevoVm();
        vm.Libro = LibroPdf();

        Assert.Contains("pag.", vm.Pasos[1].Resumen);
    }

    // ------------------------------------------------------------------
    // AC-T45 / AC-T46 — la vista esconde módulos para no-PDF, deja el eje temático siempre
    // ------------------------------------------------------------------

    [Fact]
    public void AsistenteView_EscondeCapitulosParaNoPdf_YDejaElEjeTematicoSiempre()
    {
        var doc = XDocument.Load(ArchivoFuenteHelper.RutaFuente("AutoExam/Views/AsistenteView.xaml"));

        static string Attr(XElement e, string n) =>
            e.Attributes().FirstOrDefault(a => a.Name.LocalName == n)?.Value ?? string.Empty;

        // Un StackPanel de capítulos/páginas cuya visibilidad depende de EsFuentePdf (no invertido).
        var panelCapitulos = doc.Descendants()
            .Where(e => e.Name.LocalName == "StackPanel")
            .FirstOrDefault(e =>
            {
                var vis = Attr(e, "Visibility");
                return vis.Contains("EsFuentePdf") && !vis.Contains("invertir")
                       && e.Descendants().Any(d => (Attr(d, "Text") == "CAPITULOS")
                                                   || Attr(d, "ItemsSource").Contains("Modulos"));
            });
        Assert.True(panelCapitulos is not null,
            "AsistenteView.xaml: no hay un StackPanel de capítulos/módulos gobernado por EsFuentePdf (AC-T45).");

        // El eje temático (TextBox atado a Tema) vive FUERA de ese panel: sin Visibility propia
        // atada a EsFuentePdf.
        var temaBox = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "TextBox" && Attr(e, "Text").Contains("Tema"));
        Assert.True(temaBox is not null, "AsistenteView.xaml: no se encontró el TextBox del eje temático.");
        Assert.DoesNotContain("EsFuentePdf", Attr(temaBox!, "Visibility"));
        Assert.False(temaBox!.Ancestors().Any(a => a == panelCapitulos),
            "El eje temático quedó dentro del panel que se esconde para no-PDF (rompe AC-T46).");
    }

    // ------------------------------------------------------------------
    // Cableado de GenerarAsync (inspección de fuente)
    // ------------------------------------------------------------------

    [Fact]
    public void GenerarAsync_NoArmaPaginasParaFuenteNoPdf_YFijaElTopeDeImagenesDesdeLaConfig()
    {
        string fuente = File.ReadAllText(
            ArchivoFuenteHelper.RutaFuente("AutoExam/ViewModels/AsistenteViewModel.cs"));

        // recorte.Paginas = null cuando no es PDF (AC-T47 en el camino real).
        Assert.Matches(new Regex(@"Paginas\s*=\s*esPdf\s*\?\s*rangos\s*:\s*null"), fuente);

        // El set de imágenes toma su límite de AppConfig.MaxImagenesPorMaterial antes de extraer (NFR-43).
        Assert.Matches(
            new Regex(@"MaxPaginasEscaneadas\s*=\s*Math\.Max\(\s*1\s*,\s*_sesion\.Config\.MaxImagenesPorMaterial\s*\)"),
            fuente);
    }
}
