using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Threading;
using AutoExam.Models;
using AutoExam.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoExam.ViewModels;

/// <summary>Una opcion de la pregunta actual, ya con su letra.</summary>
public partial class OpcionViewModel : ObservableObject
{
    public OpcionViewModel(int indice, string texto, bool elegida)
    {
        Indice = indice;
        Texto = texto;
        _elegida = elegida;
    }

    public int Indice { get; }

    public string Texto { get; }

    public string Letra => Pregunta.Letra(Indice);

    [ObservableProperty]
    private bool _elegida;
}

public enum VistaExamen
{
    SinExamen,
    Rindiendo,
    Resultados
}

/// <summary>Rendir el examen, corregirlo y encadenar las rondas de revancha.</summary>
public partial class ExamenViewModel : PaginaViewModel
{
    private readonly SesionUsuarioService _sesion;
    private readonly IDialogos _dialogos;
    private readonly INavegacion _nav;
    private readonly Random _random = new();

    private readonly DispatcherTimer _cronometro = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _avance = new() { Interval = TimeSpan.FromMilliseconds(280) };

    /// <summary>
    /// Mapeo nivel (0..4) -&gt; puntos, para pregunta y opciones (US-005). El nivel 2
    /// (indice 2, "Normal") reproduce el tamanio de siempre: 17pt/14pt.
    /// </summary>
    private static readonly double[] PuntosPregunta = { 13, 15, 17, 20, 23 };

    private static readonly double[] PuntosOpciones = { 11, 12, 14, 16, 18 };

    public const int NivelTextoMinimo = 0;
    public const int NivelTextoMaximo = 4;

    public ExamenViewModel(SesionUsuarioService sesion, IDialogos dialogos, INavegacion nav)
        : base("examen", "Examen", "ClipboardTaskListLtr24")
    {
        _sesion = sesion;
        _dialogos = dialogos;
        _nav = nav;

        _cronometro.Tick += (_, _) => OnPropertyChanged(nameof(Cronometro));
        _avance.Tick += (_, _) => AvanzarSolo();
    }

    public ObservableCollection<NavegadorItem> Navegador { get; } = new();

    public ObservableCollection<OpcionViewModel> Opciones { get; } = new();

    public ObservableCollection<Pregunta> Correccion { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinExamen))]
    [NotifyPropertyChangedFor(nameof(Rindiendo))]
    [NotifyPropertyChangedFor(nameof(EnResultados))]
    private VistaExamen _vista = VistaExamen.SinExamen;

    public bool SinExamen => Vista == VistaExamen.SinExamen;
    public bool Rindiendo => Vista == VistaExamen.Rindiendo;
    public bool EnResultados => Vista == VistaExamen.Resultados;

    [ObservableProperty]
    private ExamenEnCurso? _examen;

    [ObservableProperty]
    private Pregunta? _actual;

    [ObservableProperty]
    private string _encabezado = "Examen";

    [ObservableProperty]
    private string _subtitulo = string.Empty;

    [ObservableProperty]
    private string _progresoTexto = string.Empty;

    [ObservableProperty]
    private string _contadores = string.Empty;

    [ObservableProperty]
    private string _pieImagen = string.Empty;

    [ObservableProperty]
    private bool _avanceAutomatico = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnteriorCommand))]
    private bool _puedeAnterior;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    private bool _puedeSiguiente;

    [ObservableProperty]
    private string _textoFinalizar = "Finalizar examen";

    public string Cronometro => Examen?.Transcurrido.ToString(@"hh\:mm\:ss") ?? "00:00:00";

    // ------------------------------------------------------------------
    // Tamanio de texto (US-005)
    // ------------------------------------------------------------------

    /// <summary>Nivel actual, 0..4. Se persiste en AppConfig.TamanioTextoExamen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TamanioTextoPregunta))]
    [NotifyPropertyChangedFor(nameof(TamanioTextoOpciones))]
    [NotifyCanExecuteChangedFor(nameof(AumentarTextoExamenCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisminuirTextoExamenCommand))]
    private int _nivelTextoExamen = 2;

    public double TamanioTextoPregunta => PuntosPregunta[NivelTextoExamen];

    public double TamanioTextoOpciones => PuntosOpciones[NivelTextoExamen];

    partial void OnNivelTextoExamenChanged(int value)
    {
        _sesion.Config.TamanioTextoExamen = value;
        _sesion.GuardarConfig();
    }

    [RelayCommand(CanExecute = nameof(PuedeAumentarTextoExamen))]
    private void AumentarTextoExamen() => NivelTextoExamen = Math.Min(NivelTextoMaximo, NivelTextoExamen + 1);

    private bool PuedeAumentarTextoExamen() => NivelTextoExamen < NivelTextoMaximo;

    [RelayCommand(CanExecute = nameof(PuedeDisminuirTextoExamen))]
    private void DisminuirTextoExamen() => NivelTextoExamen = Math.Max(NivelTextoMinimo, NivelTextoExamen - 1);

    private bool PuedeDisminuirTextoExamen() => NivelTextoExamen > NivelTextoMinimo;

    // ---------- Resultados ----------
    [ObservableProperty]
    private int _nota;

    [ObservableProperty]
    private bool _aprobado;

    [ObservableProperty]
    private string _condicion = string.Empty;

    [ObservableProperty]
    private string _resumenResultado = string.Empty;

    [ObservableProperty]
    private string _detalleRondas = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayPendientes))]
    [NotifyPropertyChangedFor(nameof(TextoRevancha))]
    private int _pendientes;

    public bool HayPendientes => Pendientes > 0;

    public string TextoRevancha => $"Reintentar {Pendientes} pendientes";

    // ------------------------------------------------------------------
    // Arranque
    // ------------------------------------------------------------------
    public void Iniciar(ExamenEnCurso examen)
    {
        Examen = examen;
        examen.IndiceActual = 0;

        Encabezado = examen.TituloRonda;
        Subtitulo = examen.EsRevancha
            ? $"{examen.Preguntas.Count} preguntas pendientes · opciones reordenadas al azar"
            : $"{examen.LibroTitulo} · {examen.AlcanceDescripcion}";

        TextoFinalizar = examen.EsRevancha ? "Corregir revancha" : "Finalizar examen";
        Insignia = $"{examen.Preguntas.Count} preguntas";

        ReconstruirNavegador();
        MostrarActual();

        Vista = VistaExamen.Rindiendo;
        _cronometro.Start();

        _nav.IrA(Clave);
        _nav.Estado($"Examen listo: {examen.Preguntas.Count} preguntas.");
    }

    public bool HayIntentoAbierto => Examen is not null && Vista == VistaExamen.Rindiendo;

    /// <summary>
    /// Trae el nivel de tamanio de texto guardado. Se llama despues de <c>SesionUsuarioService.Cargar()</c>
    /// (ver ShellViewModel.IniciarAsync), igual que AjustesViewModel.CargarDesdeConfig: en el
    /// constructor todavia no existe config.json leido.
    /// </summary>
    public void CargarDesdeConfig()
    {
        NivelTextoExamen = Math.Clamp(_sesion.Config.TamanioTextoExamen, NivelTextoMinimo, NivelTextoMaximo);
    }

    private void ReconstruirNavegador()
    {
        Navegador.Clear();

        if (Examen is null)
        {
            return;
        }

        for (int i = 0; i < Examen.Preguntas.Count; i++)
        {
            Navegador.Add(new NavegadorItem
            {
                Numero = i + 1,
                Indice = i,
                Pregunta = Examen.Preguntas[i],
                EsActual = i == Examen.IndiceActual
            });
        }
    }

    private void MostrarActual()
    {
        if (Examen?.Actual is not Pregunta p)
        {
            return;
        }

        Actual = p;
        ProgresoTexto = $"{Examen.TextoProgreso}   ·   {p.EtiquetaEstado}";

        PieImagen = p.PaginaOrigen > 0
            ? $"Figura extraida de la pagina {p.PaginaOrigen} del PDF."
            : "Figura extraida del PDF.";

        Opciones.Clear();
        for (int i = 0; i < p.Opciones.Count; i++)
        {
            Opciones.Add(new OpcionViewModel(i, p.Opciones[i], p.IndiceRespuestaUsuario == i));
        }

        PuedeAnterior = Examen.PuedeAnterior;
        PuedeSiguiente = Examen.PuedeSiguiente;

        RefrescarNavegador();
        ActualizarContadores();
    }

    private void RefrescarNavegador()
    {
        if (Examen is null)
        {
            return;
        }

        foreach (var item in Navegador)
        {
            item.EsActual = item.Indice == Examen.IndiceActual;
            item.Refrescar();
        }
    }

    private void ActualizarContadores()
    {
        if (Examen is null)
        {
            return;
        }

        Examen.NotificarContadores();
        Contadores = $"{Examen.Respondidas} respondidas · {Examen.Salteadas} salteadas · {Examen.SinResponder} sin ver";
    }

    // ------------------------------------------------------------------
    // Responder y navegar
    // ------------------------------------------------------------------
    [RelayCommand]
    private void Responder(object? parametro)
    {
        if (Examen?.Actual is not Pregunta p || !Rindiendo)
        {
            return;
        }

        int indice = parametro switch
        {
            OpcionViewModel o => o.Indice,
            int i => i,
            string s when int.TryParse(s, out int n) => n,
            _ => -1
        };

        if (indice < 0 || indice >= p.Opciones.Count)
        {
            return;
        }

        p.IndiceRespuestaUsuario = indice;
        p.Estado = EstadoPreguntaEnum.Respondida;

        foreach (var o in Opciones)
        {
            o.Elegida = o.Indice == indice;
        }

        ProgresoTexto = $"{Examen.TextoProgreso}   ·   {p.EtiquetaEstado}";
        RefrescarNavegador();
        ActualizarContadores();

        // Un respiro antes de avanzar: sin el, la opcion marcada nunca se ve.
        if (AvanceAutomatico && Examen.PuedeSiguiente)
        {
            _avance.Stop();
            _avance.Start();
        }
    }

    private void AvanzarSolo()
    {
        _avance.Stop();

        if (Examen is null || !Rindiendo || !Examen.PuedeSiguiente)
        {
            return;
        }

        Examen.IndiceActual++;
        MostrarActual();
    }

    [RelayCommand(CanExecute = nameof(PuedeAnterior))]
    private void Anterior()
    {
        if (Examen is null)
        {
            return;
        }

        _avance.Stop();
        Examen.IndiceActual--;
        MostrarActual();
    }

    [RelayCommand(CanExecute = nameof(PuedeSiguiente))]
    private void Siguiente()
    {
        if (Examen is null)
        {
            return;
        }

        _avance.Stop();
        Examen.IndiceActual++;
        MostrarActual();
    }

    [RelayCommand]
    private void IrAPregunta(NavegadorItem? item)
    {
        if (Examen is null || item is null)
        {
            return;
        }

        _avance.Stop();
        Examen.IndiceActual = item.Indice;
        MostrarActual();
    }

    /// <summary>La pregunta pasa sin responderse y vuelve en el Modo Revancha.</summary>
    [RelayCommand]
    private void Saltear()
    {
        if (Examen?.Actual is not Pregunta p || !Rindiendo)
        {
            return;
        }

        p.IndiceRespuestaUsuario = null;
        p.Estado = EstadoPreguntaEnum.Salteada;

        _nav.Estado($"Pregunta {Examen.IndiceActual + 1} salteada.");

        if (Examen.PuedeSiguiente)
        {
            Examen.IndiceActual++;
        }

        MostrarActual();
    }

    // ------------------------------------------------------------------
    // Correccion
    // ------------------------------------------------------------------
    [RelayCommand]
    private void Finalizar()
    {
        if (Examen is not ExamenEnCurso examen || examen.Preguntas.Count == 0)
        {
            return;
        }

        int sinVer = examen.SinResponder;
        if (sinVer > 0 &&
            !_dialogos.Confirmar(
                $"Quedan {sinVer} preguntas sin responder. Se cuentan como SALTEADAS (restan igual que un error).\n\n¿Finalizar igual?"))
        {
            examen.IrAPrimeraSinResponder();
            MostrarActual();
            return;
        }

        _cronometro.Stop();
        _avance.Stop();

        var resultado = EvaluadorUBA.Evaluar(examen.Preguntas);
        int duracion = (int)examen.Transcurrido.TotalSeconds;

        if (examen.Ronda == 0)
        {
            var registro = new ExamenRendido
            {
                Id = examen.Id,
                Fecha = examen.Inicio,
                LibroId = examen.LibroId,
                LibroTitulo = examen.LibroTitulo,
                Materia = examen.Materia,
                AlcanceDescripcion = examen.AlcanceDescripcion,
                TotalPreguntas = resultado.Total,
                Correctas = resultado.Correctas,
                Incorrectas = resultado.Incorrectas,
                Salteadas = resultado.Salteadas,
                PorcentajeAciertos = Math.Round(resultado.Porcentaje, 2),
                NotaUBA = resultado.Nota,
                Condicion = resultado.Condicion,
                Aprobado = resultado.Aprobado,
                DuracionSegundos = duracion,
                CompletadoAl100 = resultado.Pendientes == 0
            };

            examen.Registro = registro;
            _sesion.RegistrarExamen(registro);
        }
        else if (examen.Registro is ExamenRendido registro)
        {
            // Las revanchas no tocan la nota original: quedan anotadas como rondas.
            registro.Revanchas.Add(new RondaRevancha
            {
                Numero = examen.Ronda,
                TotalPreguntas = resultado.Total,
                Correctas = resultado.Correctas
            });

            registro.DuracionSegundos += duracion;
            registro.CompletadoAl100 = resultado.Pendientes == 0;
            _sesion.ActualizarExamen(registro);
        }

        MostrarResultados(resultado);
        HistorialCambio?.Invoke();
    }

    /// <summary>Lo escucha el shell para refrescar las estadisticas de la pagina Historial.</summary>
    public event Action? HistorialCambio;

    private void MostrarResultados(ResultadoExamen resultado)
    {
        if (Examen is not ExamenEnCurso examen)
        {
            return;
        }

        Nota = resultado.Nota;
        Aprobado = resultado.Aprobado;
        Pendientes = resultado.Pendientes;

        Condicion = examen.EsRevancha
            ? $"Revancha ronda {examen.Ronda}: {resultado.Correctas}/{resultado.Total} correctas"
            : resultado.Condicion;

        ResumenResultado = examen.EsRevancha
            ? $"{resultado.Resumen}. La nota del intento original no se modifica."
            : $"{resultado.Resumen}. Escala UBA: se aprueba con 60% (nota 4).";

        var detalle = new StringBuilder();
        detalle.Append($"{examen.LibroTitulo} · {examen.AlcanceDescripcion} · duracion {examen.Transcurrido:hh\\:mm\\:ss}");

        if (examen.Registro is { Revanchas.Count: > 0 } reg)
        {
            detalle.Append("   |   ");
            detalle.Append(string.Join(" · ", reg.Revanchas.Select(r => r.Descripcion)));
        }

        DetalleRondas = detalle.ToString();

        Correccion.Clear();
        foreach (var p in examen.Preguntas)
        {
            Correccion.Add(p);
        }

        Vista = VistaExamen.Resultados;

        if (Pendientes == 0)
        {
            _nav.Estado("100% de aciertos: no quedan preguntas pendientes.");
            _dialogos.Aviso("Llegaste al 100% de aciertos",
                "No quedan preguntas incorrectas ni salteadas en esta ronda.");
        }
        else
        {
            _nav.Estado($"Examen corregido. Quedan {Pendientes} preguntas para el Modo Revancha.");
        }
    }

    /// <summary>Mini-examen con las falladas y las salteadas, con las opciones remezcladas.</summary>
    [RelayCommand]
    private void Revancha()
    {
        if (Examen is not ExamenEnCurso examen)
        {
            return;
        }

        var pendientes = examen.Preguntas.Where(p => p.EsPendiente).ToList();
        if (pendientes.Count == 0)
        {
            return;
        }

        var revancha = new ExamenEnCurso
        {
            Id = examen.Id,
            LibroId = examen.LibroId,
            LibroTitulo = examen.LibroTitulo,
            Materia = examen.Materia,
            AlcanceDescripcion = examen.AlcanceDescripcion,
            Inicio = DateTime.Now,
            Ronda = examen.Ronda + 1,
            Registro = examen.Registro
        };

        foreach (var original in pendientes.OrderBy(_ => _random.Next()))
        {
            var copia = original.Clonar();
            copia.MezclarOpciones(_random);
            copia.ReiniciarParaRevancha();
            revancha.Preguntas.Add(copia);
        }

        Iniciar(revancha);
        _nav.Estado($"Modo Revancha ronda {revancha.Ronda}: {revancha.Preguntas.Count} preguntas reordenadas.");
    }

    /// <summary>Salir en cualquier momento. El intento se descarta sin registrarse.</summary>
    [RelayCommand]
    private void Salir()
    {
        if (Examen is not ExamenEnCurso examen)
        {
            return;
        }

        int encaradas = examen.Respondidas + examen.Salteadas;

        bool si = _dialogos.Confirmar(
            $"Vas a salir con {encaradas} de {examen.Preguntas.Count} preguntas encaradas.\n\n" +
            "El intento se descarta y no queda registrado. ¿Salir?");

        if (!si)
        {
            return;
        }

        Cerrar();
        _nav.IrA("nuevo");
        _nav.Estado("Saliste del examen. El intento se descarto.");
    }

    [RelayCommand]
    private void ArmarOtro()
    {
        Cerrar();
        _nav.IrA("nuevo");
    }

    public void Cerrar()
    {
        _cronometro.Stop();
        _avance.Stop();

        Examen = null;
        Actual = null;
        Navegador.Clear();
        Opciones.Clear();
        Correccion.Clear();
        Insignia = string.Empty;

        Vista = VistaExamen.SinExamen;
    }
}
