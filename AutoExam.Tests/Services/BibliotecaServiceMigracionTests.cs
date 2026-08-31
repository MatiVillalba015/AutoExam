using System.IO;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services;

/// <summary>
/// M3 — back-fill de <c>libros.json</c> pre-cambio al levantar el índice
/// (specs/03-architecture.md Incremento 4 §3: "Migración en BibliotecaService.Cargar:
/// registro sin Archivos → Archivos = [RutaArchivo]; sin Tipo → Pdf" y §4.2 "Cargar(): back-fill
/// de Archivos / Tipo para registros viejos").
///
/// Ejercita el camino real de arranque de la app (<see cref="BibliotecaService.Cargar"/> contra
/// <see cref="RutasApp.ArchivoLibros"/>), a diferencia de <c>LibroFuenteGeneralizadaTests</c> que
/// prueba la deserialización cruda por <c>JsonStore</c>. Sin este back-fill, un usuario que
/// actualiza desde una versión previa vería sus PDFs sin <c>Archivos</c> → la generación y el
/// borrado (que ahora iteran <c>Archivos</c>) fallarían.
///
/// Comparte <see cref="RutasAisladasCollection"/>: <see cref="BibliotecaService"/> lee/escribe la
/// ruta estática global <see cref="RutasApp.ArchivoLibros"/>.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class BibliotecaServiceMigracionTests
{
    public BibliotecaServiceMigracionTests() => LimpiarBiblioteca();

    [Fact]
    public void Cargar_registro_viejo_sin_tipo_ni_archivos_backfillea_Pdf_y_una_ruta()
    {
        EscribirLibrosJson(/*lang=json,strict*/ """
            [
              {
                "Id": "viejo1",
                "Titulo": "Fisiología",
                "Materia": "Fisiología",
                "RutaArchivo": "C:\\datos\\Biblioteca\\viejo1.pdf",
                "NombreArchivoOriginal": "fisio.pdf",
                "CantidadPaginas": 88,
                "FechaAgregado": "2024-03-01T10:00:00",
                "Modulos": [ { "Nombre": "Cap 1", "DesdePagina": 1, "HastaPagina": 20 } ]
              }
            ]
            """);

        var servicio = new BibliotecaService();
        servicio.Cargar();

        var libro = Assert.Single(servicio.Libros);
        Assert.Equal(TipoFuente.Pdf, libro.Tipo);
        Assert.Equal(new[] { @"C:\datos\Biblioteca\viejo1.pdf" }, libro.Archivos);
        Assert.Equal(libro.RutaArchivo, libro.Archivos[0]);
        // Nada más se toca.
        Assert.Equal("Fisiología", libro.Titulo);
        Assert.Equal(88, libro.CantidadPaginas);
        Assert.Single(libro.Modulos);
    }

    [Fact]
    public void Cargar_registro_viejo_con_archivos_null_explicito_tambien_se_backfillea()
    {
        EscribirLibrosJson(/*lang=json,strict*/ """
            [
              {
                "Id": "viejo2",
                "Titulo": "Histología",
                "RutaArchivo": "C:\\datos\\Biblioteca\\viejo2.pdf",
                "Archivos": null,
                "Modulos": null,
                "FechaAgregado": "2024-01-01T09:00:00"
              }
            ]
            """);

        var servicio = new BibliotecaService();
        servicio.Cargar();

        var libro = Assert.Single(servicio.Libros);
        Assert.Equal(TipoFuente.Pdf, libro.Tipo);
        Assert.NotNull(libro.Archivos);
        Assert.Equal(new[] { @"C:\datos\Biblioteca\viejo2.pdf" }, libro.Archivos);
    }

    [Fact]
    public void Cargar_no_pisa_una_fuente_ya_migrada_multi_imagen()
    {
        // Un libros.json ya en formato nuevo (set de imágenes) debe cargar tal cual: el back-fill
        // solo actúa sobre lo que falta, no reescribe lo que ya vino completo.
        EscribirLibrosJson(/*lang=json,strict*/ """
            [
              {
                "Id": "set9",
                "Titulo": "Fotos de la clase",
                "Tipo": 4,
                "RutaArchivo": "C:\\datos\\Biblioteca\\set9\\01.png",
                "Archivos": [
                  "C:\\datos\\Biblioteca\\set9\\01.png",
                  "C:\\datos\\Biblioteca\\set9\\02.jpg",
                  "C:\\datos\\Biblioteca\\set9\\03.png"
                ],
                "MedidaTamanio": "3 imágenes",
                "FechaAgregado": "2025-06-01T12:00:00"
              }
            ]
            """);

        var servicio = new BibliotecaService();
        servicio.Cargar();

        var libro = Assert.Single(servicio.Libros);
        Assert.Equal(TipoFuente.SetImagenes, libro.Tipo);
        Assert.Equal(3, libro.Archivos.Count);
        Assert.EndsWith("01.png", libro.Archivos[0]);
        Assert.EndsWith("03.png", libro.Archivos[2]);
        Assert.Equal("3 imágenes", libro.MedidaTamanio);
    }

    [Fact]
    public void Cargar_y_Guardar_persiste_el_backfill_para_la_proxima_apertura()
    {
        EscribirLibrosJson(/*lang=json,strict*/ """
            [
              { "Id": "viejo3", "Titulo": "Patología",
                "RutaArchivo": "C:\\datos\\Biblioteca\\viejo3.pdf",
                "FechaAgregado": "2024-02-02T08:00:00" }
            ]
            """);

        var primera = new BibliotecaService();
        primera.Cargar();
        primera.Guardar();

        var segunda = new BibliotecaService();
        segunda.Cargar();

        var libro = Assert.Single(segunda.Libros);
        Assert.Equal(TipoFuente.Pdf, libro.Tipo);
        Assert.Equal(new[] { @"C:\datos\Biblioteca\viejo3.pdf" }, libro.Archivos);
    }

    private static void EscribirLibrosJson(string json)
    {
        RutasApp.AsegurarCarpetas();
        File.WriteAllText(RutasApp.ArchivoLibros, json);
    }

    private static void LimpiarBiblioteca()
    {
        RutasApp.AsegurarCarpetas();
        if (File.Exists(RutasApp.ArchivoLibros))
        {
            File.Delete(RutasApp.ArchivoLibros);
        }
        foreach (var entrada in Directory.GetFileSystemEntries(RutasApp.Biblioteca))
        {
            if (File.Exists(entrada)) File.Delete(entrada);
            else Directory.Delete(entrada, recursive: true);
        }
    }
}
