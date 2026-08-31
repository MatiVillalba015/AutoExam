using System.IO;
using AutoExam.Models;
using AutoExam.Services;

namespace AutoExam.Tests.Models;

/// <summary>
/// M3 — generalización de <see cref="Libro"/> a fuente multi-formato / multi-archivo
/// (specs/03-architecture.md Incremento 4 §3 y §4.2; specs/02-tech-spec.md "Entidades de datos",
/// fila Fuente/Material).
///
/// Nivel JSON puro: contrato del modelo (nombres, tipos, defaults) y retrocompatibilidad de
/// deserialización de un <c>libros.json</c> pre-cambio, sin pasar por <see cref="BibliotecaService"/>
/// ni por rutas de disco reales. La otra mitad — que <c>BibliotecaService.Cargar()</c> hace el
/// back-fill al levantar el índice — vive en <c>BibliotecaServiceMigracionTests</c>, que ejercita
/// el camino real de arranque de la app y no se solapa con este archivo.
///
/// Contract-first: referencia <see cref="TipoFuente"/> y los campos nuevos de <see cref="Libro"/>
/// tal como los fija la arquitectura; el build es el gate de que el developer implementó el
/// contrato exacto.
/// </summary>
public class LibroFuenteGeneralizadaTests
{
    // ------------------------------------------------------------------
    // Forma de Libro — defaults del contrato (§3: "TipoFuente Tipo (default Pdf),
    // List<string> Archivos, string MedidaTamanio; RutaArchivo se conserva")
    // ------------------------------------------------------------------

    [Fact]
    public void Libro_nuevo_trae_los_defaults_del_contrato()
    {
        var libro = new Libro();

        Assert.Equal(TipoFuente.Pdf, libro.Tipo);
        Assert.NotNull(libro.Archivos);
        Assert.Empty(libro.Archivos);
        Assert.Equal(string.Empty, libro.MedidaTamanio);
    }

    [Fact]
    public void TipoFuente_default_del_enum_es_Pdf()
    {
        // El default (valor 0) tiene que ser Pdf: un registro viejo sin la clave "tipo"
        // deserializa a 0 y debe quedar como PDF sin lógica extra (§4.1).
        Assert.Equal(TipoFuente.Pdf, default(TipoFuente));
    }

    // ------------------------------------------------------------------
    // Round-trip por JsonStore (mismo serializador que usa libros.json)
    // ------------------------------------------------------------------

    [Fact]
    public void JsonStore_round_trip_de_una_fuente_pdf_de_archivo_unico()
    {
        string ruta = RutaTemporal();
        try
        {
            var original = new Libro
            {
                Id = "abc123",
                Titulo = "Apunte de Anatomía",
                Materia = "Anatomía",
                Tipo = TipoFuente.Pdf,
                RutaArchivo = @"C:\datos\Biblioteca\abc123.pdf",
                Archivos = new List<string> { @"C:\datos\Biblioteca\abc123.pdf" },
                CantidadPaginas = 120,
                MedidaTamanio = "120 páginas",
                Modulos = { new Modulo { Nombre = "Unidad 1", DesdePagina = 1, HastaPagina = 40 } },
            };

            JsonStore.Guardar(ruta, new List<Libro> { original });
            var recargado = Assert.Single(JsonStore.Cargar(ruta, () => new List<Libro>()));

            Assert.Equal(TipoFuente.Pdf, recargado.Tipo);
            Assert.Equal(original.RutaArchivo, recargado.RutaArchivo);
            Assert.Equal(original.Archivos, recargado.Archivos);
            Assert.Equal("120 páginas", recargado.MedidaTamanio);
            Assert.Equal(120, recargado.CantidadPaginas);
            Assert.Single(recargado.Modulos);
        }
        finally { BorrarSiExiste(ruta); }
    }

    [Fact]
    public void JsonStore_round_trip_de_un_set_de_imagenes_conserva_el_orden()
    {
        string ruta = RutaTemporal();
        try
        {
            var archivos = new List<string>
            {
                @"C:\datos\Biblioteca\set9\01.png",
                @"C:\datos\Biblioteca\set9\02.jpg",
                @"C:\datos\Biblioteca\set9\03.png",
            };
            var original = new Libro
            {
                Id = "set9",
                Titulo = "Fotos de la clase",
                Tipo = TipoFuente.SetImagenes,
                Archivos = new List<string>(archivos),
                RutaArchivo = archivos[0],
                MedidaTamanio = "3 imágenes",
            };

            JsonStore.Guardar(ruta, new List<Libro> { original });
            var recargado = Assert.Single(JsonStore.Cargar(ruta, () => new List<Libro>()));

            Assert.Equal(TipoFuente.SetImagenes, recargado.Tipo);
            Assert.Equal(archivos, recargado.Archivos); // igualdad de secuencia: orden incluido
            Assert.Equal(archivos[0], recargado.RutaArchivo);
        }
        finally { BorrarSiExiste(ruta); }
    }

    // ------------------------------------------------------------------
    // Retrocompatibilidad: un libros.json escrito ANTES del cambio (pre-M3) no tiene
    // "tipo" ni "archivos". Deserializa sin romper y con defaults sanos; el back-fill de
    // Archivos = [RutaArchivo] lo hace Cargar() (ver BibliotecaServiceMigracionTests).
    // ------------------------------------------------------------------

    [Fact]
    public void Libros_json_legacy_sin_tipo_ni_archivos_deserializa_sin_excepcion()
    {
        string ruta = RutaTemporal();
        try
        {
            File.WriteAllText(ruta, /*lang=json,strict*/ """
                [
                  {
                    "Id": "viejo1",
                    "Titulo": "Fisiología",
                    "Materia": "Fisiología",
                    "RutaArchivo": "C:\\datos\\Biblioteca\\viejo1.pdf",
                    "NombreArchivoOriginal": "fisio.pdf",
                    "CantidadPaginas": 88,
                    "Modulos": [
                      { "Nombre": "Cap 1", "DesdePagina": 1, "HastaPagina": 20 }
                    ]
                  }
                ]
                """);

            var libro = Assert.Single(JsonStore.Cargar(ruta, () => new List<Libro>()));

            // Campos ya existentes: intactos.
            Assert.Equal("viejo1", libro.Id);
            Assert.Equal("Fisiología", libro.Titulo);
            Assert.Equal(88, libro.CantidadPaginas);
            Assert.Single(libro.Modulos);
            // Campos nuevos: caen al default del modelo, sin excepción.
            Assert.Equal(TipoFuente.Pdf, libro.Tipo);
            Assert.Equal(string.Empty, libro.MedidaTamanio);
            Assert.NotNull(libro.Archivos); // el back-fill a [RutaArchivo] es responsabilidad de Cargar()
        }
        finally { BorrarSiExiste(ruta); }
    }

    [Fact]
    public void Libros_json_legacy_con_archivos_null_explicito_no_rompe_la_carga()
    {
        // System.Text.Json pisa el inicializador `= new()` con null si la clave está presente
        // con valor null. Cargar() debe tolerarlo igual que la clave ausente.
        string ruta = RutaTemporal();
        try
        {
            File.WriteAllText(ruta, /*lang=json,strict*/ """
                [
                  {
                    "Id": "viejo2",
                    "Titulo": "Histología",
                    "RutaArchivo": "C:\\datos\\Biblioteca\\viejo2.pdf",
                    "Archivos": null,
                    "Modulos": null
                  }
                ]
                """);

            var libro = Assert.Single(JsonStore.Cargar(ruta, () => new List<Libro>()));

            Assert.Equal("viejo2", libro.Id);
            Assert.Equal(TipoFuente.Pdf, libro.Tipo);
            // No se valida aquí que Archivos ya esté back-filleado (eso es Cargar()); sí que la
            // deserialización cruda no lanzó y devolvió el registro.
        }
        finally { BorrarSiExiste(ruta); }
    }

    private static string RutaTemporal() =>
        Path.Combine(Path.GetTempPath(), $"autoexam-tests-libro-{Guid.NewGuid():N}.json");

    private static void BorrarSiExiste(string ruta)
    {
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
        }
    }
}
