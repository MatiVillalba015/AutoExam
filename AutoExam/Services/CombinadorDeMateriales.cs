namespace AutoExam.Services;

/// <summary>Un documento ya extraido, listo para fusionarse con los demas (US-024).</summary>
public sealed record ParteDelMaterial(string Documento, ExtraccionResultado Resultado);

/// <summary>
/// Fusiona la extraccion de varios documentos de una misma materia en un unico
/// <see cref="ExtraccionResultado"/>, para que Gemini reciba un solo material combinado
/// (US-024) en vez de N pedidos separados.
///
/// No extrae nada: cada documento ya paso por el extractor que le corresponde
/// (<c>FactoriaExtractores.Para</c>), asi que un PDF con texto, un .docx con fotos pegadas
/// y un set de imagenes llegan aca resueltos y se combinan igual (US-008/US-014/US-022).
///
/// Hay tres cosas que se arreglan al fusionar, y ninguna es cosmetica:
///
///  · <b>Trazabilidad (RN-24).</b> Cada fragmento pasa a nombrar su documento en la
///    etiqueta, asi que el material que ve el modelo dice "--- Guyton, pags. 12-20 ---".
///    Sin eso, "pagina 12" en un examen de tres apuntes no ubica nada: la pagina 12
///    existe en los tres.
///
///  · <b>Choque de identificadores.</b> Dos documentos producen figuras con el mismo
///    nombre ("fig_01"): el extractor las numera desde uno en cada archivo. Si no se
///    prefijan, la pregunta que pide "fig_01" muestra la figura del documento equivocado.
///
///  · <b>Cuota (RN-3).</b> Los topes de <see cref="OpcionesExtraccion"/> son por documento.
///    Sumar cinco materiales sin volver a aplicarlos mandaria cinco veces el presupuesto de
///    texto y de imagenes en un solo pedido, que es justo lo que hace que el modelo corte la
///    respuesta a mitad de camino.
/// </summary>
public static class CombinadorDeMateriales
{
    /// <summary>
    /// Une las partes en un unico resultado. Con una sola parte devuelve su resultado tal
    /// cual: un examen de un documento tiene que seguir comportandose exactamente como antes
    /// de US-024, sin etiquetas ni prefijos que no aportan nada cuando no hay con que
    /// confundirlo.
    /// </summary>
    public static ExtraccionResultado Combinar(IReadOnlyList<ParteDelMaterial> partes, OpcionesExtraccion op)
    {
        if (partes is null || partes.Count == 0)
        {
            return new ExtraccionResultado();
        }

        if (partes.Count == 1)
        {
            return partes[0].Resultado;
        }

        var combinado = new ExtraccionResultado();

        for (int i = 0; i < partes.Count; i++)
        {
            var parte = partes[i];
            string documento = Nombre(parte.Documento, i);

            foreach (var f in parte.Resultado.Fragmentos)
            {
                combinado.Fragmentos.Add(new FragmentoTexto
                {
                    PaginaDesde = f.PaginaDesde,
                    PaginaHasta = f.PaginaHasta,
                    Etiqueta = Etiquetar(documento, f.Etiqueta),
                    Texto = f.Texto
                });
            }

            combinado.PaginasSeleccionadas += parte.Resultado.PaginasSeleccionadas;
            combinado.PaginasLeidas += parte.Resultado.PaginasLeidas;
            combinado.PaginasSinTexto += parte.Resultado.PaginasSinTexto;
            combinado.PaginasSinTextoNiImagen += parte.Resultado.PaginasSinTextoNiImagen;

            combinado.HuboMuestreo |= parte.Resultado.HuboMuestreo;
            combinado.HuboRecorte |= parte.Resultado.HuboRecorte;
        }

        // Las imagenes se toman intercalando documentos, no uno tras otro: un apunte con
        // treinta figuras se llevaria el cupo entero y los otros dos quedarian sin ninguna
        // representacion visual en el examen.
        foreach (var img in Repartir(partes, p => p.Resultado.Imagenes, op.MaxImagenes))
        {
            combinado.Imagenes.Add(img);
        }

        foreach (var pagina in Repartir(partes, p => p.Resultado.PaginasEscaneadas, op.MaxPaginasEscaneadas))
        {
            combinado.PaginasEscaneadas.Add(pagina);
        }

        combinado.HuboRecorte |= AjustarPresupuesto(combinado.Fragmentos, op.MaxCaracteres);

        return combinado;
    }

    /// <summary>
    /// Etiqueta de un fragmento ya combinado: el documento primero, y detras el modulo o
    /// capitulo si el extractor lo habia puesto. <see cref="FragmentoTexto.Referencia"/> le
    /// agrega despues el rango de paginas.
    /// </summary>
    private static string Etiquetar(string documento, string etiquetaOriginal) =>
        string.IsNullOrWhiteSpace(etiquetaOriginal)
            ? documento
            : $"{documento} · {etiquetaOriginal}";

    /// <summary>
    /// Toma imagenes de a una por documento, en rondas, hasta llegar al tope. Devuelve
    /// copias con el identificador prefijado por documento (<c>d1_fig_01</c>): el numero de
    /// documento evita el choque, y el identificador original se conserva para que siga
    /// siendo legible en el prompt.
    /// </summary>
    private static List<ImagenExtraida> Repartir(
        IReadOnlyList<ParteDelMaterial> partes,
        Func<ParteDelMaterial, List<ImagenExtraida>> cual,
        int tope)
    {
        var elegidas = new List<ImagenExtraida>();

        if (tope <= 0)
        {
            return elegidas;
        }

        int maxPorDocumento = partes.Max(p => cual(p).Count);

        for (int vuelta = 0; vuelta < maxPorDocumento && elegidas.Count < tope; vuelta++)
        {
            for (int i = 0; i < partes.Count && elegidas.Count < tope; i++)
            {
                var lista = cual(partes[i]);
                if (vuelta >= lista.Count)
                {
                    continue;
                }

                var original = lista[vuelta];
                string documento = Nombre(partes[i].Documento, i);

                elegidas.Add(new ImagenExtraida
                {
                    Identificador = $"d{i + 1}_{original.Identificador}",
                    Ruta = original.Ruta,
                    MimeType = original.MimeType,
                    Pagina = original.Pagina,
                    Ancho = original.Ancho,
                    Alto = original.Alto,
                    Etiqueta = Etiquetar(documento, original.Etiqueta),
                    YaPreparada = original.YaPreparada
                });
            }
        }

        return elegidas;
    }

    private static string Nombre(string documento, int indice) =>
        string.IsNullOrWhiteSpace(documento) ? $"Documento {indice + 1}" : documento.Trim();

    /// <summary>
    /// Recorta el texto combinado al presupuesto global repartiendo parejo entre fragmentos,
    /// en vez de cortar por el final. Cortar por el final dejaria el ultimo documento de la
    /// seleccion sin una sola pregunta, que es exactamente lo que el alumno pidio evitar al
    /// marcar varios.
    ///
    /// Mismo criterio y misma cuota minima que el recorte propio de cada extractor
    /// (<c>PdfExtractorService.AjustarPresupuesto</c> y su par en <c>OfficeExtractor</c>);
    /// aca se vuelve a aplicar porque el tope de aquellos era por documento.
    /// </summary>
    private static bool AjustarPresupuesto(List<FragmentoTexto> fragmentos, int maxCaracteres)
    {
        long total = fragmentos.Sum(f => (long)f.Texto.Length);

        if (maxCaracteres <= 0 || total <= maxCaracteres || fragmentos.Count == 0)
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

        // Con muchisimos fragmentos la cuota minima puede no alcanzar: se descartan de a uno
        // desde el medio, que reparte la perdida entre todos los documentos en vez de vaciar
        // el ultimo.
        while (fragmentos.Sum(f => (long)f.Texto.Length) > maxCaracteres && fragmentos.Count > 1)
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
}
