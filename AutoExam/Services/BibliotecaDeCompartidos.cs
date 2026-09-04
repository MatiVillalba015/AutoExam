using System.IO;
using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>Un examen compartido ya importado y guardado en disco, listo para elegir y rendir.</summary>
public sealed class ExamenImportado
{
    public ExamenImportado(string ruta, ExamenCompartido paquete)
    {
        Ruta = ruta;
        Paquete = paquete;
    }

    /// <summary>Copia propia dentro de la carpeta de la app, no el archivo original del compañero.</summary>
    public string Ruta { get; }

    public ExamenCompartido Paquete { get; }

    public string Titulo => string.IsNullOrWhiteSpace(Paquete.Titulo) ? "Examen compartido" : Paquete.Titulo;

    public string Materia => string.IsNullOrWhiteSpace(Paquete.Materia) ? "Sin materia" : Paquete.Materia;

    public int Preguntas => Paquete.Preguntas.Count;

    public string Detalle => $"{Preguntas} preguntas · {Materia}";

    /// <summary>Color de la materia, resuelto al dibujar como en todo el resto (RN-30).</summary>
    public string ColorMateria => PaletaMaterias.ColorDe(Paquete.Materia);
}

/// <summary>
/// Guarda los examenes que le compartieron al alumno (US-037).
///
/// El archivo se copia adentro de la carpeta de datos de la app en vez de recordar la ruta
/// original. Un examen que llego por Telegram vive en Descargas, y Descargas se vacia: si solo
/// guardaramos la ruta, el examen del compañero desapareceria sin aviso justo cuando se lo
/// quiere rendir. La copia tambien es lo que permite volver a rendirlo meses despues, que es
/// lo que hace que valga la pena importar y no simplemente abrir.
/// </summary>
public static class BibliotecaDeCompartidos
{
    /// <summary>Todos los examenes importados, del mas nuevo al mas viejo.</summary>
    public static IReadOnlyList<ExamenImportado> Listar()
    {
        var salida = new List<(DateTime Fecha, ExamenImportado Examen)>();

        try
        {
            Directory.CreateDirectory(RutasApp.Compartidos);

            foreach (string ruta in Directory.EnumerateFiles(
                         RutasApp.Compartidos, "*" + CompartirExamenService.Extension))
            {
                var resultado = CompartirExamenService.Leer(ruta);

                // Un archivo que dejo de ser legible no puede tumbar la lista entera: se
                // saltea y el resto se sigue ofreciendo.
                if (resultado.Ok)
                {
                    salida.Add((File.GetLastWriteTime(ruta), new ExamenImportado(ruta, resultado.Examen!)));
                }
            }
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("Compartidos.Listar", ex);
        }

        return salida.OrderByDescending(x => x.Fecha).Select(x => x.Examen).ToList();
    }

    /// <summary>Copia el archivo elegido a la carpeta de la app y devuelve el examen guardado.</summary>
    public static ExamenImportado Guardar(ExamenCompartido paquete, string rutaOriginal)
    {
        Directory.CreateDirectory(RutasApp.Compartidos);

        string destino = RutaLibre(paquete.Titulo);

        // Se reescribe desde el paquete ya validado y no se copia el archivo tal cual: lo que
        // queda guardado es exactamente lo que la app supo leer, sin nada extra que hubiera
        // venido en el original.
        CompartirExamenService.Guardar(paquete, destino);

        return new ExamenImportado(destino, paquete);
    }

    private static string RutaLibre(string titulo)
    {
        string baseNombre = Path.GetFileNameWithoutExtension(CompartirExamenService.NombreSugerido(titulo));
        string candidato = Path.Combine(RutasApp.Compartidos, baseNombre + CompartirExamenService.Extension);

        int n = 2;
        while (File.Exists(candidato))
        {
            candidato = Path.Combine(
                RutasApp.Compartidos, $"{baseNombre} ({n}){CompartirExamenService.Extension}");
            n++;
        }

        return candidato;
    }

    public static void Borrar(ExamenImportado examen)
    {
        try
        {
            if (File.Exists(examen.Ruta))
            {
                File.Delete(examen.Ruta);
            }
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("Compartidos.Borrar", ex);
        }
    }
}
