using System.IO;
using System.Text.Json.Serialization;

namespace AutoExam.Models;

/// <summary>
/// Analisis pedagogico devuelto por Gemini: por que la correcta lo es y por que
/// falla cada una de las restantes. <see cref="AnalisisPorOpcion"/> esta alineado por
/// indice con <see cref="Pregunta.Opciones"/>, por lo que sobrevive al remezclado
/// de opciones del Modo Revancha.
/// </summary>
public class AnalisisOpciones
{
    public string ExplicacionCorrecta { get; set; } = string.Empty;

    /// <summary>Una entrada por opcion (misma cantidad y orden que Opciones).</summary>
    public List<string> AnalisisPorOpcion { get; set; } = new();
}

public class Pregunta : ObservableBase
{
    private int? _indiceRespuestaUsuario;
    private EstadoPreguntaEnum _estado = EstadoPreguntaEnum.SinResponder;
    private ResultadoPreguntaEnum _resultado = ResultadoPreguntaEnum.Pendiente;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string TextoPregunta { get; set; } = string.Empty;

    /// <summary>Ruta absoluta al PNG extraido del PDF. Vacio si la pregunta es solo texto.</summary>
    public string? RutaImagenAdjunta { get; set; }

    public List<string> Opciones { get; set; } = new();

    public int IndiceRespuestaCorrecta { get; set; }

    public string JustificacionBibliografia { get; set; } = string.Empty;

    public AnalisisOpciones AnalisisOpciones { get; set; } = new();

    /// <summary>Pagina exacta del PDF de la que salio la respuesta. 0 si el modelo no la precisa.</summary>
    public int PaginaOrigen { get; set; }

    /// <summary>
    /// Tramo de paginas que alimento esta pregunta. Es el respaldo de
    /// <see cref="PaginaOrigen"/>: siempre es mejor decir "entre las paginas 120 y 180"
    /// que no decir nada, o peor, inventar una pagina exacta.
    /// </summary>
    public string PaginasAlcance { get; set; } = string.Empty;

    public string ModuloOrigen { get; set; } = string.Empty;

    /// <summary>
    /// Titulo del documento del que salio esta pregunta (RN-24). Solo se puebla cuando el
    /// examen combino varios documentos de una materia (US-024): con una sola fuente el dato
    /// seria el titulo del examen repetido en cada pregunta.
    ///
    /// Sin esto, "Pagina 12" en un examen armado con tres apuntes no alcanza para volver al
    /// material: la pagina 12 existe en los tres.
    /// </summary>
    public string DocumentoOrigen { get; set; } = string.Empty;

    /// <summary>
    /// Titulo del examen del que se tomo prestada esta pregunta (US-026). Solo se puebla en
    /// un examen de repaso, que mezcla preguntas de varios intentos anteriores.
    ///
    /// Es distinto de <see cref="DocumentoOrigen"/> y no lo reemplaza: uno dice de que
    /// apunte salio la pregunta, el otro en que examen la habias visto. En un repaso armado
    /// con examenes de materias distintas, saber de cual venia es lo que le da sentido al
    /// repaso mezclado.
    /// </summary>
    public string ExamenOrigen { get; set; } = string.Empty;

    // ---------- Estado en tiempo de ejecucion ----------

    public int? IndiceRespuestaUsuario
    {
        get => _indiceRespuestaUsuario;
        set
        {
            if (Set(ref _indiceRespuestaUsuario, value))
            {
                OnPropertyChanged(nameof(TextoRespuestaUsuario));
            }
        }
    }

    public EstadoPreguntaEnum Estado
    {
        get => _estado;
        set
        {
            if (Set(ref _estado, value))
            {
                OnPropertyChanged(nameof(EtiquetaEstado));
            }
        }
    }

    public ResultadoPreguntaEnum Resultado
    {
        get => _resultado;
        set
        {
            if (Set(ref _resultado, value))
            {
                OnPropertyChanged(nameof(TituloResultado));
                OnPropertyChanged(nameof(MuestraRespuestaCorrecta));
                OnPropertyChanged(nameof(MuestraAnalisisCompleto));
                OnPropertyChanged(nameof(EsPendiente));
            }
        }
    }

    // ---------- Ayudas para la vista ----------

    [JsonIgnore]
    public bool TieneImagen => !string.IsNullOrWhiteSpace(RutaImagenAdjunta) && File.Exists(RutaImagenAdjunta);

    [JsonIgnore]
    public string TextoRespuestaUsuario =>
        IndiceRespuestaUsuario is int i && i >= 0 && i < Opciones.Count
            ? $"{Letra(i)}. {Opciones[i]}"
            : "(sin responder)";

    [JsonIgnore]
    public string TextoRespuestaCorrecta =>
        IndiceRespuestaCorrecta >= 0 && IndiceRespuestaCorrecta < Opciones.Count
            ? $"{Letra(IndiceRespuestaCorrecta)}. {Opciones[IndiceRespuestaCorrecta]}"
            : string.Empty;

    /// <summary>
    /// De donde sale la respuesta, en una linea. Se muestra en la correccion de TODAS
    /// las preguntas, tambien las falladas: saber que hay que ir a leer la pagina 143
    /// no revela cual de las cuatro opciones era la correcta.
    /// </summary>
    [JsonIgnore]
    public string ReferenciaFuente
    {
        get
        {
            // Con varios documentos combinados (US-024) la pagina sola no ubica nada: la
            // pagina 12 existe en los tres apuntes. Cuando hay documento se lo nombra y se
            // deja de decir "del PDF", que ademas seria falso si el material era un .docx.
            string fuente = string.IsNullOrWhiteSpace(DocumentoOrigen)
                ? "del PDF"
                : $"de \"{DocumentoOrigen}\"";

            // En un examen de repaso (US-026) la misma linea dice ademas en que examen
            // anterior habias visto esta pregunta: es lo unico que permite ubicarla cuando
            // el repaso mezcla intentos de materias distintas.
            string repaso = string.IsNullOrWhiteSpace(ExamenOrigen)
                ? string.Empty
                : $" · del examen \"{ExamenOrigen}\"";

            if (PaginaOrigen > 0)
            {
                return $"Pagina {PaginaOrigen} {fuente}{repaso}";
            }

            if (string.IsNullOrWhiteSpace(PaginasAlcance))
            {
                // Un documento sin pagina util igual es trazabilidad: dice de cual de los
                // materiales combinados salio la pregunta.
                if (!string.IsNullOrWhiteSpace(DocumentoOrigen))
                {
                    return $"De \"{DocumentoOrigen}\"{repaso}";
                }

                return repaso.Length == 0 ? string.Empty : $"Del examen \"{ExamenOrigen}\"";
            }

            string tramo = char.ToUpperInvariant(PaginasAlcance[0]) + PaginasAlcance[1..];
            return $"{tramo} {fuente} (el modelo no preciso una pagina exacta){repaso}";
        }
    }

    [JsonIgnore]
    public string EtiquetaEstado => Estado switch
    {
        EstadoPreguntaEnum.Respondida => "Respondida",
        EstadoPreguntaEnum.Salteada => "Salteada",
        _ => "Sin responder"
    };

    [JsonIgnore]
    public string TituloResultado => Resultado switch
    {
        ResultadoPreguntaEnum.Correcta => "Correcta",
        ResultadoPreguntaEnum.Incorrecta => "Incorrecta",
        ResultadoPreguntaEnum.Salteada => "Salteada / Pendiente",
        _ => "Sin corregir"
    };

    /// <summary>
    /// Revela la respuesta y el analisis aunque la pregunta este fallada.
    ///
    /// Al corregir en el momento se oculta a proposito: si el error mostrara la respuesta, el
    /// Modo Revancha no serviria para nada. Al revisar el detalle de un examen viejo desde el
    /// Historial (US-025) la situacion es la contraria — el alumno entra justamente a repasar
    /// lo que le salio mal, y esconderselo dejaria la pantalla sin motivo para existir.
    ///
    /// No se persiste: es como se esta mirando la pregunta, no un dato del intento. Y las
    /// preguntas del historial son una copia propia (ver <see cref="ClonarParaHistorial"/>),
    /// asi que encenderlo aca nunca destapa las respuestas de una revancha en curso.
    /// </summary>
    [JsonIgnore]
    public bool RevelarAnalisis
    {
        get => _revelarAnalisis;
        set
        {
            if (Set(ref _revelarAnalisis, value))
            {
                OnPropertyChanged(nameof(MuestraRespuestaCorrecta));
                OnPropertyChanged(nameof(MuestraAnalisisCompleto));
            }
        }
    }

    private bool _revelarAnalisis;

    /// <summary>Solo las correctas revelan la respuesta y el analisis completo.</summary>
    [JsonIgnore]
    public bool MuestraRespuestaCorrecta => RevelarAnalisis || Resultado == ResultadoPreguntaEnum.Correcta;

    [JsonIgnore]
    public bool MuestraAnalisisCompleto => RevelarAnalisis || Resultado == ResultadoPreguntaEnum.Correcta;

    /// <summary>Incorrectas y salteadas: entran al Modo Revancha.</summary>
    [JsonIgnore]
    public bool EsPendiente => Resultado is ResultadoPreguntaEnum.Incorrecta or ResultadoPreguntaEnum.Salteada;

    /// <summary>
    /// Desglose opcion por opcion para la vista de correccion (solo se muestra en las correctas).
    /// </summary>
    [JsonIgnore]
    public List<LineaAnalisis> LineasAnalisis
    {
        get
        {
            var lineas = new List<LineaAnalisis>();
            for (int i = 0; i < Opciones.Count; i++)
            {
                bool esCorrecta = i == IndiceRespuestaCorrecta;
                string detalle = i < AnalisisOpciones.AnalisisPorOpcion.Count
                    ? AnalisisOpciones.AnalisisPorOpcion[i]
                    : string.Empty;

                lineas.Add(new LineaAnalisis
                {
                    Encabezado = $"{Letra(i)}. {Opciones[i]}",
                    Detalle = detalle,
                    EsCorrecta = esCorrecta,
                    EsElegidaPorUsuario = IndiceRespuestaUsuario == i
                });
            }

            return lineas;
        }
    }

    public static string Letra(int indice) => indice switch
    {
        0 => "A",
        1 => "B",
        2 => "C",
        3 => "D",
        _ => ((char)('A' + indice)).ToString()
    };

    /// <summary>Reinicia la pregunta para una nueva ronda de revancha.</summary>
    public void ReiniciarParaRevancha()
    {
        IndiceRespuestaUsuario = null;
        Estado = EstadoPreguntaEnum.SinResponder;
        Resultado = ResultadoPreguntaEnum.Pendiente;
    }

    /// <summary>
    /// Remezcla A/B/C/D manteniendo sincronizados el indice correcto y el analisis por opcion.
    /// </summary>
    public void MezclarOpciones(Random rnd)
    {
        int n = Opciones.Count;
        if (n < 2)
        {
            return;
        }

        var analisis = AnalisisOpciones.AnalisisPorOpcion;
        bool analisisAlineado = analisis.Count == n;

        var indices = Enumerable.Range(0, n).ToArray();
        for (int i = n - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        var nuevasOpciones = new List<string>(n);
        var nuevoAnalisis = new List<string>(n);
        int nuevoCorrecto = 0;

        for (int destino = 0; destino < n; destino++)
        {
            int origen = indices[destino];
            nuevasOpciones.Add(Opciones[origen]);
            nuevoAnalisis.Add(analisisAlineado ? analisis[origen] : string.Empty);

            if (origen == IndiceRespuestaCorrecta)
            {
                nuevoCorrecto = destino;
            }
        }

        Opciones = nuevasOpciones;
        IndiceRespuestaCorrecta = nuevoCorrecto;
        if (analisisAlineado)
        {
            AnalisisOpciones.AnalisisPorOpcion = nuevoAnalisis;
        }
    }

    /// <summary>
    /// Copia para guardar en el historial (RN-25): igual que <see cref="Clonar"/> pero
    /// conservando lo que el alumno contesto y como salio corregida.
    ///
    /// <see cref="Clonar"/> deja esos tres campos afuera a proposito, porque su unico uso es
    /// armar una revancha, donde la pregunta tiene que volver a empezar en blanco. El detalle
    /// del historial necesita exactamente lo contrario.
    ///
    /// Es una copia y no la instancia viva por una razon concreta: el intento sigue en
    /// pantalla despues de corregirse, y encadenar revanchas mueve estado sobre esos objetos.
    /// El registro del historial tiene que ser la foto del intento original, no un puntero a
    /// algo que puede seguir cambiando.
    /// </summary>
    public Pregunta ClonarParaHistorial()
    {
        var copia = Clonar();

        copia.IndiceRespuestaUsuario = IndiceRespuestaUsuario;
        copia.Estado = Estado;
        copia.Resultado = Resultado;

        return copia;
    }

    /// <summary>Copia profunda liviana, usada para no pisar el intento original en la revancha.</summary>
    public Pregunta Clonar()
    {
        return new Pregunta
        {
            Id = Id,
            TextoPregunta = TextoPregunta,
            RutaImagenAdjunta = RutaImagenAdjunta,
            Opciones = new List<string>(Opciones),
            IndiceRespuestaCorrecta = IndiceRespuestaCorrecta,
            JustificacionBibliografia = JustificacionBibliografia,
            PaginaOrigen = PaginaOrigen,
            PaginasAlcance = PaginasAlcance,
            ModuloOrigen = ModuloOrigen,
            DocumentoOrigen = DocumentoOrigen,
            ExamenOrigen = ExamenOrigen,
            AnalisisOpciones = new AnalisisOpciones
            {
                ExplicacionCorrecta = AnalisisOpciones.ExplicacionCorrecta,
                AnalisisPorOpcion = new List<string>(AnalisisOpciones.AnalisisPorOpcion)
            }
        };
    }
}
