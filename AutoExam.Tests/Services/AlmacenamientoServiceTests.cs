using System.IO;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-046 — ver cuánto ocupa AutoExam y liberar lo que sobra.
///
/// Lo que estos tests protegen no es el número: es RN-54. "Vaciar caché" tiene que borrar
/// únicamente archivos temporales regenerables, y la forma de que eso se rompa sin que nadie
/// se entere es que un día se borre también la carpeta de imágenes de un examen que sigue en
/// el historial — el detalle de US-025 y las figuras de US-018 quedarían con un hueco meses
/// después, y nadie lo va a asociar a un botón que se tocó una vez.
///
/// Van en la colección de rutas aisladas: escriben archivos de verdad, y sin eso lo harían
/// sobre la biblioteca real de quien corre la suite.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class AlmacenamientoServiceTests
{
    private static string CrearCarpetaDeImagenes(string examenId, int bytes)
    {
        string carpeta = Path.Combine(RutasApp.Imagenes, examenId);
        Directory.CreateDirectory(carpeta);
        File.WriteAllBytes(Path.Combine(carpeta, "figura.png"), new byte[bytes]);
        return carpeta;
    }

    private static void LimpiarImagenes()
    {
        if (Directory.Exists(RutasApp.Imagenes))
        {
            Directory.Delete(RutasApp.Imagenes, recursive: true);
        }

        Directory.CreateDirectory(RutasApp.Imagenes);
    }

    // ------------------------------------------------------------------
    // AC — se ve el espacio de cada grupo
    // ------------------------------------------------------------------

    [Fact]
    public void Medir_DevuelveLosTresGrupos_EnElOrdenEnQueSeMuestran_US046()
    {
        var usos = AlmacenamientoService.Medir(Array.Empty<string>());

        Assert.Equal(3, usos.Count);
        Assert.Equal("Libros", usos[0].Etiqueta);
        Assert.Equal("Historial", usos[1].Etiqueta);
        Assert.Equal("Temporales", usos[2].Etiqueta);
    }

    [Fact]
    public void ElTamanioDeLosLibros_CuentaLoQueHayEnLaCarpetaBiblioteca_US046()
    {
        RutasApp.AsegurarCarpetas();

        string archivo = Path.Combine(RutasApp.Biblioteca, $"{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(archivo, new byte[4096]);

        try
        {
            var libros = AlmacenamientoService.Medir(Array.Empty<string>())[0];

            Assert.True(libros.Medido);
            Assert.True(libros.Bytes >= 4096,
                $"Los libros midieron {libros.Bytes} bytes y el PDF de prueba ocupa 4096.");
        }
        finally
        {
            File.Delete(archivo);
        }
    }

    /// <summary>
    /// Las imágenes de un examen que sigue en el historial son historial, no temporales: si
    /// contaran como temporales, el número de "Temporales" estaría prometiendo liberar algo
    /// que no se puede liberar sin romper US-025.
    /// </summary>
    [Fact]
    public void LasImagenesDeUnExamenDelHistorial_CuentanComoHistorial_NoComoTemporales_US046()
    {
        LimpiarImagenes();
        CrearCarpetaDeImagenes("examen-vivo", 2048);

        var usos = AlmacenamientoService.Medir(new[] { "examen-vivo" });

        Assert.True(usos[1].Bytes >= 2048, "Las imágenes del examen vivo no se contaron como historial.");
        Assert.Equal(0, usos[2].Bytes);
    }

    [Fact]
    public void LasImagenesHuerfanas_CuentanComoTemporales_US046()
    {
        LimpiarImagenes();
        CrearCarpetaDeImagenes("intento-abandonado", 3072);

        var usos = AlmacenamientoService.Medir(new[] { "otro-examen" });

        Assert.True(usos[2].Bytes >= 3072, "Una carpeta huérfana no se contó como temporal.");
    }

    // ------------------------------------------------------------------
    // AC / RN-54 — vaciar caché no toca datos
    // ------------------------------------------------------------------

    [Fact]
    public void VaciarCache_BorraLasHuerfanas_YDejaIntactasLasDelHistorial_RN54()
    {
        LimpiarImagenes();

        string viva = CrearCarpetaDeImagenes("sigue-en-el-historial", 1024);
        string huerfana = CrearCarpetaDeImagenes("intento-perdido", 1024);

        long liberados = AlmacenamientoService.VaciarCache(new[] { "sigue-en-el-historial" });

        Assert.True(Directory.Exists(viva),
            "Vaciar caché borró las imágenes de un examen que sigue en el historial (RN-54).");
        Assert.False(Directory.Exists(huerfana), "Vaciar caché no borró la carpeta huérfana.");
        Assert.True(liberados >= 1024);
    }

    [Fact]
    public void VaciarCache_NoTocaLibrosNiConfiguracion_RN54()
    {
        RutasApp.AsegurarCarpetas();
        LimpiarImagenes();
        CrearCarpetaDeImagenes("huerfano", 512);

        string libro = Path.Combine(RutasApp.Biblioteca, $"{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(libro, new byte[256]);
        File.WriteAllText(RutasApp.ArchivoConfig, "{}");
        File.WriteAllText(RutasApp.ArchivoPerfil, "{}");

        try
        {
            AlmacenamientoService.VaciarCache(Array.Empty<string>());

            Assert.True(File.Exists(libro), "Vaciar caché borró un libro (RN-54).");
            Assert.True(File.Exists(RutasApp.ArchivoConfig), "Vaciar caché borró config.json (RN-54).");
            Assert.True(File.Exists(RutasApp.ArchivoPerfil), "Vaciar caché borró perfil.json (RN-54).");
        }
        finally
        {
            File.Delete(libro);
        }
    }

    [Fact]
    public void VaciarCache_SinNadaQueBorrar_DevuelveCero_YNoLanza()
    {
        LimpiarImagenes();

        Assert.Equal(0, AlmacenamientoService.VaciarCache(Array.Empty<string>()));
    }

    // ------------------------------------------------------------------
    // AC — "no se pudo medir" se dice, no se muestra como cero
    // ------------------------------------------------------------------

    [Fact]
    public void UnGrupoQueNoSePudoMedir_LoDice_EnVezDeMostrarUnNumero_US046()
    {
        // No se puede provocar un fallo de permisos de forma portable, así que se verifica el
        // contrato del tipo: con Medido en false el texto no es un tamaño. Es lo que hace que
        // la pantalla pueda distinguir "cero bytes" de "no se pudo leer la carpeta".
        var sinMedir = new UsoDeDisco("Libros", 0, Medido: false);
        var medido = new UsoDeDisco("Libros", 0, Medido: true);

        Assert.Equal("no se pudo medir", sinMedir.Texto);
        Assert.NotEqual(sinMedir.Texto, medido.Texto);
    }

    // ------------------------------------------------------------------
    // "en términos simples": las unidades que usa una persona
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(512, "B")]
    [InlineData(4096, "KB")]
    [InlineData(5 * 1024 * 1024, "MB")]
    [InlineData(3L * 1024 * 1024 * 1024, "GB")]
    public void Legible_UsaLaUnidadQueCorresponde(long bytes, string unidad)
    {
        Assert.EndsWith(unidad, AlmacenamientoService.Legible(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public void Legible_PorDebajoDeUnMega_NoMuestraDecimales()
    {
        // "312 KB" se lee de un vistazo; "0,3 MB" obliga a traducir.
        Assert.DoesNotContain(",", AlmacenamientoService.Legible(319_488), StringComparison.Ordinal);
        Assert.DoesNotContain(".", AlmacenamientoService.Legible(319_488), StringComparison.Ordinal);
    }
}
