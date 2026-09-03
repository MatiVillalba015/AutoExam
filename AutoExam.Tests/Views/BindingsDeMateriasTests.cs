using System.Reflection;
using AutoExam.ViewModels;

namespace AutoExam.Tests.Views;

/// <summary>
/// Los enlaces que agregaron US-023 y US-024 apuntan a miembros que existen.
///
/// Un Binding a una propiedad mal escrita no rompe la compilacion ni tira una excepcion: WPF
/// lo anota en la traza de depuracion y deja el control mudo. En esta pantalla eso significa
/// que los chips de materia no se marcan, que la lista no se agrupa o que el boton de generar
/// nunca se habilita, todo sin un solo error visible. Estos tests son la red que falta.
/// </summary>
public class BindingsDeMateriasTests
{
    private static void DebeTener(Type tipo, string miembro)
    {
        bool existe = tipo.GetProperty(miembro, BindingFlags.Public | BindingFlags.Instance) is not null;

        Assert.True(existe,
            $"{tipo.Name} no expone \"{miembro}\", pero la vista lo enlaza: el control quedaria mudo sin ningun error visible.");
    }

    [Theory]
    // US-023: gestion de materias y lista agrupada.
    [InlineData("Materias")]
    [InlineData("MateriaElegida")]
    [InlineData("MateriaNueva")]
    [InlineData("EsMateriaEditable")]
    [InlineData("ResumenMateriaElegida")]
    [InlineData("LibrosPorMateria")]
    [InlineData("CrearMateriaCommand")]
    [InlineData("RenombrarMateriaCommand")]
    [InlineData("EliminarMateriaCommand")]
    [InlineData("ElegirMateriaCommand")]
    public void BibliotecaViewModel_ExponeLoQueEnlazaSuVista(string miembro)
        => DebeTener(typeof(BibliotecaViewModel), miembro);

    [Theory]
    // US-024: filtro por materia y seleccion multiple.
    [InlineData("Materias")]
    [InlineData("MateriaElegida")]
    [InlineData("LibrosDeLaMateria")]
    [InlineData("Seleccionados")]
    [InlineData("EsExamenCombinado")]
    [InlineData("ResumenSeleccion")]
    [InlineData("ElegirMateriaCommand")]
    public void AsistenteViewModel_ExponeLoQueEnlazaSuVista(string miembro)
        => DebeTener(typeof(AsistenteViewModel), miembro);

    [Fact]
    public void ElLibro_ExponeLaMarcaDeSeleccionQueUsaLaCasilla()
        => DebeTener(typeof(AutoExam.Models.Libro), "Seleccionado");

    [Fact]
    public void LaVistaAgrupada_NoEsLaMismaColeccionPlanaDeLibros()
    {
        // Si LibrosPorMateria devolviera la coleccion cruda, el ListBox no agruparia nada y
        // la pantalla se veria igual que antes de US-023.
        var propiedad = typeof(BibliotecaViewModel)
            .GetProperty("LibrosPorMateria", BindingFlags.Public | BindingFlags.Instance)!;

        Assert.True(typeof(System.ComponentModel.ICollectionView).IsAssignableFrom(propiedad.PropertyType),
            "LibrosPorMateria tiene que ser una vista de coleccion para poder agrupar (US-023).");
    }
}
