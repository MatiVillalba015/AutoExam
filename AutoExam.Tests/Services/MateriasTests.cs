using System.IO;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-023 / RN-22 — la biblioteca se organiza en Materias.
///
/// El nombre de la materia ya vivia en <c>Libro.Materia</c> como texto libre, asi que la
/// migracion sale gratis. Lo que agrega este incremento es el indice aparte
/// (<c>materias.json</c>), y esa decision es la que estos tests protegen: sin el, una materia
/// recien creada y todavia vacia no tendria donde existir y desapareceria al reiniciar, que
/// es justo el caso del alumno que arma la estructura de la cursada antes de subir nada.
///
/// Comparte <see cref="RutasAisladasCollection"/> porque <see cref="BibliotecaService"/>
/// lee y escribe rutas estaticas globales.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class MateriasTests
{
    public MateriasTests() => Limpiar();

    private static BibliotecaService Cargada()
    {
        var servicio = new BibliotecaService();
        servicio.Cargar();
        return servicio;
    }

    // ------------------------------------------------------------------
    // AC — crear una materia con nombre libre
    // ------------------------------------------------------------------

    [Fact]
    public void SeCreaUnaMateriaConNombreLibre()
    {
        var b = Cargada();

        Assert.True(b.CrearMateria("Bioquimica"));
        Assert.Contains(b.NombresDeMaterias, n => n == "Bioquimica");
    }

    [Fact]
    public void UnaMateriaVacia_SobreviveAlReinicio()
    {
        // Es el motivo de que exista materias.json. Si el unico registro de una materia
        // fueran los libros que la usan, crear "Bioquimica" antes de subir el primer apunte
        // no dejaria rastro y al abrir la app de nuevo no estaria.
        var primera = Cargada();
        primera.CrearMateria("Bioquimica");

        var segunda = Cargada();

        Assert.Contains(segunda.NombresDeMaterias, n => n == "Bioquimica");
        Assert.Empty(segunda.LibrosDe("Bioquimica"));
    }

    [Fact]
    public void NoSeCreanDosMateriasQueSoloSeDiferencianEnMayusculas()
    {
        // "Fisiologia" y "fisiologia" son la misma materia. Admitir las dos partiria la
        // biblioteca en dos grupos identicos que el alumno tendria que mantener a mano.
        var b = Cargada();
        b.CrearMateria("Fisiologia");

        Assert.False(b.CrearMateria("fisiologia"));
        Assert.Single(b.Materias, m => m.Nombre.Equals("Fisiologia", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UnNombreVacio_NoCreaMateria(string nombre)
    {
        var b = Cargada();
        int antes = b.Materias.Count;

        Assert.False(b.CrearMateria(nombre));
        Assert.Equal(antes, b.Materias.Count);
    }

    // ------------------------------------------------------------------
    // RN-22 — el material anterior a las materias no queda huerfano
    // ------------------------------------------------------------------

    [Fact]
    public void ElMaterialSinMateria_CaeEnLaMateriaPorDefecto_RN22()
    {
        EscribirLibros("""
            [
              { "Id": "a1", "Titulo": "Apunte viejo", "RutaArchivo": "C:\\x\\a1.pdf",
                "FechaAgregado": "2024-01-01T10:00:00" }
            ]
            """);

        var b = Cargada();

        var libro = Assert.Single(b.Libros);
        Assert.Equal(BibliotecaService.SinMateria, libro.Materia);
        Assert.Contains(b.NombresDeMaterias, n => n == BibliotecaService.SinMateria);
    }

    [Fact]
    public void LaMateriaDeUnLibroViejo_QuedaEnElIndiceAunqueNadieLaHayaCreado()
    {
        // El material anterior a US-023 ya traia materia escrita a mano. Si el indice solo
        // se armara con materias.json, ese libro quedaria en un grupo que la gestion de
        // materias no conoce: no se podria renombrar ni eliminar.
        EscribirLibros("""
            [
              { "Id": "a1", "Titulo": "Guyton", "Materia": "Fisiologia",
                "RutaArchivo": "C:\\x\\a1.pdf", "FechaAgregado": "2024-01-01T10:00:00" }
            ]
            """);

        var b = Cargada();

        Assert.Contains(b.NombresDeMaterias, n => n == "Fisiologia");
    }

    [Fact]
    public void MigrarNoDuplicaNiPierdeMaterial_RN22()
    {
        EscribirLibros("""
            [
              { "Id": "a1", "Titulo": "Uno", "RutaArchivo": "C:\\x\\a1.pdf", "FechaAgregado": "2024-01-01T10:00:00" },
              { "Id": "a2", "Titulo": "Dos", "Materia": "Fisiologia", "RutaArchivo": "C:\\x\\a2.pdf", "FechaAgregado": "2024-01-02T10:00:00" }
            ]
            """);

        var b = Cargada();

        Assert.Equal(2, b.Libros.Count);
        Assert.Equal(new[] { "a1", "a2" }, b.Libros.Select(l => l.Id).OrderBy(i => i));
    }

    // ------------------------------------------------------------------
    // AC — renombrar arrastra los documentos
    // ------------------------------------------------------------------

    [Fact]
    public void AlRenombrar_LosDocumentosSiguenEnLaMateriaRenombrada()
    {
        var b = Cargada();
        b.CrearMateria("Fisio");
        AgregarLibro(b, "Guyton", "Fisio");
        AgregarLibro(b, "Best", "Fisio");

        int movidos = b.RenombrarMateria("Fisio", "Fisiologia");

        Assert.Equal(2, movidos);
        Assert.Contains(b.NombresDeMaterias, n => n == "Fisiologia");
        Assert.DoesNotContain(b.NombresDeMaterias, n => n == "Fisio");
        Assert.Equal(2, b.LibrosDe("Fisiologia").Count());
        Assert.Empty(b.LibrosDe("Fisio"));
    }

    [Fact]
    public void ElRenombre_Persiste()
    {
        var primera = Cargada();
        primera.CrearMateria("Fisio");
        AgregarLibro(primera, "Guyton", "Fisio");
        primera.RenombrarMateria("Fisio", "Fisiologia");

        var segunda = Cargada();

        Assert.Contains(segunda.NombresDeMaterias, n => n == "Fisiologia");
        Assert.Equal("Fisiologia", Assert.Single(segunda.Libros).Materia);
    }

    [Fact]
    public void RenombrarSobreUnaMateriaQueYaExiste_NoFusionaEnSilencio()
    {
        // Permitirlo juntaria dos grupos en uno sin avisar, y despues no habria forma de
        // volver atras: no queda registro de cual documento era de cual materia.
        var b = Cargada();
        b.CrearMateria("Fisiologia");
        b.CrearMateria("Bioquimica");
        AgregarLibro(b, "Guyton", "Fisiologia");

        Assert.Equal(-1, b.RenombrarMateria("Fisiologia", "Bioquimica"));
        Assert.Equal("Fisiologia", Assert.Single(b.Libros).Materia);
    }

    [Fact]
    public void LaMateriaPorDefecto_NoSeRenombra()
    {
        // Es el destino de RN-22 y de la reasignacion al borrar otra materia: si cambiara
        // de nombre, el material sin clasificar se quedaria sin cajon adonde caer.
        var b = Cargada();

        Assert.Equal(-1, b.RenombrarMateria(BibliotecaService.SinMateria, "General"));
        Assert.Contains(b.NombresDeMaterias, n => n == BibliotecaService.SinMateria);
    }

    // ------------------------------------------------------------------
    // AC — al eliminar, los documentos nunca se borran en silencio
    // ------------------------------------------------------------------

    [Fact]
    public void AlEliminarConservandoDocumentos_PasanALaMateriaPorDefecto()
    {
        var b = Cargada();
        b.CrearMateria("Fisiologia");
        AgregarLibro(b, "Guyton", "Fisiologia");

        int adentro = b.EliminarMateria("Fisiologia", borrarDocumentos: false);

        Assert.Equal(1, adentro);
        Assert.DoesNotContain(b.NombresDeMaterias, n => n == "Fisiologia");

        var libro = Assert.Single(b.Libros);
        Assert.Equal(BibliotecaService.SinMateria, libro.Materia);
    }

    [Fact]
    public void AlEliminarBorrandoDocumentos_SeVanTambienLosLibros()
    {
        var b = Cargada();
        b.CrearMateria("Fisiologia");
        AgregarLibro(b, "Guyton", "Fisiologia");
        AgregarLibro(b, "Otro", "Bioquimica");

        b.EliminarMateria("Fisiologia", borrarDocumentos: true);

        var queda = Assert.Single(b.Libros);
        Assert.Equal("Otro", queda.Titulo);
    }

    [Fact]
    public void EliminarUnaMateria_NoTocaLosLibrosDeLasDemas()
    {
        var b = Cargada();
        b.CrearMateria("Fisiologia");
        b.CrearMateria("Bioquimica");
        AgregarLibro(b, "Guyton", "Fisiologia");
        AgregarLibro(b, "Lehninger", "Bioquimica");

        b.EliminarMateria("Fisiologia", borrarDocumentos: true);

        Assert.Equal("Bioquimica", Assert.Single(b.Libros).Materia);
        Assert.Contains(b.NombresDeMaterias, n => n == "Bioquimica");
    }

    [Fact]
    public void LaMateriaPorDefecto_NoSeElimina()
    {
        var b = Cargada();

        Assert.Equal(-1, b.EliminarMateria(BibliotecaService.SinMateria, borrarDocumentos: false));
        Assert.Contains(b.NombresDeMaterias, n => n == BibliotecaService.SinMateria);
    }

    // ------------------------------------------------------------------
    // Orden de la lista
    // ------------------------------------------------------------------

    [Fact]
    public void LaMateriaPorDefecto_QuedaUltima()
    {
        // Es un cajon de pendientes, no una materia mas: ordenada por nombre caeria en el
        // medio de la lista, entre dos materias reales.
        var b = Cargada();
        b.CrearMateria("Zoologia");
        b.CrearMateria("Anatomia");

        Assert.Equal(BibliotecaService.SinMateria, b.Materias[^1].Nombre);
        Assert.Equal(new[] { "Anatomia", "Zoologia" }, b.Materias.Take(2).Select(m => m.Nombre));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static void AgregarLibro(BibliotecaService b, string titulo, string materia)
    {
        b.Libros.Add(new Libro { Titulo = titulo, Materia = materia });
        b.CrearMateria(materia);
        b.Guardar();
    }

    private static void EscribirLibros(string json)
    {
        RutasApp.AsegurarCarpetas();
        File.WriteAllText(RutasApp.ArchivoLibros, json);
    }

    private static void Limpiar()
    {
        RutasApp.AsegurarCarpetas();

        foreach (string archivo in new[] { RutasApp.ArchivoLibros, RutasApp.ArchivoMaterias })
        {
            if (File.Exists(archivo))
            {
                File.Delete(archivo);
            }
        }

        foreach (var entrada in Directory.GetFileSystemEntries(RutasApp.Biblioteca))
        {
            if (File.Exists(entrada))
            {
                File.Delete(entrada);
            }
            else
            {
                Directory.Delete(entrada, recursive: true);
            }
        }
    }
}
