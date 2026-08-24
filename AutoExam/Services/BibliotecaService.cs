using System.IO;
using System.Collections.ObjectModel;
using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>Alta, baja y persistencia de los libros de la biblioteca (libros.json + copia del PDF).</summary>
public class BibliotecaService
{
    private readonly PdfExtractorService _pdf = new();

    public ObservableCollection<Libro> Libros { get; } = new();

    /// <summary>
    /// Solo queda en true si <see cref="Cargar"/> llego a leer el indice. Sin esto, un fallo
    /// de arranque seguido de un guardado dejaria libros.json en blanco y se perderia la
    /// biblioteca entera.
    /// </summary>
    public bool Cargado { get; private set; }

    public void Cargar()
    {
        RutasApp.AsegurarCarpetas();

        var libros = JsonStore.Cargar(RutasApp.ArchivoLibros, () => new List<Libro>());

        Libros.Clear();
        foreach (var libro in libros.OrderByDescending(l => l.FechaAgregado))
        {
            libro.Modulos ??= new List<Modulo>();
            Libros.Add(libro);
        }

        Cargado = true;
    }

    public void Guardar()
    {
        if (!Cargado)
        {
            // Nunca pisar el indice con lo que haya en memoria si nunca se leyo el archivo.
            RutasApp.RegistrarError("BibliotecaService.Guardar",
                new InvalidOperationException("Se intento guardar la biblioteca sin haberla cargado. Guardado omitido."));
            return;
        }

        JsonStore.Guardar(RutasApp.ArchivoLibros, Libros.ToList());
    }

    /// <summary>
    /// Vuelve a registrar los PDF que estan en la carpeta interna pero desaparecieron del
    /// indice. Reconstruye el titulo desde el historial cuando el libro figura ahi.
    /// </summary>
    public async Task<int> RecuperarHuerfanosAsync(
        IReadOnlyDictionary<string, (string Titulo, string Materia)>? conocidos = null,
        CancellationToken ct = default)
    {
        if (!Cargado || !Directory.Exists(RutasApp.Biblioteca))
        {
            return 0;
        }

        var registrados = Libros
            .Select(l => Path.GetFileNameWithoutExtension(l.RutaArchivo))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int recuperados = 0;

        foreach (string pdf in Directory.GetFiles(RutasApp.Biblioteca, "*.pdf"))
        {
            ct.ThrowIfCancellationRequested();

            string id = Path.GetFileNameWithoutExtension(pdf);
            if (registrados.Contains(id))
            {
                continue;
            }

            try
            {
                var libro = new Libro
                {
                    Id = id,
                    RutaArchivo = pdf,
                    NombreArchivoOriginal = Path.GetFileName(pdf),
                    FechaAgregado = File.GetCreationTime(pdf),
                    CantidadPaginas = await ContarPaginasAsync(pdf, ct).ConfigureAwait(true)
                };

                if (conocidos is not null && conocidos.TryGetValue(id, out var datos))
                {
                    libro.Titulo = datos.Titulo;
                    libro.Materia = datos.Materia;
                }
                else
                {
                    libro.Titulo = "Libro recuperado";
                    libro.Materia = "Sin materia";
                }

                libro.Modulos.Add(new Modulo
                {
                    Nombre = "Libro completo",
                    DesdePagina = 1,
                    HastaPagina = libro.CantidadPaginas
                });

                Libros.Insert(0, libro);
                recuperados++;
            }
            catch (Exception ex)
            {
                RutasApp.RegistrarError($"RecuperarHuerfano({pdf})", ex);
            }
        }

        if (recuperados > 0)
        {
            Guardar();
        }

        return recuperados;
    }

    private Task<int> ContarPaginasAsync(string ruta, CancellationToken ct) => _pdf.ContarPaginasAsync(ruta, ct);

    /// <summary>Copia el PDF a la carpeta interna, cuenta sus paginas y lo registra.</summary>
    public async Task<Libro> AgregarLibroAsync(
        string rutaOrigen,
        string titulo,
        string materia,
        IEnumerable<Modulo>? modulos = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(rutaOrigen))
        {
            throw new FileNotFoundException("No se encontro el PDF seleccionado.", rutaOrigen);
        }

        RutasApp.AsegurarCarpetas();

        var libro = new Libro
        {
            Titulo = string.IsNullOrWhiteSpace(titulo)
                ? Path.GetFileNameWithoutExtension(rutaOrigen)
                : titulo.Trim(),
            Materia = string.IsNullOrWhiteSpace(materia) ? "Sin materia" : materia.Trim(),
            NombreArchivoOriginal = Path.GetFileName(rutaOrigen),
            FechaAgregado = DateTime.Now
        };

        string destino = Path.Combine(RutasApp.Biblioteca, $"{libro.Id}.pdf");

        await Task.Run(() => File.Copy(rutaOrigen, destino, overwrite: true), ct).ConfigureAwait(true);
        libro.RutaArchivo = destino;

        try
        {
            libro.CantidadPaginas = await _pdf.ContarPaginasAsync(destino, ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            File.Delete(destino);
            throw new InvalidOperationException(
                $"No se pudo leer el PDF: {ex.Message}. Puede estar protegido con contrasenia o dañado.", ex);
        }

        if (modulos is not null)
        {
            libro.Modulos.AddRange(modulos);
        }

        if (libro.Modulos.Count == 0)
        {
            libro.Modulos.Add(new Modulo
            {
                Nombre = "Libro completo",
                DesdePagina = 1,
                HastaPagina = libro.CantidadPaginas
            });
        }

        Libros.Insert(0, libro);
        Guardar();

        return libro;
    }

    public void EliminarLibro(Libro libro)
    {
        try
        {
            if (File.Exists(libro.RutaArchivo))
            {
                File.Delete(libro.RutaArchivo);
            }
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError($"EliminarLibro({libro.RutaArchivo})", ex);
        }

        Libros.Remove(libro);
        Guardar();
    }

    /// <summary>Divide el libro en N modulos de paginas iguales (atajo para cargar rapido).</summary>
    public static List<Modulo> GenerarModulosAutomaticos(int totalPaginas, int cantidad, string prefijo = "Modulo")
    {
        var modulos = new List<Modulo>();
        cantidad = Math.Clamp(cantidad, 1, Math.Max(1, totalPaginas));

        int porModulo = (int)Math.Ceiling(totalPaginas / (double)cantidad);

        for (int i = 0; i < cantidad; i++)
        {
            int desde = i * porModulo + 1;
            int hasta = Math.Min(totalPaginas, (i + 1) * porModulo);

            if (desde > totalPaginas)
            {
                break;
            }

            modulos.Add(new Modulo
            {
                Nombre = $"{prefijo} {i + 1}",
                DesdePagina = desde,
                HastaPagina = hasta
            });
        }

        return modulos;
    }
}
