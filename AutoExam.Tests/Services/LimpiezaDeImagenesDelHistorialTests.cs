using System.IO;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-025 / US-018 — las imagenes de un examen que sigue en el historial no se borran.
///
/// La limpieza periodica borraba por antiguedad TODA carpeta de imagenes de mas de siete
/// dias, sin mirar si el examen seguia en el historial. Con eso, el criterio de US-025 ("la
/// imagen sigue disponible ahi") y el de US-018 ("las imagenes siguen disponibles para ver la
/// correccion con el mismo contexto visual") se cumplian una semana y despues dejaban el
/// detalle con el hueco de una figura que ya no existia, sin ningun aviso.
///
/// Lo que la limpieza si tiene que seguir barriendo son las carpetas huerfanas: intentos que
/// se abandonaron sin registrarse, que es de donde venia el crecimiento que ataca.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class LimpiezaDeImagenesDelHistorialTests
{
    public LimpiezaDeImagenesDelHistorialTests() => Limpiar();

    /// <summary>Carpeta de imagenes de un examen, envejecida a mano.</summary>
    private static string CarpetaVieja(string examenId, int dias)
    {
        string carpeta = RutasApp.CarpetaImagenesExamen(examenId);
        File.WriteAllText(Path.Combine(carpeta, "fig_01.png"), "figura");
        Directory.SetCreationTime(carpeta, DateTime.Now.AddDays(-dias));
        return carpeta;
    }

    [Fact]
    public void LasImagenesDeUnExamenDelHistorial_NoSeBorranPorAntiguedad()
    {
        string carpeta = CarpetaVieja("examenEnHistorial", dias: 30);

        RutasApp.LimpiarImagenesAntiguas(new[] { "examenEnHistorial" });

        Assert.True(Directory.Exists(carpeta),
            "Se borro la carpeta de imagenes de un examen que sigue en el historial: su detalle " +
            "quedaria con figuras rotas (US-025 / US-018).");
    }

    [Fact]
    public void LasImagenesHuerfanasViejas_SiSeBorran()
    {
        // Un intento que se abandono sin registrarse. Nadie lo va a volver a mirar y es de
        // donde venia el crecimiento de AppData que esta limpieza ataca.
        string carpeta = CarpetaVieja("intentoAbandonado", dias: 30);

        RutasApp.LimpiarImagenesAntiguas(new[] { "otroExamen" });

        Assert.False(Directory.Exists(carpeta));
    }

    [Fact]
    public void LasImagenesRecientes_NoSeBorranAunqueNoEstenEnElHistorial()
    {
        // Comportamiento de siempre: puede ser el examen que se esta rindiendo ahora mismo,
        // que todavia no se registro porque no se finalizo.
        string carpeta = CarpetaVieja("reciente", dias: 1);

        RutasApp.LimpiarImagenesAntiguas(Array.Empty<string>());

        Assert.True(Directory.Exists(carpeta));
    }

    [Fact]
    public void SinListaDeExamenes_LaLimpiezaSigueFuncionandoComoAntes()
    {
        // La firma admite omitir la lista: no puede reventar, solo dejar de proteger.
        string carpeta = CarpetaVieja("cualquiera", dias: 30);

        RutasApp.LimpiarImagenesAntiguas();

        Assert.False(Directory.Exists(carpeta));
    }

    [Fact]
    public void LaComparacionDeIds_NoDistingueMayusculas()
    {
        // Los ids son GUID en hexadecimal: dependiendo de quien los escriba pueden venir en
        // otra caja, y una carpeta protegida que no se reconoce se borra igual.
        string carpeta = CarpetaVieja("ABCDEF123456", dias: 30);

        RutasApp.LimpiarImagenesAntiguas(new[] { "abcdef123456" });

        Assert.True(Directory.Exists(carpeta));
    }

    private static void Limpiar()
    {
        RutasApp.AsegurarCarpetas();

        foreach (var dir in Directory.GetDirectories(RutasApp.Imagenes))
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
