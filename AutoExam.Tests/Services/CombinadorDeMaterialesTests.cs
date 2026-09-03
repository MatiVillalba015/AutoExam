using AutoExam.Services;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-024 / RN-24 — un unico examen a partir de varios documentos de una misma materia.
///
/// Cada documento ya paso por su extractor (US-008/US-014/US-022); lo que se prueba aca es
/// la fusion, que es donde estan las tres decisiones que importan: que cada pregunta pueda
/// seguir diciendo de que documento salio, que dos documentos no se pisen las figuras entre
/// si, y que sumar materiales no multiplique lo que viaja a Gemini.
/// </summary>
public class CombinadorDeMaterialesTests
{
    private static ExtraccionResultado Doc(
        string texto, string etiqueta = "", int figuras = 0, int escaneadas = 0, int desde = 1, int hasta = 10)
    {
        var r = new ExtraccionResultado();

        r.Fragmentos.Add(new FragmentoTexto
        {
            PaginaDesde = desde,
            PaginaHasta = hasta,
            Etiqueta = etiqueta,
            Texto = texto
        });

        for (int i = 0; i < figuras; i++)
        {
            r.Imagenes.Add(new ImagenExtraida
            {
                Identificador = $"fig_{i + 1:D2}.png",
                Ruta = $@"C:\img\fig_{i + 1:D2}.png",
                Pagina = i + 1
            });
        }

        for (int i = 0; i < escaneadas; i++)
        {
            r.PaginasEscaneadas.Add(new ImagenExtraida
            {
                Identificador = $"pag_{i + 1:D2}.jpg",
                Ruta = $@"C:\img\pag_{i + 1:D2}.jpg",
                Pagina = i + 1,
                YaPreparada = true
            });
        }

        return r;
    }

    private static OpcionesExtraccion Opciones(int maxCaracteres = 90_000, int maxImagenes = 12, int maxEscaneadas = 10)
        => new()
        {
            MaxCaracteres = maxCaracteres,
            MaxImagenes = maxImagenes,
            MaxPaginasEscaneadas = maxEscaneadas
        };

    private static string Relleno(int largo) => string.Join(" ", Enumerable.Repeat("palabra", largo / 8));

    // ------------------------------------------------------------------
    // AC — el contenido de todos los documentos entra al mismo examen
    // ------------------------------------------------------------------

    [Fact]
    public void ElMaterialCombinado_TraeElTextoDeTodosLosDocumentos()
    {
        var partes = new[]
        {
            new ParteDelMaterial("Guyton", Doc("membrana y potencial de accion")),
            new ParteDelMaterial("Lehninger", Doc("enzimas y cinetica"))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones());

        string todo = string.Join("\n", combinado.Fragmentos.Select(f => f.Texto));

        Assert.Contains("potencial de accion", todo);
        Assert.Contains("cinetica", todo);
    }

    [Fact]
    public void LosContadoresDePaginas_SeSuman()
    {
        var a = Doc("uno");
        a.PaginasLeidas = 30;
        a.PaginasSeleccionadas = 40;

        var b = Doc("dos");
        b.PaginasLeidas = 12;
        b.PaginasSeleccionadas = 15;

        var combinado = CombinadorDeMateriales.Combinar(
            new[] { new ParteDelMaterial("A", a), new ParteDelMaterial("B", b) }, Opciones());

        Assert.Equal(42, combinado.PaginasLeidas);
        Assert.Equal(55, combinado.PaginasSeleccionadas);
    }

    // ------------------------------------------------------------------
    // RN-24 — cada pregunta tiene que poder decir de que documento salio
    // ------------------------------------------------------------------

    [Fact]
    public void CadaFragmento_NombraSuDocumentoEnLaReferencia_RN24()
    {
        // La referencia es la cabecera que ve el modelo ("--- Guyton, pags. 1-10 ---") y de
        // ahi sale la cita de cada pregunta. Sin el documento, "pagina 12" no ubica nada:
        // la pagina 12 existe en los tres apuntes.
        var partes = new[]
        {
            new ParteDelMaterial("Guyton", Doc("a", desde: 1, hasta: 10)),
            new ParteDelMaterial("Lehninger", Doc("b", desde: 1, hasta: 10))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones());

        Assert.Contains(combinado.Fragmentos, f => f.Referencia.Contains("Guyton", StringComparison.Ordinal));
        Assert.Contains(combinado.Fragmentos, f => f.Referencia.Contains("Lehninger", StringComparison.Ordinal));
    }

    [Fact]
    public void ElCapituloDelDocumento_NoSePierdeAlAgregarleElTitulo()
    {
        var partes = new[]
        {
            new ParteDelMaterial("Guyton", Doc("a", etiqueta: "Capitulo 5")),
            new ParteDelMaterial("Lehninger", Doc("b"))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones());

        var conCapitulo = combinado.Fragmentos.First(f => f.Etiqueta.Contains("Guyton", StringComparison.Ordinal));

        Assert.Contains("Capitulo 5", conCapitulo.Etiqueta, StringComparison.Ordinal);
    }

    [Fact]
    public void UnDocumentoSinTitulo_IgualQuedaIdentificado()
    {
        var partes = new[]
        {
            new ParteDelMaterial("", Doc("a")),
            new ParteDelMaterial("Lehninger", Doc("b"))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones());

        Assert.All(combinado.Fragmentos, f => Assert.False(string.IsNullOrWhiteSpace(f.Etiqueta)));
    }

    // ------------------------------------------------------------------
    // Choque de identificadores entre documentos
    // ------------------------------------------------------------------

    [Fact]
    public void DosDocumentos_NoCompartenElIdentificadorDeUnaFigura()
    {
        // Cada extractor numera desde uno, asi que los dos producen "fig_01.png". Si no se
        // prefijaran, la pregunta que pide "fig_01" mostraria la figura del documento
        // equivocado — y no habria forma de notarlo mirando el examen.
        var partes = new[]
        {
            new ParteDelMaterial("Guyton", Doc("a", figuras: 2)),
            new ParteDelMaterial("Lehninger", Doc("b", figuras: 2))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones());

        var ids = combinado.Imagenes.Select(i => i.Identificador).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void LasPaginasEscaneadas_TampocoChocanEntreDocumentos()
    {
        var partes = new[]
        {
            new ParteDelMaterial("Apunte A", Doc("a", escaneadas: 3)),
            new ParteDelMaterial("Apunte B", Doc("b", escaneadas: 3))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones());

        var ids = combinado.PaginasEscaneadas.Select(i => i.Identificador).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void LaFigura_ConservaSuArchivoYSuFormato()
    {
        // Solo cambia el identificador: si se perdiera la ruta o el mime, la imagen no se
        // podria adjuntar al pedido ni mostrar despues en el historial (US-018).
        var partes = new[]
        {
            new ParteDelMaterial("Guyton", Doc("a", figuras: 1)),
            new ParteDelMaterial("Lehninger", Doc("b", figuras: 1))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones());

        Assert.All(combinado.Imagenes, i =>
        {
            Assert.False(string.IsNullOrWhiteSpace(i.Ruta));
            Assert.False(string.IsNullOrWhiteSpace(i.MimeType));
        });
    }

    [Fact]
    public void LaPaginaEscaneada_SigueMarcadaComoYaPreparada()
    {
        // Perder esa marca haria que se la vuelva a escalar y comprimir al armar el request.
        var partes = new[]
        {
            new ParteDelMaterial("A", Doc("a", escaneadas: 1)),
            new ParteDelMaterial("B", Doc("b", escaneadas: 1))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones());

        Assert.All(combinado.PaginasEscaneadas, p => Assert.True(p.YaPreparada));
    }

    // ------------------------------------------------------------------
    // RN-3 — combinar no multiplica lo que viaja
    // ------------------------------------------------------------------

    [Fact]
    public void ElTopeDeFiguras_EsDelExamen_NoDeCadaDocumento()
    {
        var partes = new[]
        {
            new ParteDelMaterial("A", Doc("a", figuras: 8)),
            new ParteDelMaterial("B", Doc("b", figuras: 8)),
            new ParteDelMaterial("C", Doc("c", figuras: 8))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones(maxImagenes: 6));

        Assert.Equal(6, combinado.Imagenes.Count);
    }

    [Fact]
    public void ElCupoDeFiguras_SeRepartePorRondasYNoSeLoLlevaElPrimero()
    {
        // Tomando en orden, el documento con muchas figuras se llevaria el cupo entero y los
        // otros dos quedarian sin ninguna representacion visual en el examen.
        var partes = new[]
        {
            new ParteDelMaterial("A", Doc("a", figuras: 20)),
            new ParteDelMaterial("B", Doc("b", figuras: 2)),
            new ParteDelMaterial("C", Doc("c", figuras: 2))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones(maxImagenes: 6));

        Assert.Contains(combinado.Imagenes, i => i.Identificador.StartsWith("d2_", StringComparison.Ordinal));
        Assert.Contains(combinado.Imagenes, i => i.Identificador.StartsWith("d3_", StringComparison.Ordinal));
    }

    [Fact]
    public void ElTopeDePaginasEscaneadas_TambienEsDelExamen()
    {
        // Cada pagina escaneada pesa cientos de KB en Base64: sin este tope, tres apuntes
        // fotografiados arman un request que el modelo corta a mitad de camino (RN-3).
        var partes = new[]
        {
            new ParteDelMaterial("A", Doc("a", escaneadas: 6)),
            new ParteDelMaterial("B", Doc("b", escaneadas: 6))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones(maxEscaneadas: 4));

        Assert.Equal(4, combinado.PaginasEscaneadas.Count);
    }

    [Fact]
    public void ElTextoCombinado_RespetaElPresupuestoGlobal()
    {
        var partes = new[]
        {
            new ParteDelMaterial("A", Doc(Relleno(9_000))),
            new ParteDelMaterial("B", Doc(Relleno(9_000))),
            new ParteDelMaterial("C", Doc(Relleno(9_000)))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones(maxCaracteres: 10_000));

        Assert.True(combinado.CaracteresTotales <= 10_000,
            $"El material combinado quedo en {combinado.CaracteresTotales} caracteres sobre un tope de 10.000.");
    }

    [Fact]
    public void AlRecortarPorPresupuesto_NingunDocumentoQuedaAfuera()
    {
        // Cortar por el final dejaria al ultimo documento sin una sola pregunta, que es
        // exactamente lo contrario de lo que el alumno pidio al marcar varios.
        var partes = new[]
        {
            new ParteDelMaterial("Guyton", Doc(Relleno(9_000))),
            new ParteDelMaterial("Lehninger", Doc(Relleno(9_000))),
            new ParteDelMaterial("Best", Doc(Relleno(9_000)))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones(maxCaracteres: 10_000));

        foreach (string doc in new[] { "Guyton", "Lehninger", "Best" })
        {
            Assert.Contains(combinado.Fragmentos, f => f.Etiqueta.Contains(doc, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ElRecorte_QuedaInformado()
    {
        var partes = new[]
        {
            new ParteDelMaterial("A", Doc(Relleno(9_000))),
            new ParteDelMaterial("B", Doc(Relleno(9_000)))
        };

        var combinado = CombinadorDeMateriales.Combinar(partes, Opciones(maxCaracteres: 5_000));

        Assert.True(combinado.HuboRecorte);
    }

    // ------------------------------------------------------------------
    // Un solo documento: nada tiene que cambiar respecto de antes de US-024
    // ------------------------------------------------------------------

    [Fact]
    public void ConUnSoloDocumento_ElResultadoPasaIntacto()
    {
        // Un examen de una sola fuente tiene que seguir comportandose igual que antes:
        // sin prefijos en los identificadores ni titulos pegados a las etiquetas, que no
        // aportan nada cuando no hay con que confundirlo.
        var original = Doc("texto", etiqueta: "Capitulo 5", figuras: 2);

        var combinado = CombinadorDeMateriales.Combinar(
            new[] { new ParteDelMaterial("Guyton", original) }, Opciones());

        Assert.Same(original, combinado);
        Assert.Equal("Capitulo 5", combinado.Fragmentos[0].Etiqueta);
        Assert.Equal("fig_01.png", combinado.Imagenes[0].Identificador);
    }

    [Fact]
    public void SinDocumentos_DevuelveUnResultadoVacio_SinRomper()
    {
        var combinado = CombinadorDeMateriales.Combinar(Array.Empty<ParteDelMaterial>(), Opciones());

        Assert.Empty(combinado.Fragmentos);
        Assert.False(combinado.TieneMaterial);
    }
}
