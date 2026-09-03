using System.IO;
using System.Text.Json;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Models;

/// <summary>
/// US-027 / RN-30 / RN-31 — cada materia tiene un color propio.
///
/// La decisión que estos tests protegen es dónde vive el color. RN-30 lo dice sin margen: es
/// un atributo de la materia, no del examen. Esa diferencia es la que hace que cambiarle el
/// color a "Fisiología" repinte también los exámenes de fisiología rendidos hace meses; si el
/// color se copiara en cada examen al generarlo, cada intento quedaría congelado con el color
/// que la materia tenía ese día y el historial se volvería un arcoíris sin sentido.
/// </summary>
public class ColorDeMateriaTests
{
    // ------------------------------------------------------------------
    // RN-31 — paleta cerrada y accesible
    // ------------------------------------------------------------------

    [Fact]
    public void LaPaleta_TieneVariosColoresParaElegir()
    {
        Assert.True(PaletaMaterias.Colores.Count >= 6,
            "Con muy pocos colores, dos materias creadas seguidas salen del mismo color.");
    }

    [Fact]
    public void TodosLosColores_SonHexValido()
    {
        Assert.All(PaletaMaterias.Colores, c =>
        {
            Assert.StartsWith("#", c, StringComparison.Ordinal);
            Assert.Equal(7, c.Length);
        });
    }

    [Fact]
    public void NoHayColoresRepetidosEnLaPaleta()
    {
        Assert.Equal(
            PaletaMaterias.Colores.Count,
            PaletaMaterias.Colores.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void UnColorInventado_NoSeAceptaComoDeLaPaleta_RN31()
    {
        // RN-31: los colores salen de una paleta predefinida, no de un selector RGB libre.
        // Sin esta validación, un materias.json editado a mano metería cualquier tono.
        Assert.False(PaletaMaterias.EsDeLaPaleta("#FF0000"));
        Assert.False(PaletaMaterias.EsDeLaPaleta(""));
        Assert.False(PaletaMaterias.EsDeLaPaleta(null));

        Assert.True(PaletaMaterias.EsDeLaPaleta(PaletaMaterias.Colores[0]));
    }

    [Fact]
    public void NingunColorDeMateria_EsRojoNiVerdePuro()
    {
        // Verde y rojo ya significan "correcta" e "incorrecta" y RN-27 pide no tocar ese
        // significado. Una materia en verde bandera al lado de una respuesta correcta compite
        // con la única lectura de color que importa mientras se corrige.
        foreach (string hex in PaletaMaterias.Colores)
        {
            int r = System.Convert.ToInt32(hex.Substring(1, 2), 16);
            int g = System.Convert.ToInt32(hex.Substring(3, 2), 16);
            int b = System.Convert.ToInt32(hex.Substring(5, 2), 16);

            bool rojoPuro = r > 200 && g < 80 && b < 80;
            bool verdePuro = g > 200 && r < 80 && b < 80;

            Assert.False(rojoPuro || verdePuro,
                $"El color {hex} compite con la semántica de correcto/incorrecto (RN-27).");
        }
    }

    // ------------------------------------------------------------------
    // US-027 — asignación automática
    // ------------------------------------------------------------------

    [Fact]
    public void SeProponePrimeroUnColorQueNadieUsa()
    {
        var existentes = new[]
        {
            new Materia { Nombre = "A", Color = PaletaMaterias.Colores[0] },
            new Materia { Nombre = "B", Color = PaletaMaterias.Colores[1] }
        };

        string siguiente = PaletaMaterias.SiguienteLibre(existentes);

        Assert.Equal(PaletaMaterias.Colores[2], siguiente);
    }

    [Fact]
    public void ConLaPaletaAgotada_SigueDevolviendoUnColor()
    {
        // US-027 no prohíbe repetir: sólo pide sugerir primero los libres. Con más materias
        // que colores tiene que seguir asignando alguno, nunca vacío.
        var todas = PaletaMaterias.Colores
            .Select((c, i) => new Materia { Nombre = $"M{i}", Color = c })
            .ToList();

        string siguiente = PaletaMaterias.SiguienteLibre(todas);

        Assert.True(PaletaMaterias.EsDeLaPaleta(siguiente));
    }

    // ------------------------------------------------------------------
    // RN-30 — el color se resuelve por nombre, al dibujar
    // ------------------------------------------------------------------

    [Fact]
    public void ElColorSeResuelvePorNombreDeMateria()
    {
        PaletaMaterias.Registrar(new[]
        {
            new Materia { Nombre = "Fisiologia", Color = PaletaMaterias.Colores[3] }
        });

        Assert.Equal(PaletaMaterias.Colores[3], PaletaMaterias.ColorDe("Fisiologia"));
    }

    [Fact]
    public void ElNombreDeMateria_SeResuelveSinDistinguirMayusculas()
    {
        PaletaMaterias.Registrar(new[]
        {
            new Materia { Nombre = "Fisiologia", Color = PaletaMaterias.Colores[3] }
        });

        Assert.Equal(PaletaMaterias.Colores[3], PaletaMaterias.ColorDe("FISIOLOGIA"));
    }

    [Fact]
    public void UnaMateriaDesconocida_CaeEnElNeutroSinRomper()
    {
        // Es el caso de un examen viejo cuya materia se borró: tiene que dibujarse igual.
        PaletaMaterias.Registrar(Array.Empty<Materia>());

        Assert.Equal(PaletaMaterias.Neutro, PaletaMaterias.ColorDe("Materia que ya no existe"));
        Assert.Equal(PaletaMaterias.Neutro, PaletaMaterias.ColorDe(null));
    }

    [Fact]
    public void ElExamenRendido_NoGuardaElColor_RN30()
    {
        // Es la prueba de fuego de RN-30: si el color se persistiera con el examen, cambiarlo
        // después no repintaría nada de lo ya rendido.
        var examen = new ExamenRendido { Materia = "Fisiologia" };

        string json = JsonSerializer.Serialize(examen);

        Assert.DoesNotContain("ColorMateria", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CambiarElColorDeUnaMateria_RepintaLosExamenesYaRendidos_RN30()
    {
        var examen = new ExamenRendido { Materia = "Fisiologia" };

        PaletaMaterias.Registrar(new[] { new Materia { Nombre = "Fisiologia", Color = PaletaMaterias.Colores[0] } });
        Assert.Equal(PaletaMaterias.Colores[0], examen.ColorMateria);

        // El alumno le cambia el color a la materia desde Libros.
        PaletaMaterias.Registrar(new[] { new Materia { Nombre = "Fisiologia", Color = PaletaMaterias.Colores[5] } });

        Assert.Equal(PaletaMaterias.Colores[5], examen.ColorMateria);
    }

    [Fact]
    public void ElLibro_TambienResuelveSuColorPorLaMateria()
    {
        PaletaMaterias.Registrar(new[] { new Materia { Nombre = "Bioquimica", Color = PaletaMaterias.Colores[2] } });

        var libro = new Libro { Titulo = "Lehninger", Materia = "Bioquimica" };

        Assert.Equal(PaletaMaterias.Colores[2], libro.ColorMateria);
    }
}

/// <summary>
/// US-027 en el servicio: alta con color, cambio de color y migración del formato anterior.
///
/// Comparte <see cref="RutasAisladasCollection"/> porque <see cref="BibliotecaService"/> lee
/// y escribe rutas estáticas globales.
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class ColorDeMateriaEnLaBibliotecaTests
{
    public ColorDeMateriaEnLaBibliotecaTests() => Limpiar();

    private static BibliotecaService Cargada()
    {
        var servicio = new BibliotecaService();
        servicio.Cargar();
        return servicio;
    }

    [Fact]
    public void UnaMateriaNueva_NuncaQuedaSinColor()
    {
        var b = Cargada();
        b.CrearMateria("Bioquimica");

        var materia = b.MateriaPorNombre("Bioquimica");

        Assert.NotNull(materia);
        Assert.True(materia!.TieneColor);
        Assert.True(PaletaMaterias.EsDeLaPaleta(materia.Color));
    }

    [Fact]
    public void DosMateriasSeguidas_SalenDeColoresDistintos()
    {
        var b = Cargada();
        b.CrearMateria("Fisiologia");
        b.CrearMateria("Bioquimica");

        Assert.NotEqual(
            b.MateriaPorNombre("Fisiologia")!.Color,
            b.MateriaPorNombre("Bioquimica")!.Color);
    }

    [Fact]
    public void SePuedeElegirElColorAlCrearla()
    {
        var b = Cargada();
        string elegido = PaletaMaterias.Colores[4];

        b.CrearMateria("Anatomia", elegido);

        Assert.Equal(elegido, b.MateriaPorNombre("Anatomia")!.Color);
    }

    [Fact]
    public void UnColorFueraDeLaPaleta_SeReemplazaPorUnoValido_RN31()
    {
        var b = Cargada();

        b.CrearMateria("Anatomia", "#FF00FF");

        Assert.True(PaletaMaterias.EsDeLaPaleta(b.MateriaPorNombre("Anatomia")!.Color));
    }

    [Fact]
    public void ElColorSePersiste()
    {
        var primera = Cargada();
        primera.CrearMateria("Fisiologia");
        string color = primera.MateriaPorNombre("Fisiologia")!.Color;

        var segunda = Cargada();

        Assert.Equal(color, segunda.MateriaPorNombre("Fisiologia")!.Color);
    }

    [Fact]
    public void CambiarElColor_LoPersisteYActualizaLaPaleta()
    {
        var b = Cargada();
        b.CrearMateria("Fisiologia");

        string nuevo = PaletaMaterias.Colores[6];
        Assert.True(b.CambiarColorDeMateria("Fisiologia", nuevo));

        Assert.Equal(nuevo, PaletaMaterias.ColorDe("Fisiologia"));
        Assert.Equal(nuevo, Cargada().MateriaPorNombre("Fisiologia")!.Color);
    }

    [Fact]
    public void CambiarAUnColorFueraDeLaPaleta_SeRechaza_RN31()
    {
        var b = Cargada();
        b.CrearMateria("Fisiologia");
        string antes = b.MateriaPorNombre("Fisiologia")!.Color;

        Assert.False(b.CambiarColorDeMateria("Fisiologia", "#123456"));
        Assert.Equal(antes, b.MateriaPorNombre("Fisiologia")!.Color);
    }

    [Fact]
    public void AlRenombrarUnaMateria_ElColorViajaConElla()
    {
        // Renombrar es cambiarle el nombre al grupo, no empezar de cero: si el color se
        // reasignara, la materia cambiaría de color sola al corregirle una falta de ortografía.
        var b = Cargada();
        b.CrearMateria("Fisio");
        string color = b.MateriaPorNombre("Fisio")!.Color;

        b.RenombrarMateria("Fisio", "Fisiologia");

        Assert.Equal(color, b.MateriaPorNombre("Fisiologia")!.Color);
    }

    // ------------------------------------------------------------------
    // Migración del materias.json anterior a US-027
    // ------------------------------------------------------------------

    [Fact]
    public void ElIndiceDeMateriasAnterior_SeLeeYRecibeColores()
    {
        // Antes de US-027 materias.json era una lista de nombres sueltos. Sin tolerar ese
        // formato, la primera vez que se abre la versión nueva el índice se leería como vacío
        // y toda materia sin libros adentro desaparecería.
        RutasApp.AsegurarCarpetas();
        File.WriteAllText(RutasApp.ArchivoMaterias, """["Fisiologia","Bioquimica"]""");

        var b = Cargada();

        Assert.Contains(b.NombresDeMaterias, n => n == "Fisiologia");
        Assert.Contains(b.NombresDeMaterias, n => n == "Bioquimica");
        Assert.All(b.Materias, m => Assert.True(m.TieneColor));
    }

    [Fact]
    public void LaMateriaPorDefecto_UsaElColorNeutro()
    {
        // "Sin materia" es un cajón de pendientes, no una materia con identidad: darle un
        // color vivo la haría competir con las materias reales en la lista.
        var b = Cargada();

        Assert.Equal(PaletaMaterias.Neutro, b.MateriaPorNombre(BibliotecaService.SinMateria)!.Color);
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
    }
}
