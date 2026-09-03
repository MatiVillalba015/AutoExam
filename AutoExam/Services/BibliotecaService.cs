using System.IO;
using System.Text.Json;
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

            // RN-22: ningun material queda huerfano al actualizar a US-023. El material
            // anterior a las materias tenia el campo vacio; cae en el cajon por defecto y
            // desde ahi el alumno lo reasigna cuando quiera.
            if (string.IsNullOrWhiteSpace(libro.Materia))
            {
                libro.Materia = SinMateria;
            }
            else
            {
                libro.Materia = libro.Materia.Trim();
            }

            Libros.Add(libro);
        }

        Cargado = true;

        // Despues de poblar Libros: el indice de materias se arma tambien con las que los
        // libros ya usan, asi que necesita la biblioteca en memoria.
        CargarMaterias();
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
                    libro.Materia = SinMateria;
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
            Materia = string.IsNullOrWhiteSpace(materia) ? SinMateria : materia.Trim(),
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

        // Si el alta trae una materia que todavia no estaba en el indice, se registra sola.
        // Es lo que permite crear la materia desde el propio flujo de carga (US-023) sin un
        // paso previo de "primero anda a crear la materia".
        CrearMateria(libro.Materia);

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


    // ==================================================================
    //  US-023 — Materias · US-027 — color propio de cada materia
    //
    //  El nombre de la materia sigue viviendo en Libro.Materia (texto libre): eso es lo
    //  que hace que la migracion de RN-22 sea gratis, porque todo libro ya tenia el campo.
    //  Lo que agrega este indice aparte es poder tener una materia VACIA —crear
    //  "Bioquimica" y todavia no haberle subido nada— y, desde US-027, guardar su color.
    //
    //  Desde US-027 la materia dejo de ser un string y paso a ser una entidad: el color
    //  tiene que vivir en la materia y no en el examen (RN-30), que es lo que hace que
    //  repintar "Fisiologia" repinte tambien los examenes de fisiologia ya rendidos.
    // ==================================================================

    /// <summary>Materia por defecto de todo material sin clasificar (RN-22).</summary>
    public const string SinMateria = "Sin materia";

    /// <summary>
    /// Materias existentes, ordenadas alfabeticamente con <see cref="SinMateria"/> siempre
    /// al final: es un cajon de pendientes, no una materia mas, y ordenada por nombre
    /// quedaria en el medio de la lista.
    /// </summary>
    public ObservableCollection<Materia> Materias { get; } = new();

    /// <summary>Solo los nombres, que es como los referencian libros y examenes.</summary>
    public IEnumerable<string> NombresDeMaterias => Materias.Select(m => m.Nombre);

    /// <summary>La materia con ese nombre, o null.</summary>
    public Materia? MateriaPorNombre(string? nombre) => Materias.FirstOrDefault(
        m => string.Equals(m.Nombre, (nombre ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reconstruye el indice de materias: las guardadas en materias.json, mas las que
    /// aparecen en algun libro, mas <see cref="SinMateria"/>.
    ///
    /// La union con lo que usan los libros no es redundante: cubre el material que ya
    /// existia antes de US-023 (RN-22) y tambien un libros.json editado a mano. Sin ella,
    /// un libro podria quedar en una materia que no figura en ninguna lista y volverse
    /// invisible al agrupar.
    /// </summary>
    private void CargarMaterias()
    {
        var guardadas = LeerMateriasGuardadas();

        var porNombre = new Dictionary<string, Materia>(StringComparer.OrdinalIgnoreCase);

        foreach (var materia in guardadas)
        {
            string nombre = (materia.Nombre ?? string.Empty).Trim();

            if (nombre.Length > 0 && !porNombre.ContainsKey(nombre))
            {
                materia.Nombre = nombre;
                porNombre[nombre] = materia;
            }
        }

        // Materias que usa algun libro pero que no figuran en el indice.
        foreach (string nombre in Libros.Select(l => (l.Materia ?? string.Empty).Trim()))
        {
            if (nombre.Length > 0 && !porNombre.ContainsKey(nombre))
            {
                porNombre[nombre] = new Materia { Nombre = nombre };
            }
        }

        if (!porNombre.ContainsKey(SinMateria))
        {
            porNombre[SinMateria] = new Materia { Nombre = SinMateria, Color = PaletaMaterias.Neutro };
        }

        Materias.Clear();
        foreach (var materia in Ordenadas(porNombre.Values))
        {
            Materias.Add(materia);
        }

        // US-027: ninguna materia queda sin color. Las que vienen de una version anterior
        // (o de un libro que las usaba sin estar en el indice) reciben uno automatico.
        foreach (var materia in Materias)
        {
            if (!materia.TieneColor)
            {
                materia.Color = EsPorDefecto(materia.Nombre)
                    ? PaletaMaterias.Neutro
                    : PaletaMaterias.SiguienteLibre(Materias);
            }
        }

        PaletaMaterias.Registrar(Materias);
    }

    /// <summary>
    /// Lee materias.json tolerando el formato anterior a US-027, que era una lista de
    /// nombres sueltos (por ejemplo <c>["Fisiologia","Bioquimica"]</c>) en vez de objetos con
    /// nombre y color.
    ///
    /// El archivo se lee y se parsea aca en vez de delegarlo a <see cref="JsonStore"/> por
    /// una razon concreta: ante un JSON que no encaja con el tipo pedido, JsonStore da el
    /// archivo por corrupto y lo mueve a un ".corrupto-<fecha>". El formato viejo NO esta
    /// corrupto —es el que escribio la version anterior de la app—, asi que pasarlo por ahi
    /// significaria que la primera vez que el alumno abre la version nueva sus materias
    /// desaparecen y quedan en un archivo de respaldo que nadie va a mirar.
    /// </summary>
    private static List<Materia> LeerMateriasGuardadas()
    {
        try
        {
            if (!File.Exists(RutasApp.ArchivoMaterias))
            {
                return new List<Materia>();
            }

            string json = File.ReadAllText(RutasApp.ArchivoMaterias);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<Materia>();
            }

            // Formato actual: objetos con nombre y color.
            try
            {
                var entidades = JsonSerializer.Deserialize<List<Materia>>(json, OpcionesJson);

                if (entidades is not null && entidades.Any(m => !string.IsNullOrWhiteSpace(m.Nombre)))
                {
                    return entidades;
                }
            }
            catch (JsonException)
            {
                // No es el formato nuevo: se prueba el viejo abajo.
            }

            // Formato anterior a US-027: una lista de nombres. Se convierte a entidades sin
            // color; CargarMaterias les asigna uno automatico despues.
            var nombres = JsonSerializer.Deserialize<List<string>>(json, OpcionesJson);

            return (nombres ?? new List<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => new Materia { Nombre = n.Trim() })
                .ToList();
        }
        catch (Exception ex)
        {
            // Un materias.json ilegible de verdad no puede impedir que la app abra: el indice
            // se reconstruye igual con las materias que usan los libros.
            RutasApp.RegistrarError("BibliotecaService.LeerMateriasGuardadas", ex);
            return new List<Materia>();
        }
    }

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static bool EsPorDefecto(string nombre) =>
        string.Equals(nombre, SinMateria, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<Materia> Ordenadas(IEnumerable<Materia> materias) => materias
        .OrderBy(m => EsPorDefecto(m.Nombre))
        .ThenBy(m => m.Nombre, StringComparer.CurrentCultureIgnoreCase);

    private void GuardarMaterias()
    {
        if (!Cargado)
        {
            return;
        }

        JsonStore.Guardar(RutasApp.ArchivoMaterias, Materias.ToList());
        PaletaMaterias.Registrar(Materias);
    }

    /// <summary>
    /// Da de alta una materia. Devuelve false si el nombre esta vacio o ya existe
    /// (comparando sin distinguir mayusculas: "Fisiologia" y "fisiologia" son la misma
    /// materia, y admitir las dos partiria la biblioteca en dos grupos identicos).
    ///
    /// Un color vacio o fuera de la paleta (RN-31) hace que se asigne el primero todavia
    /// sin usar: US-027 pide que ninguna materia quede sin color.
    /// </summary>
    public bool CrearMateria(string nombre, string? color = null)
    {
        string limpio = (nombre ?? string.Empty).Trim();

        if (limpio.Length == 0 || ExisteMateria(limpio))
        {
            return false;
        }

        Materias.Add(new Materia
        {
            Nombre = limpio,
            Color = PaletaMaterias.EsDeLaPaleta(color) ? color! : PaletaMaterias.SiguienteLibre(Materias)
        });

        Reordenar();
        GuardarMaterias();

        return true;
    }

    /// <summary>
    /// Cambia el color de una materia (US-027). Devuelve false si la materia no existe o el
    /// color no es de la paleta. No impide repetir un color que ya usa otra materia: la
    /// interfaz ofrece primero los libres, pero elegir uno repetido es valido.
    /// </summary>
    public bool CambiarColorDeMateria(string nombre, string color)
    {
        var materia = MateriaPorNombre(nombre);

        if (materia is null || !PaletaMaterias.EsDeLaPaleta(color))
        {
            return false;
        }

        materia.Color = color;
        GuardarMaterias();

        // Los libros ya cargados muestran el color resuelto por nombre (RN-30): hay que
        // pedirles que lo vuelvan a leer.
        foreach (var libro in Libros)
        {
            libro.NotificarCambioResumen();
        }

        return true;
    }

    public bool ExisteMateria(string nombre) => MateriaPorNombre(nombre) is not null;

    /// <summary>
    /// Renombra una materia y arrastra sus documentos (US-023: "los documentos que ya
    /// estaban agrupados ahi siguen asociados a la materia renombrada"). Devuelve la
    /// cantidad de libros reasignados, o -1 si el cambio no se pudo aplicar.
    /// </summary>
    public int RenombrarMateria(string viejo, string nuevo)
    {
        string origen = (viejo ?? string.Empty).Trim();
        string destino = (nuevo ?? string.Empty).Trim();

        var materia = MateriaPorNombre(origen);

        if (origen.Length == 0 || destino.Length == 0 || materia is null)
        {
            return -1;
        }

        if (string.Equals(origen, destino, StringComparison.OrdinalIgnoreCase))
        {
            // Mismo nombre con otra capitalizacion: no es un choque, es una correccion.
            if (origen == destino)
            {
                return -1;
            }
        }
        else if (ExisteMateria(destino))
        {
            // Renombrar sobre una materia existente fusionaria dos grupos en silencio.
            return -1;
        }

        // "Sin materia" es el destino de RN-22 y lo nombra codigo, no el usuario: si se
        // pudiera renombrar, el material sin clasificar se quedaria sin cajon adonde caer.
        if (EsPorDefecto(origen))
        {
            return -1;
        }

        int movidos = 0;
        foreach (var libro in Libros.Where(l => string.Equals(l.Materia, origen, StringComparison.OrdinalIgnoreCase)))
        {
            libro.Materia = destino;
            libro.NotificarCambioResumen();
            movidos++;
        }

        // El color viaja con la materia: renombrar no es empezar de cero.
        materia.Nombre = destino;
        Reordenar();

        GuardarMaterias();
        Guardar();

        return movidos;
    }

    /// <summary>
    /// Elimina una materia. Con borrarDocumentos en true se borran los libros que tenia
    /// adentro (con su copia interna); en false se mandan a <see cref="SinMateria"/>. Quien
    /// llama tiene que haberle preguntado al usuario: US-023 prohibe borrarlos en silencio.
    /// </summary>
    public int EliminarMateria(string nombre, bool borrarDocumentos)
    {
        string objetivo = (nombre ?? string.Empty).Trim();
        var materia = MateriaPorNombre(objetivo);

        // Borrar "Sin materia" dejaria sin destino a la reasignacion y al alta por defecto.
        if (objetivo.Length == 0 || EsPorDefecto(objetivo) || materia is null)
        {
            return -1;
        }

        var adentro = Libros
            .Where(l => string.Equals(l.Materia, objetivo, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var libro in adentro)
        {
            if (borrarDocumentos)
            {
                BorrarDeposito(libro);
                Libros.Remove(libro);
            }
            else
            {
                libro.Materia = SinMateria;
                libro.NotificarCambioResumen();
            }
        }

        Materias.Remove(materia);

        GuardarMaterias();
        Guardar();

        return adentro.Count;
    }

    /// <summary>Libros de una materia, en el orden en que estan en la biblioteca.</summary>
    public IEnumerable<Libro> LibrosDe(string materia) => Libros
        .Where(l => string.Equals(l.Materia, (materia ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reordena en el lugar. No se reemplaza la coleccion entera porque la vista esta
    /// enlazada a esta instancia: cambiarla romperia el binding.
    /// </summary>
    private void Reordenar()
    {
        var ordenadas = Ordenadas(Materias.ToList()).ToList();

        for (int destino = 0; destino < ordenadas.Count; destino++)
        {
            int actual = Materias.IndexOf(ordenadas[destino]);
            if (actual != destino && actual >= 0)
            {
                Materias.Move(actual, destino);
            }
        }
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
