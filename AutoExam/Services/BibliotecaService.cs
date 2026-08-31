using System.IO;
using System.Collections.ObjectModel;
using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>
/// Alta, baja y persistencia de las fuentes de la biblioteca (libros.json + copia interna
/// del/los archivo(s)). Generalizado a fuentes multi-formato/multi-archivo (arquitectura
/// Inc-4 §3/§4.2): PDF/Office se guardan como <c>Biblioteca\{Id}{ext}</c>; los sets de
/// imagenes como <c>Biblioteca\{Id}\01.ext, 02.ext...</c> preservando el orden de alta.
/// </summary>
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
            libro.Archivos ??= new List<string>();

            // Migracion de registros previos a fuentes multi-formato (arquitectura Inc-4 §3):
            //  - sin Tipo   -> Pdf (ya es el default del enum al deserializar un campo ausente).
            //  - sin Archivos -> [RutaArchivo].
            //  - sin RutaArchivo pero con Archivos -> RutaArchivo = Archivos[0].
            if (libro.Archivos.Count == 0 && !string.IsNullOrWhiteSpace(libro.RutaArchivo))
            {
                libro.Archivos.Add(libro.RutaArchivo);
            }
            else if (string.IsNullOrWhiteSpace(libro.RutaArchivo) && libro.Archivos.Count > 0)
            {
                libro.RutaArchivo = libro.Archivos[0];
            }

            if (string.IsNullOrWhiteSpace(libro.MedidaTamanio))
            {
                libro.MedidaTamanio = MedidaBasica(libro.Tipo, libro.Archivos, libro.CantidadPaginas);
            }

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
    /// (Solo PDF: los sets de imagenes viven en subcarpetas y no se escanean.)
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
            .Select(l => l.Id)
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
                    Tipo = TipoFuente.Pdf,
                    RutaArchivo = pdf,
                    Archivos = new List<string> { pdf },
                    NombreArchivoOriginal = Path.GetFileName(pdf),
                    FechaAgregado = File.GetCreationTime(pdf),
                    CantidadPaginas = await ContarPaginasAsync(pdf, ct).ConfigureAwait(true)
                };

                libro.MedidaTamanio = MedidaBasica(libro.Tipo, libro.Archivos, libro.CantidadPaginas);

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

    /// <summary>
    /// Alta de una fuente multi-formato: 1 archivo (PDF/Word/Excel/PowerPoint) o N imagenes
    /// ordenadas. Copia el/los archivo(s) a la carpeta interna, obtiene la medida de tamanio
    /// por formato (contrato <c>IExtractorContenido.MedirAsync</c>, arquitectura Inc-4 §4.1)
    /// y — solo para PDF — cuenta paginas y arma el modulo por defecto.
    /// </summary>
    /// <remarks>
    /// US-008/US-009/US-010, AC-T41. Reglas de negocio "1 fuente / no se combinan tipos"
    /// (arquitectura Inc-4 §3): mezcla de familias o mas de un archivo no-imagen ⇒
    /// <see cref="FuenteInvalidaException"/>, cuyo <c>Message</c> el ViewModel muestra tal cual
    /// al usuario (sin sufijo de parametro). Fuente ilegible (protegida/danada) ⇒ borra lo
    /// copiado y re-lanza <see cref="FuenteIlegibleException"/> con la causa — mismo patron
    /// que el <c>AgregarLibroAsync</c> original.
    /// </remarks>
    public async Task<Libro> AgregarFuenteAsync(
        IReadOnlyList<string> rutasOrigen,
        string titulo,
        string materia,
        IEnumerable<Modulo>? modulos = null,
        CancellationToken ct = default)
    {
        if (rutasOrigen is null || rutasOrigen.Count == 0)
        {
            throw new FuenteInvalidaException("No se selecciono ninguna fuente.");
        }

        foreach (string ruta in rutasOrigen)
        {
            if (!File.Exists(ruta))
            {
                throw new FileNotFoundException("No se encontro el archivo seleccionado.", ruta);
            }
        }

        // Tipo por extension; todas las rutas tienen que ser de la misma familia.
        var tipos = rutasOrigen
            .Select(r => TipoDeExtension(Path.GetExtension(r)))
            .Distinct()
            .ToList();

        if (tipos.Count > 1)
        {
            throw new FuenteInvalidaException(
                "No se combinan tipos de archivo: un examen usa una sola fuente.");
        }

        TipoFuente tipo = tipos[0];

        if (tipo != TipoFuente.SetImagenes && rutasOrigen.Count > 1)
        {
            throw new FuenteInvalidaException(
                "Solo se puede agregar un archivo por fuente (salvo un set de imagenes).");
        }

        RutasApp.AsegurarCarpetas();

        var libro = new Libro
        {
            Tipo = tipo,
            Titulo = string.IsNullOrWhiteSpace(titulo)
                ? Path.GetFileNameWithoutExtension(rutasOrigen[0])
                : titulo.Trim(),
            Materia = string.IsNullOrWhiteSpace(materia) ? "Sin materia" : materia.Trim(),
            NombreArchivoOriginal = Path.GetFileName(rutasOrigen[0]),
            FechaAgregado = DateTime.Now
        };

        libro.Archivos = await Task.Run(() => CopiarADeposito(libro.Id, tipo, rutasOrigen), ct).ConfigureAwait(true);
        libro.RutaArchivo = libro.Archivos[0];

        try
        {
            if (tipo == TipoFuente.Pdf)
            {
                libro.CantidadPaginas = await _pdf.ContarPaginasAsync(libro.RutaArchivo, ct).ConfigureAwait(true);
            }

            var extractor = FactoriaExtractores.Para(Path.GetExtension(libro.RutaArchivo));
            libro.MedidaTamanio = extractor is not null
                ? (await extractor.MedirAsync(libro.Archivos, ct).ConfigureAwait(true)).Texto
                : MedidaBasica(tipo, libro.Archivos, libro.CantidadPaginas);
        }
        catch (FuenteIlegibleException)
        {
            BorrarDeposito(libro);
            throw;
        }
        catch (Exception ex)
        {
            BorrarDeposito(libro);
            throw new FuenteIlegibleException(
                $"No se pudo leer la fuente: {ex.Message}. Puede estar protegida con contrasenia o danada.", ex);
        }

        // Los modulos/capitulos son exclusivos de PDF con indice (arquitectura Inc-4 §3).
        if (tipo == TipoFuente.Pdf)
        {
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
        }

        Libros.Insert(0, libro);
        Guardar();

        return libro;
    }

    /// <summary>
    /// Compatibilidad: alta de una fuente de archivo unico (el uso historico era siempre PDF).
    /// Wrapper sobre <see cref="AgregarFuenteAsync"/>.
    /// </summary>
    public Task<Libro> AgregarLibroAsync(
        string rutaOrigen,
        string titulo,
        string materia,
        IEnumerable<Modulo>? modulos = null,
        CancellationToken ct = default)
        => AgregarFuenteAsync(new[] { rutaOrigen }, titulo, materia, modulos, ct);

    public void EliminarLibro(Libro libro)
    {
        BorrarDeposito(libro);
        Libros.Remove(libro);
        Guardar();
    }

    /// <summary>Copia la fuente a la carpeta interna y devuelve las rutas internas en orden.</summary>
    private static List<string> CopiarADeposito(string id, TipoFuente tipo, IReadOnlyList<string> origenes)
    {
        var destinos = new List<string>();

        if (tipo == TipoFuente.SetImagenes)
        {
            string carpeta = Path.Combine(RutasApp.Biblioteca, id);
            Directory.CreateDirectory(carpeta);

            for (int i = 0; i < origenes.Count; i++)
            {
                string ext = Path.GetExtension(origenes[i]).ToLowerInvariant();
                string destino = Path.Combine(carpeta, $"{i + 1:D2}{ext}");
                File.Copy(origenes[i], destino, overwrite: true);
                destinos.Add(destino);
            }
        }
        else
        {
            string ext = Path.GetExtension(origenes[0]).ToLowerInvariant();
            string destino = Path.Combine(RutasApp.Biblioteca, $"{id}{ext}");
            File.Copy(origenes[0], destino, overwrite: true);
            destinos.Add(destino);
        }

        return destinos;
    }

    /// <summary>Borra la copia interna: la carpeta entera si es un set de imagenes, si no el/los archivo(s).</summary>
    private static void BorrarDeposito(Libro libro)
    {
        try
        {
            if (libro.Tipo == TipoFuente.SetImagenes)
            {
                string carpeta = Path.Combine(RutasApp.Biblioteca, libro.Id);
                if (Directory.Exists(carpeta))
                {
                    Directory.Delete(carpeta, recursive: true);
                }
            }
            else
            {
                foreach (string archivo in ArchivosDe(libro))
                {
                    if (File.Exists(archivo))
                    {
                        File.Delete(archivo);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError($"BibliotecaService.BorrarDeposito({libro.Id})", ex);
        }
    }

    private static IEnumerable<string> ArchivosDe(Libro libro)
    {
        if (libro.Archivos is { Count: > 0 })
        {
            return libro.Archivos;
        }

        return string.IsNullOrWhiteSpace(libro.RutaArchivo)
            ? Array.Empty<string>()
            : new[] { libro.RutaArchivo };
    }

    private static TipoFuente TipoDeExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => TipoFuente.Pdf,
        ".docx" => TipoFuente.Word,
        ".xlsx" => TipoFuente.Excel,
        ".pptx" => TipoFuente.PowerPoint,
        ".jpg" or ".jpeg" or ".png" or ".heic" or ".heif" => TipoFuente.SetImagenes,
        _ => throw new FormatoNoSoportadoException()
    };

    /// <summary>
    /// Medida de tamanio de reserva: se usa para el back-fill de registros viejos en
    /// <see cref="Cargar"/> y como fallback si <c>FactoriaExtractores.Para</c> no resuelve
    /// un extractor. El alta normal la reemplaza con <c>IExtractorContenido.MedirAsync</c>.
    /// </summary>
    private static string MedidaBasica(TipoFuente tipo, IReadOnlyList<string> archivos, int cantidadPaginas) => tipo switch
    {
        TipoFuente.Pdf => $"{cantidadPaginas} paginas",
        TipoFuente.SetImagenes => $"{archivos.Count} {(archivos.Count == 1 ? "imagen" : "imagenes")}",
        _ => "documento unico"
    };

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

/// <summary>
/// La seleccion del usuario no arma una fuente valida (nada seleccionado, familias mezcladas,
/// varios archivos no-imagen). Su <see cref="Exception.Message"/> es texto listo para mostrar:
/// <c>BibliotecaViewModel</c> / <c>AsistenteViewModel</c> lo pasan tal cual a <c>Avisar</c>,
/// asi que no debe arrastrar sufijos como el nombre de parametro de <see cref="ArgumentException"/>.
/// </summary>
public sealed class FuenteInvalidaException : Exception
{
    public FuenteInvalidaException(string message) : base(message)
    {
    }
}
