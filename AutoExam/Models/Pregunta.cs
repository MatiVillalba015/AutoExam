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
            if (PaginaOrigen > 0)
            {
                return $"Pagina {PaginaOrigen} del PDF";
            }

            if (string.IsNullOrWhiteSpace(PaginasAlcance))
            {
                return string.Empty;
            }

            string tramo = char.ToUpperInvariant(PaginasAlcance[0]) + PaginasAlcance[1..];
            return $"{tramo} del PDF (el modelo no preciso una pagina exacta)";
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

    /// <summary>Solo las correctas revelan la respuesta y el analisis completo.</summary>
    [JsonIgnore]
    public bool MuestraRespuestaCorrecta => Resultado == ResultadoPreguntaEnum.Correcta;

    [JsonIgnore]
    public bool MuestraAnalisisCompleto => Resultado == ResultadoPreguntaEnum.Correcta;

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
            AnalisisOpciones = new AnalisisOpciones
            {
                ExplicacionCorrecta = AnalisisOpciones.ExplicacionCorrecta,
                AnalisisPorOpcion = new List<string>(AnalisisOpciones.AnalisisPorOpcion)
            }
        };
    }
}
