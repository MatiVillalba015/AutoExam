using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>
/// Extractor de <see cref="IExtractorContenido"/> para los formatos Office modernos
/// <c>.docx</c> / <c>.xlsx</c> / <c>.pptx</c> (US-008, arquitectura Inc-4 §4.1 y §1.2).
///
/// OOXML es un contenedor OPC (ZIP): se abre con <see cref="ZipArchive"/> y se lee <b>una parte
/// por vez</b> con <see cref="XmlReader"/> forward-only — nunca se descomprime el contenedor
/// entero ni se materializa una parte completa en memoria (NFR-38/NFR-39/NFR-A3). La acumulacion
/// de texto se corta <b>durante</b> la lectura al superar <see cref="Presupuesto"/> (2×
/// <see cref="OpcionesExtraccion.MaxCaracteres"/>), igual que <c>PdfExtractorService</c> corta por
/// <c>MaxCaracteres</c>/<c>MaxPaginasLeidas</c> mientras recorre — un <c>.xlsx</c> de millones de
/// filas no llega a llenar la RAM del proceso antes del recorte.
///
/// Alcance v1 (R-17): texto de cuerpo de Word, celdas de Excel (shared / inline / valor) y texto
/// de diapositivas de PowerPoint. Notas del orador, cuadros de texto anidados y comentarios
/// quedan como mejora futura. <c>.doc</c> / <c>.xls</c> / <c>.ppt</c> (OLE2) no llegan aca: la
/// factory ya los rechaza por extension (RN-8).
/// </summary>
public sealed class OfficeExtractor : IExtractorContenido
{
    private static readonly string[] Extensiones = { ".docx", ".xlsx", ".pptx" };

    private static readonly Regex RegexHoja =
        new(@"^xl/worksheets/sheet(\d+)\.xml$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegexDiapositiva =
        new(@"^ppt/slides/slide(\d+)\.xml$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly XmlReaderSettings LectorXml = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CloseInput = true,
        XmlResolver = null,
    };

    public bool Soporta(string extension) =>
        Extensiones.Contains(Normalizar(extension));

    /// <summary>Medida por formato (NFR-40): Word → "documento unico"; Excel → "N hojas · ~M filas";
    /// PowerPoint → "N diapositivas". No extrae texto — solo cuenta partes/filas.</summary>
    public Task<MedidaFuente> MedirAsync(IReadOnlyList<string> rutas, CancellationToken ct)
        => Task.Run(() => Medir(PrimeraRuta(rutas), ct), ct);

    public Task<ExtraccionResultado> ExtraerAsync(
        IReadOnlyList<string> rutas,
        RecorteFuente recorte,
        OpcionesExtraccion opciones,
        IProgress<string>? progreso,
        CancellationToken ct)
        => Task.Run(() => Extraer(PrimeraRuta(rutas), opciones ?? new OpcionesExtraccion(), progreso, ct), ct);

    // ------------------------------------------------------------------
    // Medida
    // ------------------------------------------------------------------

    private static MedidaFuente Medir(string ruta, CancellationToken ct)
    {
        string ext = Normalizar(Path.GetExtension(ruta));

        using var zip = AbrirContenedor(ruta);

        switch (ext)
        {
            case ".docx":
                if (Buscar(zip, "word/document.xml") is null)
                {
                    throw FaltaParte("word/document.xml");
                }

                return new MedidaFuente(TipoFuente.Word, "documento unico");

            case ".xlsx":
            {
                var hojas = PartesOrdenadas(zip, RegexHoja);
                if (hojas.Count == 0)
                {
                    throw FaltaParte("xl/worksheets/sheet1.xml");
                }

                long filas = 0;
                foreach (var hoja in hojas)
                {
                    ct.ThrowIfCancellationRequested();
                    filas += ContarFilas(hoja, ct);
                }

                string hojasTxt = hojas.Count == 1 ? "1 hoja" : $"{hojas.Count} hojas";
                return new MedidaFuente(TipoFuente.Excel, $"{hojasTxt} · ~{Abreviar(filas)} filas");
            }

            case ".pptx":
            {
                int diapositivas = PartesOrdenadas(zip, RegexDiapositiva).Count;
                if (diapositivas == 0)
                {
                    throw FaltaParte("ppt/slides/slide1.xml");
                }

                string txt = diapositivas == 1 ? "1 diapositiva" : $"{diapositivas} diapositivas";
                return new MedidaFuente(TipoFuente.PowerPoint, txt);
            }

            default:
                throw new FormatoNoSoportadoException();
        }
    }

    // ------------------------------------------------------------------
    // Extraccion
    // ------------------------------------------------------------------

    private static ExtraccionResultado Extraer(
        string ruta, OpcionesExtraccion op, IProgress<string>? progreso, CancellationToken ct)
    {
        string ext = Normalizar(Path.GetExtension(ruta));

        using var zip = AbrirContenedor(ruta);

        var presupuesto = new Presupuesto(op.MaxCaracteres);

        var resultado = ext switch
        {
            ".docx" => ExtraerWord(zip, presupuesto, ct),
            ".xlsx" => ExtraerExcel(zip, presupuesto, progreso, ct),
            ".pptx" => ExtraerPowerPoint(zip, presupuesto, ct),
            _ => throw new FormatoNoSoportadoException(),
        };

        if (presupuesto.Excedido)
        {
            // Se dejo de leer material antes de terminar el archivo (NFR-39): el recorte fino a
            // MaxCaracteres exacto lo hace AjustarPresupuesto, pero el flag ya vale.
            resultado.HuboRecorte = true;
        }

        AjustarPresupuesto(resultado, op.MaxCaracteres);
        return resultado;
    }

    private static ExtraccionResultado ExtraerWord(ZipArchive zip, Presupuesto presupuesto, CancellationToken ct)
    {
        var entrada = Buscar(zip, "word/document.xml") ?? throw FaltaParte("word/document.xml");

        var parrafos = new List<string>();
        var actual = new StringBuilder();
        bool enTexto = false;

        using (var s = entrada.Open())
        using (var reader = XmlReader.Create(s, LectorXml))
        {
            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();

                switch (reader.NodeType)
                {
                    case XmlNodeType.Element when reader.LocalName == "t":
                        enTexto = !reader.IsEmptyElement;
                        break;

                    case XmlNodeType.Element when reader.LocalName is "tab":
                        actual.Append(' ');
                        break;

                    case XmlNodeType.Element when reader.LocalName is "br" or "cr":
                        actual.Append('\n');
                        break;

                    case XmlNodeType.Text:
                    case XmlNodeType.SignificantWhitespace:
                    case XmlNodeType.Whitespace:
                        if (enTexto)
                        {
                            actual.Append(reader.Value);
                        }

                        break;

                    case XmlNodeType.EndElement when reader.LocalName == "t":
                        enTexto = false;
                        break;

                    case XmlNodeType.EndElement when reader.LocalName == "p":
                        presupuesto.Sumar(VolcarParrafo(parrafos, actual));
                        break;
                }

                if (presupuesto.Excedido)
                {
                    break;
                }
            }
        }

        VolcarParrafo(parrafos, actual);

        var resultado = new ExtraccionResultado();
        string texto = string.Join("\n", parrafos).Trim();

        if (texto.Length > 0)
        {
            resultado.Fragmentos.Add(new FragmentoTexto
            {
                PaginaDesde = 1,
                PaginaHasta = 1,
                Etiqueta = "documento",
                Texto = texto,
            });
        }

        return resultado;
    }

    private static ExtraccionResultado ExtraerPowerPoint(ZipArchive zip, Presupuesto presupuesto, CancellationToken ct)
    {
        var resultado = new ExtraccionResultado();
        var diapositivas = PartesOrdenadas(zip, RegexDiapositiva);

        int numero = 0;
        foreach (var slide in diapositivas)
        {
            ct.ThrowIfCancellationRequested();
            numero++;

            var parrafos = new List<string>();
            var actual = new StringBuilder();
            bool enTexto = false;

            using (var s = slide.Open())
            using (var reader = XmlReader.Create(s, LectorXml))
            {
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();

                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element when reader.LocalName == "t":
                            enTexto = !reader.IsEmptyElement;
                            break;

                        case XmlNodeType.Text:
                        case XmlNodeType.SignificantWhitespace:
                        case XmlNodeType.Whitespace:
                            if (enTexto)
                            {
                                actual.Append(reader.Value);
                            }

                            break;

                        case XmlNodeType.EndElement when reader.LocalName == "t":
                            enTexto = false;
                            break;

                        case XmlNodeType.EndElement when reader.LocalName == "p":
                            VolcarParrafo(parrafos, actual);
                            break;
                    }
                }
            }

            VolcarParrafo(parrafos, actual);

            string texto = string.Join("\n", parrafos).Trim();
            if (texto.Length == 0)
            {
                continue;
            }

            resultado.Fragmentos.Add(new FragmentoTexto
            {
                PaginaDesde = numero,
                PaginaHasta = numero,
                Etiqueta = $"diapositiva {numero}",
                Texto = texto,
            });

            presupuesto.Sumar(texto.Length);
            if (presupuesto.Excedido)
            {
                // No se leen mas diapositivas: el material ya supera el presupuesto (NFR-39).
                break;
            }
        }

        return resultado;
    }

    private static ExtraccionResultado ExtraerExcel(
        ZipArchive zip, Presupuesto presupuesto, IProgress<string>? progreso, CancellationToken ct)
    {
        var resultado = new ExtraccionResultado();

        var compartidas = LeerSharedStrings(zip, presupuesto, ct);
        var hojas = PartesOrdenadas(zip, RegexHoja);

        int numero = 0;
        foreach (var hoja in hojas)
        {
            ct.ThrowIfCancellationRequested();
            numero++;
            progreso?.Report($"Leyendo Excel: hoja {numero}/{hojas.Count}...");

            var filas = new List<string>();
            var fila = new StringBuilder();

            string tipoCelda = string.Empty;
            var valor = new StringBuilder();
            var inline = new StringBuilder();
            bool enValor = false;
            bool enInline = false;

            using (var s = hoja.Open())
            using (var reader = XmlReader.Create(s, LectorXml))
            {
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();

                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element when reader.LocalName == "c":
                            tipoCelda = reader.GetAttribute("t") ?? string.Empty;
                            valor.Clear();
                            inline.Clear();
                            if (reader.IsEmptyElement)
                            {
                                tipoCelda = string.Empty;
                            }

                            break;

                        case XmlNodeType.Element when reader.LocalName == "v":
                            enValor = !reader.IsEmptyElement;
                            break;

                        case XmlNodeType.Element when reader.LocalName == "t":
                            enInline = !reader.IsEmptyElement;
                            break;

                        case XmlNodeType.Text:
                        case XmlNodeType.SignificantWhitespace:
                        case XmlNodeType.Whitespace:
                            if (enValor)
                            {
                                valor.Append(reader.Value);
                            }
                            else if (enInline)
                            {
                                inline.Append(reader.Value);
                            }

                            break;

                        case XmlNodeType.EndElement when reader.LocalName == "v":
                            enValor = false;
                            break;

                        case XmlNodeType.EndElement when reader.LocalName == "t":
                            enInline = false;
                            break;

                        case XmlNodeType.EndElement when reader.LocalName == "c":
                            AgregarCelda(fila, tipoCelda, valor.ToString(), inline.ToString(), compartidas);
                            tipoCelda = string.Empty;
                            break;

                        case XmlNodeType.EndElement when reader.LocalName == "row":
                            if (fila.Length > 0)
                            {
                                string linea = fila.ToString().TrimEnd();
                                filas.Add(linea);
                                fila.Clear();
                                presupuesto.Sumar(linea.Length);
                            }

                            break;
                    }

                    if (presupuesto.Excedido)
                    {
                        break;
                    }
                }
            }

            if (fila.Length > 0)
            {
                filas.Add(fila.ToString().TrimEnd());
            }

            if (filas.Count > 0)
            {
                resultado.Fragmentos.Add(new FragmentoTexto
                {
                    PaginaDesde = numero,
                    PaginaHasta = numero,
                    Etiqueta = $"hoja {numero}",
                    Texto = string.Join("\n", filas),
                });
            }

            if (presupuesto.Excedido)
            {
                // Se corta antes de abrir la hoja siguiente (NFR-39/NFR-A3).
                break;
            }
        }

        return resultado;
    }

    // ------------------------------------------------------------------
    // Helpers de OOXML
    // ------------------------------------------------------------------

    private static List<string> LeerSharedStrings(ZipArchive zip, Presupuesto presupuesto, CancellationToken ct)
    {
        var lista = new List<string>();
        var entrada = Buscar(zip, "xl/sharedStrings.xml");
        if (entrada is null)
        {
            return lista;
        }

        var actual = new StringBuilder();
        bool enTexto = false;
        bool enItem = false;

        using var s = entrada.Open();
        using var reader = XmlReader.Create(s, LectorXml);

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();

            switch (reader.NodeType)
            {
                case XmlNodeType.Element when reader.LocalName == "si":
                    enItem = true;
                    actual.Clear();
                    if (reader.IsEmptyElement)
                    {
                        lista.Add(string.Empty);
                        enItem = false;
                    }

                    break;

                case XmlNodeType.Element when reader.LocalName == "t":
                    enTexto = enItem && !reader.IsEmptyElement;
                    break;

                case XmlNodeType.Text:
                case XmlNodeType.SignificantWhitespace:
                case XmlNodeType.Whitespace:
                    if (enTexto)
                    {
                        actual.Append(reader.Value);
                    }

                    break;

                case XmlNodeType.EndElement when reader.LocalName == "t":
                    enTexto = false;
                    break;

                case XmlNodeType.EndElement when reader.LocalName == "si":
                    string si = actual.ToString();
                    lista.Add(si);
                    enItem = false;
                    presupuesto.Sumar(si.Length);
                    break;
            }

            if (presupuesto.Excedido)
            {
                // Tabla de cadenas ya mas grande que el presupuesto: las celdas que apunten a
                // indices no leidos quedan vacias (degradacion aceptable a ese tamanio).
                break;
            }
        }

        return lista;
    }

    private static void AgregarCelda(
        StringBuilder fila, string tipo, string valor, string inline, List<string> compartidas)
    {
        string texto = tipo switch
        {
            "s" when int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)
                     && i >= 0 && i < compartidas.Count
                => compartidas[i],
            "inlineStr" => inline,
            "str" => valor,
            "b" => valor == "1" ? "verdadero" : "falso",
            _ => valor,
        };

        texto = texto.Trim();
        if (texto.Length == 0)
        {
            return;
        }

        if (fila.Length > 0)
        {
            fila.Append(' ');
        }

        fila.Append(texto);
    }

    private static long ContarFilas(ZipArchiveEntry hoja, CancellationToken ct)
    {
        long filas = 0;

        using var s = hoja.Open();
        using var reader = XmlReader.Create(s, LectorXml);

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();

            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "row")
            {
                filas++;
            }
        }

        return filas;
    }

    private static List<ZipArchiveEntry> PartesOrdenadas(ZipArchive zip, Regex patron) =>
        zip.Entries
            .Select(e => (e, m: patron.Match(e.FullName)))
            .Where(x => x.m.Success)
            .OrderBy(x => int.Parse(x.m.Groups[1].Value, CultureInfo.InvariantCulture))
            .Select(x => x.e)
            .ToList();

    private static ZipArchiveEntry? Buscar(ZipArchive zip, string nombre) =>
        zip.GetEntry(nombre)
        ?? zip.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName, nombre, StringComparison.OrdinalIgnoreCase));

    private static ZipArchive AbrirContenedor(string ruta)
    {
        try
        {
            return ZipFile.OpenRead(ruta);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            // OOXML cifrado con contrasenia es un contenedor OLE2, no un ZIP: cae aca igual que
            // un archivo danado. El llamador traduce esto a un aviso, nunca a un crash (NFR-37).
            throw new FuenteIlegibleException(
                "No se pudo abrir el archivo de Office: puede estar danado o protegido con contrasenia.", ex);
        }
    }

    private static FuenteIlegibleException FaltaParte(string parte) =>
        new($"El archivo de Office no tiene la parte '{parte}': puede estar danado o incompleto.");

    /// <summary>Agrega el contenido acumulado como parrafo (si tiene texto util) y devuelve cuantos
    /// caracteres se agregaron — para descontarlos del <see cref="Presupuesto"/>.</summary>
    private static long VolcarParrafo(List<string> parrafos, StringBuilder actual)
    {
        string linea = actual.ToString().Trim();
        actual.Clear();

        if (linea.Length == 0)
        {
            return 0;
        }

        parrafos.Add(linea);
        return linea.Length;
    }

    /// <summary>
    /// Recorte fino final a <see cref="OpcionesExtraccion.MaxCaracteres"/> exacto, repartiendo
    /// parejo entre fragmentos en vez de cortar el final del material — mismo criterio que
    /// <c>PdfExtractorService</c>. La guarda contra RAM ya la aplico <see cref="Presupuesto"/>
    /// durante la lectura; esto solo ajusta el 2× de margen que quedo bufferizado.
    /// </summary>
    private static void AjustarPresupuesto(ExtraccionResultado resultado, int maxCaracteres)
    {
        var fragmentos = resultado.Fragmentos;
        long total = fragmentos.Sum(f => (long)f.Texto.Length);

        if (maxCaracteres <= 0 || total <= maxCaracteres || fragmentos.Count == 0)
        {
            return;
        }

        int cuota = Math.Max(1_200, maxCaracteres / fragmentos.Count);

        foreach (var f in fragmentos)
        {
            if (f.Texto.Length > cuota)
            {
                f.Texto = CortarEnPalabra(f.Texto, cuota);
            }
        }

        while (fragmentos.Sum(f => (long)f.Texto.Length) > maxCaracteres && fragmentos.Count > 1)
        {
            fragmentos.RemoveAt(fragmentos.Count / 2);
        }

        resultado.HuboRecorte = true;
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

    private static string Abreviar(long n) =>
        n < 1_000
            ? n.ToString(CultureInfo.InvariantCulture)
            : (n / 1_000.0).ToString("0.#", CultureInfo.InvariantCulture) + "k";

    private static string PrimeraRuta(IReadOnlyList<string> rutas) =>
        rutas is { Count: > 0 } && !string.IsNullOrWhiteSpace(rutas[0])
            ? rutas[0]
            : throw new FuenteIlegibleException("No se indico ningun archivo de Office para extraer.");

    private static string Normalizar(string? extension) =>
        (extension ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Tope de acumulacion de texto en RAM durante la lectura (NFR-39/NFR-A3). Se fija en 2× el
    /// presupuesto final (<see cref="OpcionesExtraccion.MaxCaracteres"/>) con un piso de 40k para
    /// que un <c>MaxCaracteres</c> chico no mutile material de un archivo normal; el recorte a la
    /// cifra exacta lo hace despues <see cref="AjustarPresupuesto"/>.
    /// </summary>
    private sealed class Presupuesto
    {
        private readonly long _limite;

        public Presupuesto(int maxCaracteres) =>
            _limite = maxCaracteres > 0 ? Math.Max(40_000L, maxCaracteres * 2L) : long.MaxValue;

        public long Acumulado { get; private set; }

        public bool Excedido { get; private set; }

        public void Sumar(long caracteres)
        {
            if (caracteres <= 0)
            {
                return;
            }

            Acumulado += caracteres;
            if (Acumulado >= _limite)
            {
                Excedido = true;
            }
        }
    }
}
