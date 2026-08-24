using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Writer;

namespace AutoExam.Services;

public record RangoPaginas(int Desde, int Hasta, string Etiqueta);

/// <summary>Capitulo leido del indice interno del PDF.</summary>
public record CapituloDetectado(string Titulo, int Desde, int Hasta)
{
    public int CantidadPaginas => Math.Max(0, Hasta - Desde + 1);
}

/// <summary>Bloque contiguo de paginas ya convertido a texto plano.</summary>
public class FragmentoTexto
{
    public int PaginaDesde { get; init; }
    public int PaginaHasta { get; init; }
    public string Etiqueta { get; init; } = string.Empty;
    public string Texto { get; set; } = string.Empty;

    public string Referencia => string.IsNullOrWhiteSpace(Etiqueta)
        ? $"pags. {PaginaDesde}-{PaginaHasta}"
        : $"{Etiqueta}, pags. {PaginaDesde}-{PaginaHasta}";
}

public class ImagenExtraida
{
    /// <summary>Identificador que se le informa a Gemini para que lo devuelva en RutaImagenAdjunta.</summary>
    public string Identificador { get; init; } = string.Empty;

    public string Ruta { get; init; } = string.Empty;
    public string MimeType { get; init; } = "image/png";
    public int Pagina { get; init; }
    public int Ancho { get; init; }
    public int Alto { get; init; }
    public string Etiqueta { get; init; } = string.Empty;

    /// <summary>
    /// El archivo ya salio escalado y comprimido para mandar (caso de las paginas
    /// escaneadas). Evita que se lo vuelva a reescalar al armar el request.
    /// </summary>
    public bool YaPreparada { get; init; }
}

public class ExtraccionResultado
{
    public List<FragmentoTexto> Fragmentos { get; } = new();

    /// <summary>Figuras con valor academico: alimentan preguntas sobre graficos.</summary>
    public List<ImagenExtraida> Imagenes { get; } = new();

    /// <summary>
    /// Paginas sin texto extraible rescatadas como imagen. No son figuras: son el
    /// material de estudio, y viajan a Gemini para que les lea el texto.
    /// </summary>
    public List<ImagenExtraida> PaginasEscaneadas { get; } = new();

    public int PaginasSeleccionadas { get; set; }
    public int PaginasLeidas { get; set; }

    /// <summary>Paginas leidas que no devolvieron texto (candidatas a escaneo).</summary>
    public int PaginasSinTexto { get; set; }

    /// <summary>Paginas sin texto cuya imagen tampoco se pudo decodificar (JBIG2, JPX...).</summary>
    public int PaginasSinTextoNiImagen { get; set; }

    public bool HuboMuestreo { get; set; }
    public bool HuboRecorte { get; set; }

    public int CaracteresTotales => Fragmentos.Sum(f => f.Texto.Length);

    public bool TieneTexto => Fragmentos.Any(f => f.Texto.Length > 200);

    public bool TienePaginasEscaneadas => PaginasEscaneadas.Count > 0;

    /// <summary>Hay con que generar un examen: texto, imagenes de pagina, o ambos.</summary>
    public bool TieneMaterial => TieneTexto || TienePaginasEscaneadas;
}

public class OpcionesExtraccion
{
    /// <summary>Paginas leidas por bloque (bucle) para no cargar el PDF entero en RAM.</summary>
    public int PaginasPorBloque { get; set; } = 15;

    /// <summary>Tope duro de paginas efectivamente leidas; por encima se toman muestras representativas.</summary>
    public int MaxPaginasLeidas { get; set; } = 400;

    public int MaxCaracteres { get; set; } = 90_000;

    public bool ExtraerImagenes { get; set; } = true;
    public int MaxImagenes { get; set; } = 12;
    public int MinAnchoImagen { get; set; } = 220;
    public int MinAltoImagen { get; set; } = 180;

    // ---------- Rescate de paginas escaneadas ----------

    /// <summary>Caracteres utiles minimos para dar una pagina por "con texto".</summary>
    public int MinCaracteresPagina { get; set; } = 40;

    /// <summary>
    /// Tope de paginas que se mandan como imagen. Cada una pesa varios cientos de KB en
    /// Base64, asi que el limite lo pone el tamanio del request, no el PDF.
    /// </summary>
    public int MaxPaginasEscaneadas { get; set; } = 10;

    /// <summary>Reparte esas paginas: como maximo esta cantidad por bloque leido.</summary>
    public int MaxPaginasEscaneadasPorBloque { get; set; } = 3;

    /// <summary>Lado maximo en px de la imagen de pagina que viaja a Gemini.</summary>
    public int LadoMaximoPaginaEscaneada { get; set; } = 1600;

    /// <summary>Descarta como pagina escaneada cualquier imagen mas chica que esto.</summary>
    public int MinLadoPaginaEscaneada { get; set; } = 500;

    public string CarpetaImagenes { get; set; } = string.Empty;
}

/// <summary>
/// Extraccion de texto e imagenes con PdfPig, siempre por bloques de paginas.
/// Nunca materializa el documento completo: abre el PDF, recorre solo las paginas
/// pedidas en lotes y va soltando el texto ya normalizado.
/// </summary>
public class PdfExtractorService
{
    private static readonly Regex EspaciosMultiples = new(@"[ \t]{2,}", RegexOptions.Compiled);
    private static readonly Regex SaltosMultiples = new(@"(\r?\n){3,}", RegexOptions.Compiled);

    /// <summary>Puntos guia del indice: "Capitulo 3 .......... 47".</summary>
    private static readonly Regex PuntosGuia = new(@"[.·•…\-_]{3,}\s*\d*\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Detecta los capitulos del PDF leyendo su indice interno (bookmarks / outline), que es
    /// el mismo que muestra el panel lateral de cualquier visor. Devuelve la lista vacia si
    /// el PDF no trae indice, que es lo normal en los escaneados.
    /// </summary>
    public Task<List<CapituloDetectado>> DetectarCapitulosAsync(string rutaPdf, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var doc = PdfDocument.Open(rutaPdf, OpcionesLectura());

            if (!doc.TryGetBookmarks(out var marcadores))
            {
                return new List<CapituloDetectado>();
            }

            // El indice es un arbol: el nivel 0 suele ser "Capitulo 1, Capitulo 2...", pero
            // hay libros que cuelgan todo de una raiz unica ("Contenido"). Se elige el nivel
            // menos profundo que tenga al menos dos entradas con pagina: ese es el que el
            // usuario reconoce como "los capitulos".
            var porNivel = new Dictionary<int, List<DocumentBookmarkNode>>();
            Recorrer(marcadores.Roots, porNivel, ct);

            var nivel = porNivel
                .OrderBy(p => p.Key)
                .FirstOrDefault(p => p.Value.Count >= 2);

            var elegidos = nivel.Value ?? porNivel.OrderBy(p => p.Key).FirstOrDefault().Value;
            if (elegidos is null || elegidos.Count == 0)
            {
                return new List<CapituloDetectado>();
            }

            return ArmarCapitulos(elegidos, doc.NumberOfPages);
        }, ct);
    }

    private static void Recorrer(
        IEnumerable<BookmarkNode> nodos,
        Dictionary<int, List<DocumentBookmarkNode>> porNivel,
        CancellationToken ct)
    {
        foreach (var nodo in nodos)
        {
            ct.ThrowIfCancellationRequested();

            if (nodo is DocumentBookmarkNode conPagina && conPagina.PageNumber > 0)
            {
                if (!porNivel.TryGetValue(nodo.Level, out var lista))
                {
                    lista = new List<DocumentBookmarkNode>();
                    porNivel[nodo.Level] = lista;
                }

                lista.Add(conPagina);
            }

            if (nodo.Children.Count > 0)
            {
                Recorrer(nodo.Children, porNivel, ct);
            }
        }
    }

    /// <summary>
    /// Cierra cada capitulo donde empieza el siguiente. El PDF solo dice donde ARRANCA
    /// cada entrada del indice, nunca donde termina.
    /// </summary>
    private static List<CapituloDetectado> ArmarCapitulos(
        List<DocumentBookmarkNode> nodos, int totalPaginas)
    {
        var ordenados = nodos
            .Where(n => n.PageNumber >= 1 && n.PageNumber <= totalPaginas)
            .GroupBy(n => n.PageNumber)
            .Select(g => g.First())
            .OrderBy(n => n.PageNumber)
            .ToList();

        var capitulos = new List<CapituloDetectado>();

        for (int i = 0; i < ordenados.Count; i++)
        {
            int desde = ordenados[i].PageNumber;
            int hasta = i + 1 < ordenados.Count ? ordenados[i + 1].PageNumber - 1 : totalPaginas;

            if (hasta < desde)
            {
                continue;
            }

            string titulo = LimpiarTitulo(ordenados[i].Title);
            if (titulo.Length == 0)
            {
                titulo = $"Capitulo {capitulos.Count + 1}";
            }

            capitulos.Add(new CapituloDetectado(titulo, desde, hasta));
        }

        return capitulos;
    }

    /// <summary>Normaliza el titulo del indice: espacios raros, puntos guia y numeracion suelta.</summary>
    private static string LimpiarTitulo(string? titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            return string.Empty;
        }

        string limpio = titulo.Replace(' ', ' ').Replace('\t', ' ').Trim();
        limpio = PuntosGuia.Replace(limpio, string.Empty).Trim();
        limpio = EspaciosMultiples.Replace(limpio, " ");

        return limpio.Length > 70 ? limpio[..67].TrimEnd() + "..." : limpio;
    }

    public Task<int> ContarPaginasAsync(string rutaPdf, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var doc = PdfDocument.Open(rutaPdf, OpcionesLectura());
            return doc.NumberOfPages;
        }, ct);
    }

    public Task<ExtraccionResultado> ExtraerAsync(
        string rutaPdf,
        IReadOnlyList<RangoPaginas> rangos,
        OpcionesExtraccion opciones,
        IProgress<string>? progreso = null,
        CancellationToken ct = default)
    {
        return Task.Run(() => Extraer(rutaPdf, rangos, opciones, progreso, ct), ct);
    }

    private ExtraccionResultado Extraer(
        string rutaPdf,
        IReadOnlyList<RangoPaginas> rangos,
        OpcionesExtraccion op,
        IProgress<string>? progreso,
        CancellationToken ct)
    {
        var resultado = new ExtraccionResultado();

        using var doc = PdfDocument.Open(rutaPdf, OpcionesLectura());
        int totalPaginas = doc.NumberOfPages;

        var paginas = ExpandirPaginas(rangos, totalPaginas, out var etiquetaPorPagina);
        resultado.PaginasSeleccionadas = paginas.Count;

        if (paginas.Count == 0)
        {
            return resultado;
        }

        // Bloques contiguos de N paginas: la unidad de lectura y tambien la de muestreo.
        var bloques = AgruparEnBloques(paginas, Math.Max(1, op.PaginasPorBloque));

        int maxBloques = Math.Max(1, op.MaxPaginasLeidas / Math.Max(1, op.PaginasPorBloque));
        if (bloques.Count > maxBloques)
        {
            bloques = MuestrearUniforme(bloques, maxBloques);
            resultado.HuboMuestreo = true;
        }

        var hashesImagen = new HashSet<string>(StringComparer.Ordinal);
        int bloqueActual = 0;

        foreach (var bloque in bloques)
        {
            ct.ThrowIfCancellationRequested();
            bloqueActual++;

            progreso?.Report(
                $"Leyendo PDF: bloque {bloqueActual}/{bloques.Count} (pags. {bloque[0]}-{bloque[^1]})...");

            var sb = new StringBuilder(capacity: 8 * 1024);
            string etiqueta = etiquetaPorPagina.TryGetValue(bloque[0], out var e) ? e : string.Empty;

            // Las paginas escaneadas del bloque se toman espaciadas, no las primeras N:
            // asi la muestra cubre todo el bloque en vez de su comienzo.
            int pasoEscaneo = Math.Max(1, bloque.Count / Math.Max(1, op.MaxPaginasEscaneadasPorBloque));
            int escaneadasDelBloque = 0;

            for (int indiceEnBloque = 0; indiceEnBloque < bloque.Count; indiceEnBloque++)
            {
                ct.ThrowIfCancellationRequested();
                int numeroPagina = bloque[indiceEnBloque];

                Page page;
                try
                {
                    page = doc.GetPage(numeroPagina);
                }
                catch (Exception ex)
                {
                    RutasApp.RegistrarError($"PdfPig.GetPage({numeroPagina})", ex);
                    continue;
                }

                string texto = LeerTextoPagina(page);
                bool conTexto = ContarUtiles(texto) >= op.MinCaracteresPagina;

                if (conTexto)
                {
                    sb.Append("[Pagina ").Append(numeroPagina).Append(']').AppendLine();
                    sb.AppendLine(texto);
                    sb.AppendLine();

                    // Solo una pagina con texto puede aportar figuras: en una escaneada la
                    // "figura" seria la pagina entera, y generaria preguntas sobre el escaneo.
                    if (op.ExtraerImagenes && resultado.Imagenes.Count < op.MaxImagenes)
                    {
                        ExtraerImagenesDePagina(page, numeroPagina, etiqueta, op, resultado, hashesImagen);
                    }
                }
                else
                {
                    resultado.PaginasSinTexto++;

                    bool leTocaTurno = indiceEnBloque % pasoEscaneo == 0;
                    bool hayCupo = escaneadasDelBloque < op.MaxPaginasEscaneadasPorBloque
                                   && resultado.PaginasEscaneadas.Count < op.MaxPaginasEscaneadas;

                    if (leTocaTurno && hayCupo)
                    {
                        if (RescatarPaginaComoImagen(page, numeroPagina, etiqueta, op, resultado, hashesImagen))
                        {
                            escaneadasDelBloque++;
                        }
                        else
                        {
                            resultado.PaginasSinTextoNiImagen++;
                        }
                    }
                }

                resultado.PaginasLeidas++;
            }

            string textoBloque = Normalizar(sb.ToString());
            if (textoBloque.Length > 0)
            {
                resultado.Fragmentos.Add(new FragmentoTexto
                {
                    PaginaDesde = bloque[0],
                    PaginaHasta = bloque[^1],
                    Etiqueta = etiqueta,
                    Texto = textoBloque
                });
            }
        }

        resultado.HuboRecorte = AjustarPresupuesto(resultado.Fragmentos, op.MaxCaracteres);

        var informe = new StringBuilder();
        informe.Append($"PDF procesado: {resultado.PaginasLeidas} paginas leidas, ")
               .Append($"{resultado.CaracteresTotales:N0} caracteres, {resultado.Imagenes.Count} figuras");

        if (resultado.PaginasSinTexto > 0)
        {
            informe.Append($", {resultado.PaginasSinTexto} paginas sin texto ")
                   .Append($"({resultado.PaginasEscaneadas.Count} rescatadas como imagen)");
        }

        progreso?.Report(informe.Append('.').ToString());

        return resultado;
    }

    /// <summary>
    /// Guarda la imagen de una pagina que no dio texto, para mandarsela a Gemini y que la lea.
    /// Devuelve false si PdfPig no pudo decodificar ninguna imagen de la pagina, que es lo que
    /// pasa con los escaneos comprimidos en JBIG2 o JPEG 2000.
    /// </summary>
    private static bool RescatarPaginaComoImagen(
        Page page,
        int numeroPagina,
        string etiqueta,
        OpcionesExtraccion op,
        ExtraccionResultado resultado,
        HashSet<string> hashes)
    {
        List<IPdfImage> candidatas;
        try
        {
            candidatas = page.GetImages()
                .Where(i => i.WidthInSamples >= op.MinLadoPaginaEscaneada
                            && i.HeightInSamples >= op.MinLadoPaginaEscaneada)
                .OrderByDescending(i => (long)i.WidthInSamples * i.HeightInSamples)
                .ToList();
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError($"PdfPig.GetImages(pagina escaneada {numeroPagina})", ex);
            return false;
        }

        if (candidatas.Count == 0)
        {
            return false;
        }

        // Casi siempre la pagina es una sola imagen. Si viene partida en tiras se toma
        // tambien la segunda, siempre que sea comparable en tamanio a la primera.
        long mayor = (long)candidatas[0].WidthInSamples * candidatas[0].HeightInSamples;
        var elegidas = candidatas
            .Take(2)
            .Where(i => (long)i.WidthInSamples * i.HeightInSamples >= mayor * 6 / 10)
            .ToList();

        bool alguna = false;
        int parte = 0;

        foreach (var img in elegidas)
        {
            parte++;

            try
            {
                if (!TryObtenerBytes(img, out byte[] crudos, out _, out _))
                {
                    continue;
                }

                string hash = Convert.ToHexString(SHA1.HashData(crudos));
                if (!hashes.Add(hash))
                {
                    continue;
                }

                var (bytes, mime) = ImagenUtil.PrepararParaLectura(crudos, op.LadoMaximoPaginaEscaneada);

                string identificador = elegidas.Count > 1
                    ? $"pagina_{numeroPagina}_{parte}.jpg"
                    : $"pagina_{numeroPagina}.jpg";

                string destino = Path.Combine(op.CarpetaImagenes, identificador);
                File.WriteAllBytes(destino, bytes);

                resultado.PaginasEscaneadas.Add(new ImagenExtraida
                {
                    Identificador = identificador,
                    Ruta = destino,
                    MimeType = mime,
                    Pagina = numeroPagina,
                    Ancho = img.WidthInSamples,
                    Alto = img.HeightInSamples,
                    Etiqueta = etiqueta,
                    YaPreparada = true
                });

                alguna = true;
            }
            catch (Exception ex)
            {
                RutasApp.RegistrarError($"RescatarPaginaComoImagen({numeroPagina}, parte {parte})", ex);
            }
        }

        return alguna;
    }

    /// <summary>Cuenta solo caracteres con contenido: un salto de linea no es texto extraido.</summary>
    private static int ContarUtiles(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return 0;
        }

        int n = 0;
        foreach (char c in texto)
        {
            if (!char.IsWhiteSpace(c))
            {
                n++;
            }
        }

        return n;
    }

    private static ParsingOptions OpcionesLectura() => new()
    {
        UseLenientParsing = true,
        SkipMissingFonts = true,
        ClipPaths = false
    };

    // ------------------------------------------------------------------
    // Recorte para la Files API
    // ------------------------------------------------------------------

    /// <summary>
    /// Arma un PDF nuevo con solo las paginas del alcance elegido, conservando el orden y
    /// admitiendo rangos salteados (capitulos 1, 2, 5, 7...).
    ///
    /// Existe para la subida a la Files API: si el usuario eligio tres capitulos de un libro
    /// de 900 paginas, subir el libro entero le haria leer a Gemini 900 paginas para preguntar
    /// sobre 60. Recortar antes es mas rapido, mas barato y ademas evita que el modelo se
    /// vaya de tema, que era el problema del eje tematico.
    /// </summary>
    /// <returns>
    /// La ruta del PDF recortado y cuantas paginas tiene, o <c>null</c> si el recorte no
    /// pudo hacerse (PDF con estructuras que PdfPig no sabe copiar). El llamador debe
    /// tratar ese null como "seguir por el camino de texto", nunca como un error fatal.
    /// </returns>
    public Task<(string ruta, int paginas)?> RecortarAsync(
        string rutaPdf,
        IReadOnlyList<RangoPaginas> rangos,
        string rutaDestino,
        CancellationToken ct = default)
    {
        return Task.Run<(string, int)?>(() =>
        {
            try
            {
                using var origen = PdfDocument.Open(rutaPdf, OpcionesLectura());

                var paginas = ExpandirPaginas(rangos, origen.NumberOfPages, out _);

                if (paginas.Count == 0)
                {
                    return null;
                }

                // El alcance es el libro entero: no hay nada que recortar y copiar pagina
                // por pagina solo agregaria riesgo de perder algo por el camino.
                if (paginas.Count == origen.NumberOfPages)
                {
                    return (rutaPdf, origen.NumberOfPages);
                }

                var builder = new PdfDocumentBuilder();
                int copiadas = 0;

                foreach (int p in paginas)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        builder.AddPage(origen, p);
                        copiadas++;
                    }
                    catch (Exception ex)
                    {
                        // Una pagina que no se deja copiar no invalida el resto del alcance.
                        RutasApp.RegistrarError($"RecortarPdf / pagina {p}", ex);
                    }
                }

                if (copiadas == 0)
                {
                    return null;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(rutaDestino)!);
                File.WriteAllBytes(rutaDestino, builder.Build());

                return (rutaDestino, copiadas);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                RutasApp.RegistrarError("RecortarPdf", ex);
                return null;
            }
        }, ct);
    }

    private static string LeerTextoPagina(Page page)
    {
        try
        {
            return ContentOrderTextExtractor.GetText(page) ?? string.Empty;
        }
        catch
        {
            // Algunas paginas con fuentes rotas fallan en el extractor ordenado: se usa el texto crudo.
            try
            {
                return page.Text ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private void ExtraerImagenesDePagina(
        Page page,
        int numeroPagina,
        string etiqueta,
        OpcionesExtraccion op,
        ExtraccionResultado resultado,
        HashSet<string> hashes)
    {
        IEnumerable<IPdfImage> imagenes;
        try
        {
            imagenes = page.GetImages();
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError($"PdfPig.GetImages(pag {numeroPagina})", ex);
            return;
        }

        int indice = 0;
        foreach (var img in imagenes)
        {
            if (resultado.Imagenes.Count >= op.MaxImagenes)
            {
                return;
            }

            indice++;

            try
            {
                // Descarta iconos, vinetas, filetes y logos de encabezado.
                if (img.WidthInSamples < op.MinAnchoImagen || img.HeightInSamples < op.MinAltoImagen)
                {
                    continue;
                }

                double relacion = (double)img.WidthInSamples / Math.Max(1, img.HeightInSamples);
                if (relacion > 8 || relacion < 0.125)
                {
                    continue;
                }

                if (!TryObtenerBytes(img, out byte[] bytes, out string extension, out string mime))
                {
                    continue;
                }

                string hash = Convert.ToHexString(SHA1.HashData(bytes));
                if (!hashes.Add(hash))
                {
                    continue; // Imagen repetida (marca de agua, logo por pagina).
                }

                string identificador = $"img_p{numeroPagina}_{indice}{extension}";
                string destino = Path.Combine(op.CarpetaImagenes, identificador);
                File.WriteAllBytes(destino, bytes);

                resultado.Imagenes.Add(new ImagenExtraida
                {
                    Identificador = identificador,
                    Ruta = destino,
                    MimeType = mime,
                    Pagina = numeroPagina,
                    Ancho = img.WidthInSamples,
                    Alto = img.HeightInSamples,
                    Etiqueta = etiqueta
                });
            }
            catch (Exception ex)
            {
                RutasApp.RegistrarError($"ExtraerImagen(pag {numeroPagina}, #{indice})", ex);
            }
        }
    }

    private static bool TryObtenerBytes(IPdfImage img, out byte[] bytes, out string extension, out string mime)
    {
        bytes = Array.Empty<byte>();
        extension = ".png";
        mime = "image/png";

        // 1) PdfPig sabe reconstruir un PNG para la mayoria de los espacios de color.
        try
        {
            if (img.TryGetPng(out var png) && png is { Length: > 512 })
            {
                bytes = png;
                return true;
            }
        }
        catch
        {
            // Sigue con el plan B.
        }

        // 2) Si el stream original ya es un JPEG (DCTDecode), se guarda tal cual.
        try
        {
            var crudo = img.RawMemory.ToArray();
            if (crudo.Length > 512 && crudo[0] == 0xFF && crudo[1] == 0xD8)
            {
                bytes = crudo;
                extension = ".jpg";
                mime = "image/jpeg";
                return true;
            }
        }
        catch
        {
            // Formato no soportado: se ignora la imagen.
        }

        return false;
    }

    /// <summary>Convierte los rangos en una lista ordenada y sin repetidos de numeros de pagina.</summary>
    private static List<int> ExpandirPaginas(
        IReadOnlyList<RangoPaginas> rangos,
        int totalPaginas,
        out Dictionary<int, string> etiquetaPorPagina)
    {
        var set = new SortedSet<int>();
        etiquetaPorPagina = new Dictionary<int, string>();

        foreach (var rango in rangos)
        {
            int desde = Math.Max(1, Math.Min(rango.Desde, rango.Hasta));
            int hasta = Math.Min(totalPaginas, Math.Max(rango.Desde, rango.Hasta));

            for (int p = desde; p <= hasta; p++)
            {
                set.Add(p);
                if (!etiquetaPorPagina.ContainsKey(p))
                {
                    etiquetaPorPagina[p] = rango.Etiqueta;
                }
            }
        }

        return set.ToList();
    }

    private static List<List<int>> AgruparEnBloques(List<int> paginas, int tamanoBloque)
    {
        var bloques = new List<List<int>>();
        var actual = new List<int>();

        foreach (int p in paginas)
        {
            // Corta el bloque si se llena o si hay un salto (rangos no contiguos).
            if (actual.Count > 0 && (actual.Count >= tamanoBloque || p != actual[^1] + 1))
            {
                bloques.Add(actual);
                actual = new List<int>();
            }

            actual.Add(p);
        }

        if (actual.Count > 0)
        {
            bloques.Add(actual);
        }

        return bloques;
    }

    /// <summary>Toma <paramref name="cantidad"/> bloques repartidos parejo a lo largo de toda la seleccion.</summary>
    private static List<List<int>> MuestrearUniforme(List<List<int>> bloques, int cantidad)
    {
        var muestra = new List<List<int>>(cantidad);
        double paso = (double)bloques.Count / cantidad;

        for (int i = 0; i < cantidad; i++)
        {
            int indice = (int)Math.Floor(i * paso);
            indice = Math.Clamp(indice, 0, bloques.Count - 1);

            if (muestra.Count == 0 || !ReferenceEquals(muestra[^1], bloques[indice]))
            {
                muestra.Add(bloques[indice]);
            }
        }

        return muestra;
    }

    /// <summary>
    /// Recorta el texto para que entre en la ventana de contexto, repartiendo el presupuesto
    /// entre todos los fragmentos en lugar de descartar el final del temario.
    /// </summary>
    private static bool AjustarPresupuesto(List<FragmentoTexto> fragmentos, int maxCaracteres)
    {
        int total = fragmentos.Sum(f => f.Texto.Length);
        if (total <= maxCaracteres || fragmentos.Count == 0)
        {
            return false;
        }

        int cuota = Math.Max(1_200, maxCaracteres / fragmentos.Count);

        foreach (var f in fragmentos)
        {
            if (f.Texto.Length > cuota)
            {
                f.Texto = CortarEnPalabra(f.Texto, cuota);
            }
        }

        // Si aun asi no entra (muchisimos fragmentos), se descartan de a uno intercalado.
        while (fragmentos.Sum(f => f.Texto.Length) > maxCaracteres && fragmentos.Count > 1)
        {
            fragmentos.RemoveAt(fragmentos.Count / 2);
        }

        return true;
    }

    private static string CortarEnPalabra(string texto, int limite)
    {
        if (texto.Length <= limite)
        {
            return texto;
        }

        int corte = texto.LastIndexOf(' ', Math.Min(limite, texto.Length - 1));
        if (corte < limite / 2)
        {
            corte = limite;
        }

        return texto[..corte] + " [...]";
    }

    private static string Normalizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        texto = texto.Replace(' ', ' ').Replace("\r\n", "\n");
        texto = EspaciosMultiples.Replace(texto, " ");
        texto = SaltosMultiples.Replace(texto, "\n\n");
        return texto.Trim();
    }
}
