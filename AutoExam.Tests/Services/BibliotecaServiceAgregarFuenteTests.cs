using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services;

/// <summary>
/// M3 — <c>BibliotecaService.AgregarFuenteAsync</c> (specs/03-architecture.md Inc-4 §3 / §4.2,
/// specs/02-tech-spec.md AC-T41 / AC-T43 / AC-T48, NFR-37 / NFR-40 / NFR-41 / NFR-43 / NFR-A8).
/// Cierra la brecha de test-developer: <c>specs/test-plan.md</c> asumía un
/// <c>BibliotecaServiceAgregarFuenteTests</c> que nunca se creó.
///
/// Cubre:
/// - AC-T48 / NFR-43 — N imágenes = 1 <c>Libro</c> <c>Tipo=SetImagenes</c>, <c>Archivos</c> en
///   orden de alta (nombres origen no alfabéticos), copia interna correlativa <c>NN.ext</c>.
/// - AC-T41 / NFR-40 — <c>MedidaTamanio</c> poblada tras el alta (PDF → "N paginas",
///   set-imágenes → "N imagenes").
/// - AC-T43 / NFR-37 — extensión legacy (<c>.doc/.xls/.ppt</c>) o desconocida ⇒
///   <see cref="FormatoNoSoportadoException"/>, 0 fuentes; mezcla de tipos / varios archivos
///   no-imagen ⇒ <see cref="FuenteInvalidaException"/> con <c>Message</c> limpio (sin sufijo
///   <c>(Parameter ...)</c>); archivo ilegible ⇒ <see cref="FuenteIlegibleException"/> sin dejar
///   copia parcial.
/// - NFR-A8 — persistencia sólo vía <c>JsonStore</c>+<c>RutasApp</c>; <c>EliminarLibro</c> borra
///   archivo (tipo único) o carpeta (set-imágenes) y persiste.
/// - <c>AgregarLibroAsync</c> como wrapper: el call-site viejo (1 PDF) sigue dando un
///   <c>Libro</c> <c>Tipo=Pdf</c> con módulo por defecto.
///
/// Comparte <see cref="RutasAisladasCollection"/> (rutas estáticas globales); limpia
/// <c>libros.json</c> + <c>Biblioteca\</c> antes de cada test.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class BibliotecaServiceAgregarFuenteTests : IDisposable
{
    private readonly CarpetaDescartable _origen = new();

    public BibliotecaServiceAgregarFuenteTests() => LimpiarDeposito();

    public void Dispose()
    {
        _origen.Dispose();
        LimpiarDeposito();
    }

    // ------------------------------------------------------------------
    // AC-T48 / NFR-43 — set de imágenes
    // ------------------------------------------------------------------

    [Fact]
    public async Task AgregarFuente_SetImagenes_UnaFuente_ArchivosEnOrdenDeAlta_YCopiaCorrelativa_AC_T48()
    {
        // Nombres origen deliberadamente no alfabéticos, y tamaños distintos para poder
        // distinguir qué copia interna quedó en qué posición.
        string z = FuentesDePrueba.CrearPng(_origen.Ruta, "z-primera.png", lado: 16);
        string a = FuentesDePrueba.CrearPng(_origen.Ruta, "a-segunda.png", lado: 32);
        string m = FuentesDePrueba.CrearPng(_origen.Ruta, "m-tercera.png", lado: 48);

        var svc = new BibliotecaService();
        svc.Cargar();

        var libro = await svc.AgregarFuenteAsync(new[] { z, a, m }, "Fotos de clase", "Bioquímica");

        Assert.Equal(TipoFuente.SetImagenes, libro.Tipo);
        Assert.Equal(3, libro.Archivos.Count);
        Assert.Equal(libro.Archivos[0], libro.RutaArchivo);
        Assert.EndsWith("01.png", libro.Archivos[0]);
        Assert.EndsWith("02.png", libro.Archivos[1]);
        Assert.EndsWith("03.png", libro.Archivos[2]);
        Assert.All(libro.Archivos, r => Assert.StartsWith(Path.Combine(RutasApp.Biblioteca, libro.Id), r));

        // Orden de alta preservado (no alfabético): la 1ª copia == z-primera.png, etc.
        Assert.Equal(File.ReadAllBytes(z), File.ReadAllBytes(libro.Archivos[0]));
        Assert.Equal(File.ReadAllBytes(a), File.ReadAllBytes(libro.Archivos[1]));
        Assert.Equal(File.ReadAllBytes(m), File.ReadAllBytes(libro.Archivos[2]));

        Assert.Equal("3 imagenes", libro.MedidaTamanio);
        Assert.Empty(libro.Modulos);   // módulos son sólo de PDF con índice
    }

    [Fact]
    public async Task AgregarFuente_SetImagenes_Persiste_ConLaFormaNueva_NFR_A8()
    {
        string p1 = FuentesDePrueba.CrearJpeg(_origen.Ruta, "uno.jpg");
        string p2 = FuentesDePrueba.CrearJpeg(_origen.Ruta, "dos.jpg");

        var primera = new BibliotecaService();
        primera.Cargar();
        var libro = await primera.AgregarFuenteAsync(new[] { p1, p2 }, "Set", "M");

        var segunda = new BibliotecaService();
        segunda.Cargar();

        var recargado = Assert.Single(segunda.Libros);
        Assert.Equal(libro.Id, recargado.Id);
        Assert.Equal(TipoFuente.SetImagenes, recargado.Tipo);
        Assert.Equal(2, recargado.Archivos.Count);
        Assert.Equal("2 imagenes", recargado.MedidaTamanio);
    }

    // ------------------------------------------------------------------
    // AC-T41 / NFR-40 — medida por formato
    // ------------------------------------------------------------------

    [Fact]
    public async Task AgregarFuente_Pdf_PueblaMedidaTamanioEnPaginas_YModuloPorDefecto_AC_T41()
    {
        string pdf = FuentesDePrueba.CrearPdf(_origen.Ruta, paginas: 3);

        var svc = new BibliotecaService();
        svc.Cargar();

        var libro = await svc.AgregarFuenteAsync(new[] { pdf }, "Guyton", "Fisio");

        Assert.Equal(TipoFuente.Pdf, libro.Tipo);
        Assert.Equal(3, libro.CantidadPaginas);
        Assert.Equal("3 paginas", libro.MedidaTamanio);
        Assert.Single(libro.Modulos);
        Assert.Single(svc.Libros);
    }

    [Fact]
    public async Task AgregarLibroAsync_Wrapper_UnPdf_DaFuentePdfConUnArchivo_YModulo()
    {
        string pdf = FuentesDePrueba.CrearPdf(_origen.Ruta, paginas: 2);

        var svc = new BibliotecaService();
        svc.Cargar();

        var libro = await svc.AgregarLibroAsync(pdf, "Robbins", "Patología");

        Assert.Equal(TipoFuente.Pdf, libro.Tipo);
        Assert.Single(libro.Archivos);
        Assert.Equal(libro.RutaArchivo, libro.Archivos[0]);
        Assert.NotEmpty(libro.Modulos);
    }

    // ------------------------------------------------------------------
    // AC-T43 / NFR-37 — rechazo de formato
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(".doc")]
    [InlineData(".xls")]
    [InlineData(".ppt")]
    [InlineData(".rtf")]
    [InlineData(".zip")]
    public async Task AgregarFuente_ExtensionLegacyODesconocida_Rechaza_SinCrearFuente_AC_T43(string extension)
    {
        string archivo = FuentesDePrueba.CrearArchivoConExtension(_origen.Ruta, extension);

        var svc = new BibliotecaService();
        svc.Cargar();

        await Assert.ThrowsAsync<FormatoNoSoportadoException>(
            () => svc.AgregarFuenteAsync(new[] { archivo }, "X", "Y"));

        Assert.Empty(svc.Libros);
        Assert.Empty(Directory.GetFileSystemEntries(RutasApp.Biblioteca));
    }

    [Fact]
    public async Task AgregarFuente_MezclaDeTipos_FuenteInvalida_MensajeLimpio_SinCopiarNada_AC_T43()
    {
        string foto = FuentesDePrueba.CrearPng(_origen.Ruta, "foto.png");
        string pdf = FuentesDePrueba.CrearPdf(_origen.Ruta, paginas: 1);

        var svc = new BibliotecaService();
        svc.Cargar();

        var ex = await Assert.ThrowsAsync<FuenteInvalidaException>(
            () => svc.AgregarFuenteAsync(new[] { foto, pdf }, "X", "Y"));

        Assert.DoesNotContain("Parameter", ex.Message);
        Assert.DoesNotContain("(Parameter", ex.Message);
        Assert.Empty(svc.Libros);
        Assert.Empty(Directory.GetFileSystemEntries(RutasApp.Biblioteca));
    }

    [Fact]
    public async Task AgregarFuente_VariosArchivosNoImagen_FuenteInvalida_SinCopiarNada()
    {
        string a = FuentesDePrueba.CrearPdf(_origen.Ruta, paginas: 1);
        string b = FuentesDePrueba.CrearDocx(_origen.Ruta, "texto");

        var svc = new BibliotecaService();
        svc.Cargar();

        // Dos familias distintas (Pdf + Word) → mezcla de tipos.
        var ex = await Assert.ThrowsAsync<FuenteInvalidaException>(
            () => svc.AgregarFuenteAsync(new[] { a, b }, "X", "Y"));
        Assert.DoesNotContain("Parameter", ex.Message);

        // Dos PDF → misma familia pero "un examen = una fuente".
        string c = FuentesDePrueba.CrearPdf(_origen.Ruta, paginas: 2);
        var ex2 = await Assert.ThrowsAsync<FuenteInvalidaException>(
            () => svc.AgregarFuenteAsync(new[] { a, c }, "X", "Y"));
        Assert.DoesNotContain("Parameter", ex2.Message);

        Assert.Empty(svc.Libros);
        Assert.Empty(Directory.GetFileSystemEntries(RutasApp.Biblioteca));
    }

    [Fact]
    public async Task AgregarFuente_ArchivoInexistente_LanzaFileNotFound_SinCrearFuente()
    {
        var svc = new BibliotecaService();
        svc.Cargar();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => svc.AgregarFuenteAsync(new[] { Path.Combine(_origen.Ruta, "fantasma.pdf") }, "X", "Y"));

        Assert.Empty(svc.Libros);
        Assert.Empty(Directory.GetFileSystemEntries(RutasApp.Biblioteca));
    }

    [Fact]
    public async Task AgregarFuente_PdfDanado_BorraLaCopiaParcial_YReLanzaConCausa_AC_T43_NFR41()
    {
        string roto = FuentesDePrueba.CrearPdfCorrupto(_origen.Ruta);

        var svc = new BibliotecaService();
        svc.Cargar();

        await Assert.ThrowsAsync<FuenteIlegibleException>(
            () => svc.AgregarFuenteAsync(new[] { roto }, "X", "Y"));

        Assert.Empty(svc.Libros);
        // 0 fuentes vacías creadas y 0 copias parciales en Biblioteca\ (AC-T43).
        Assert.Empty(Directory.GetFileSystemEntries(RutasApp.Biblioteca));
    }

    // ------------------------------------------------------------------
    // NFR-A8 — EliminarLibro
    // ------------------------------------------------------------------

    [Fact]
    public async Task EliminarLibro_Pdf_BorraElArchivo_YPersiste()
    {
        string pdf = FuentesDePrueba.CrearPdf(_origen.Ruta, paginas: 2);
        var svc = new BibliotecaService();
        svc.Cargar();
        var libro = await svc.AgregarFuenteAsync(new[] { pdf }, "T", "M");
        string copia = libro.RutaArchivo;
        Assert.True(File.Exists(copia));

        svc.EliminarLibro(libro);

        Assert.False(File.Exists(copia));
        Assert.Empty(svc.Libros);

        var recargado = new BibliotecaService();
        recargado.Cargar();
        Assert.Empty(recargado.Libros);
    }

    [Fact]
    public async Task EliminarLibro_SetImagenes_BorraLaCarpetaCompleta()
    {
        string p1 = FuentesDePrueba.CrearPng(_origen.Ruta, "a.png");
        string p2 = FuentesDePrueba.CrearPng(_origen.Ruta, "b.png");
        var svc = new BibliotecaService();
        svc.Cargar();
        var libro = await svc.AgregarFuenteAsync(new[] { p1, p2 }, "Set", "M");
        string carpeta = Path.Combine(RutasApp.Biblioteca, libro.Id);
        Assert.True(Directory.Exists(carpeta));

        svc.EliminarLibro(libro);

        Assert.False(Directory.Exists(carpeta));
        Assert.Empty(svc.Libros);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static void LimpiarDeposito()
    {
        RutasApp.AsegurarCarpetas();
        if (File.Exists(RutasApp.ArchivoLibros))
        {
            File.Delete(RutasApp.ArchivoLibros);
        }
        foreach (var entrada in Directory.GetFileSystemEntries(RutasApp.Biblioteca))
        {
            try
            {
                if (File.Exists(entrada)) File.Delete(entrada);
                else Directory.Delete(entrada, recursive: true);
            }
            catch { /* best-effort */ }
        }
    }
}
