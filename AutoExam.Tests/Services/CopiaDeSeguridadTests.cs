using System.IO;
using System.IO.Compression;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-051 — copia de seguridad manual y local.
///
/// Dos garantías se prueban acá por encima del resto, porque son las que convierten un
/// respaldo en una pérdida de datos:
/// <list type="bullet">
///   <item>Un archivo que no es una copia de AutoExam se rechaza SIN haber tocado nada. El
///   criterio lo pide con todas las letras ("muestra un error claro y no modifica ningún dato
///   existente"), y es fácil de romper: alcanza con borrar antes de validar.</item>
///   <item>Ninguna entrada del ZIP puede escribir fuera de la carpeta de datos. Un .axcopia
///   es un archivo que puede llegar de cualquier lado, y una entrada con ".." escribiría en el
///   disco del que importa.</item>
/// </list>
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class CopiaDeSeguridadTests
{
    private static string Temporal(string extension) =>
        Path.Combine(Path.GetTempPath(), $"autoexam-test-{Guid.NewGuid():N}{extension}");

    private static void SembrarDatos(string tituloDelLibro)
    {
        RutasApp.AsegurarCarpetas();

        File.WriteAllText(RutasApp.ArchivoLibros, $"[{{\"Titulo\":\"{tituloDelLibro}\"}}]");
        File.WriteAllText(RutasApp.ArchivoPerfil, "{\"Nombre\":\"Estudiante\"}");
        File.WriteAllText(RutasApp.ArchivoConfig, "{\"Modelo\":\"gemini-1.5-flash\"}");
        File.WriteAllBytes(Path.Combine(RutasApp.Biblioteca, "material.pdf"), new byte[512]);
    }

    // ------------------------------------------------------------------
    // AC — exportar e importar, ida y vuelta
    // ------------------------------------------------------------------

    [Fact]
    public void ExportarEImportar_DevuelvenLosDatosTalComoEstaban_US051()
    {
        string copia = Temporal(CopiaDeSeguridadService.Extension);

        try
        {
            SembrarDatos("Fisiologia renal");
            CopiaDeSeguridadService.Exportar(copia, libros: 1, examenes: 0);

            // Se pisa todo con otra cosa, como si fuera otra computadora.
            SembrarDatos("Otra biblioteca distinta");

            CopiaDeSeguridadService.Importar(copia);

            Assert.Contains("Fisiologia renal", File.ReadAllText(RutasApp.ArchivoLibros), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(RutasApp.Biblioteca, "material.pdf")),
                "El PDF de la biblioteca no volvió con la copia.");
        }
        finally
        {
            File.Delete(copia);
        }
    }

    [Fact]
    public void LaCopia_LlevaLibrosHistorialYConfiguracion_EnUnSoloArchivo_US051()
    {
        string copia = Temporal(CopiaDeSeguridadService.Extension);

        try
        {
            SembrarDatos("Un libro");
            CopiaDeSeguridadService.Exportar(copia, libros: 1, examenes: 3);

            using var archivo = ZipFile.OpenRead(copia);
            var nombres = archivo.Entries.Select(e => e.FullName).ToList();

            // Historial (perfil.json) y configuración con las claves y los colores por materia:
            // el criterio enumera los tres, y exportar dos de tres es un respaldo que engaña.
            Assert.Contains("libros.json", nombres);
            Assert.Contains("perfil.json", nombres);
            Assert.Contains("config.json", nombres);
            Assert.Contains(nombres, n => n.StartsWith("Biblioteca/", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(copia);
        }
    }

    [Fact]
    public void Inspeccionar_DiceQueTraeLaCopia_AntesDeTocarNada_US051()
    {
        string copia = Temporal(CopiaDeSeguridadService.Extension);

        try
        {
            SembrarDatos("Un libro");
            CopiaDeSeguridadService.Exportar(copia, libros: 4, examenes: 7);

            var resumen = CopiaDeSeguridadService.Inspeccionar(copia);

            Assert.Equal(4, resumen.Libros);
            Assert.Equal(7, resumen.Examenes);
            Assert.Equal(ActualizacionService.VersionActual, resumen.Version);
        }
        finally
        {
            File.Delete(copia);
        }
    }

    [Fact]
    public void ElNombreSugerido_LlevaLaExtensionPropia_US051()
    {
        Assert.EndsWith(CopiaDeSeguridadService.Extension, CopiaDeSeguridadService.NombreSugerido(),
            StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // AC — un archivo inválido no modifica nada
    // ------------------------------------------------------------------

    [Fact]
    public void UnArchivoQueNoEsUnZip_SeRechaza_YNoBorraNada_US051()
    {
        string falso = Temporal(".axcopia");
        File.WriteAllText(falso, "esto no es un zip");

        try
        {
            SembrarDatos("Mi biblioteca");

            Assert.Throws<CopiaInvalidaException>(() => CopiaDeSeguridadService.Importar(falso));

            Assert.Contains("Mi biblioteca", File.ReadAllText(RutasApp.ArchivoLibros), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(RutasApp.Biblioteca, "material.pdf")),
                "Un archivo inválido se llevó puesta la biblioteca (US-051).");
        }
        finally
        {
            File.Delete(falso);
        }
    }

    [Fact]
    public void UnZipDeOtraApp_SeRechazaPorFaltarleElManifiesto_YNoBorraNada_US051()
    {
        string ajeno = Temporal(".axcopia");

        using (var archivo = ZipFile.Open(ajeno, ZipArchiveMode.Create))
        {
            archivo.CreateEntry("cualquier-cosa.txt");
        }

        try
        {
            SembrarDatos("Mi biblioteca");

            var ex = Assert.Throws<CopiaInvalidaException>(() => CopiaDeSeguridadService.Importar(ajeno));

            Assert.Contains("AutoExam", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Mi biblioteca", File.ReadAllText(RutasApp.ArchivoLibros), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(ajeno);
        }
    }

    /// <summary>
    /// Un .axcopia puede venir de cualquier lado — de un pendrive, de un chat. Una entrada con
    /// ".." escribiría fuera de la carpeta de datos, en cualquier lugar del disco al que llegue
    /// el permiso del usuario. Se rechaza la copia entera, y antes de borrar nada.
    /// </summary>
    [Fact]
    public void UnaCopiaConRutasQueSalenDeLaCarpetaDeDatos_SeRechaza_US051()
    {
        string malicioso = Temporal(".axcopia");

        using (var archivo = ZipFile.Open(malicioso, ZipArchiveMode.Create))
        {
            using (var manifiesto = new StreamWriter(archivo.CreateEntry("autoexam-copia.json").Open()))
            {
                manifiesto.Write("{\"Version\":\"1.0.0\",\"Fecha\":\"2026-01-01T00:00:00\",\"Libros\":1,\"Examenes\":0}");
            }

            using var fuga = new StreamWriter(archivo.CreateEntry("../../fuera.txt").Open());
            fuga.Write("no deberia escribirse");
        }

        try
        {
            SembrarDatos("Mi biblioteca");

            Assert.Throws<CopiaInvalidaException>(() => CopiaDeSeguridadService.Importar(malicioso));

            Assert.Contains("Mi biblioteca", File.ReadAllText(RutasApp.ArchivoLibros), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(malicioso);
        }
    }

    // ------------------------------------------------------------------
    // RN-59 — siempre local
    // ------------------------------------------------------------------

    /// <summary>
    /// El servicio no conoce ninguna URL ni ningún cliente HTTP: no hay dónde subir la copia
    /// aunque alguien quisiera. Se verifica sobre el código fuente porque es una garantía
    /// sobre lo que el archivo NO hace, y eso no se puede observar ejecutándolo.
    /// </summary>
    [Fact]
    public void LaCopia_NoSeSubeANingunLado_RN59()
    {
        string fuente = File.ReadAllText(
            ArchivoFuenteHelper.RutaFuente("AutoExam/Services/CopiaDeSeguridadService.cs"));

        foreach (string rastro in new[] { "HttpClient", "http://", "https://", "WebClient", "UploadFile" })
        {
            Assert.False(fuente.Contains(rastro, StringComparison.OrdinalIgnoreCase),
                $"CopiaDeSeguridadService menciona \"{rastro}\": la copia tiene que ser siempre local (RN-59).");
        }
    }
}
