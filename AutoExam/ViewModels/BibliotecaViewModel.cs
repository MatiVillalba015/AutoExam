using System.Collections.ObjectModel;
using System.IO;
using AutoExam.Models;
using AutoExam.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoExam.ViewModels;

/// <summary>Alta de libros y definicion de sus modulos.</summary>
public partial class BibliotecaViewModel : PaginaViewModel
{
    private readonly BibliotecaService _biblioteca;
    private readonly PdfExtractorService _pdf;
    private readonly IDialogos _dialogos;
    private readonly INavegacion _nav;

    public BibliotecaViewModel(
        BibliotecaService biblioteca, PdfExtractorService pdf, IDialogos dialogos, INavegacion nav)
        : base("libros", "Libros", "Library24")
    {
        _biblioteca = biblioteca;
        _pdf = pdf;
        _dialogos = dialogos;
        _nav = nav;

        Modulos.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ResumenModulos));
            OnPropertyChanged(nameof(HayModulos));
        };

        _biblioteca.Libros.CollectionChanged += (_, _) => Insignia = TextoInsignia();
        Insignia = TextoInsignia();
    }

    public ObservableCollection<Libro> Libros => _biblioteca.Libros;

    /// <summary>Modulos del libro abierto. Se vuelcan al modelo al guardar.</summary>
    public ObservableCollection<Modulo> Modulos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayLibroAbierto))]
    [NotifyPropertyChangedFor(nameof(DetalleArchivo))]
    [NotifyPropertyChangedFor(nameof(FaltaElPdf))]
    private Libro? _libroSeleccionado;

    // Se llama TituloLibro y no Titulo porque PaginaViewModel ya usa Titulo
    // para el nombre de la pagina en la navegacion lateral.
    [ObservableProperty]
    private string _tituloLibro = string.Empty;

    [ObservableProperty]
    private string _materia = string.Empty;

    [ObservableProperty]
    private int _partesParaDividir = 10;

    [ObservableProperty]
    private bool _ocupado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMensaje))]
    private string _mensaje = string.Empty;

    /// <summary>0 = info · 1 = ok · 2 = aviso · 3 = error. Lo traduce SeveridadConverter.</summary>
    [ObservableProperty]
    private int _severidad = 1;

    public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

    public bool HayLibroAbierto => LibroSeleccionado is not null;

    public bool HayModulos => Modulos.Count > 0;

    public bool FaltaElPdf => LibroSeleccionado is { ArchivoDisponible: false };

    public string DetalleArchivo => LibroSeleccionado is null
        ? string.Empty
        : $"{LibroSeleccionado.CantidadPaginas} paginas · {LibroSeleccionado.NombreArchivoOriginal}";

    public string ResumenModulos => Modulos.Count == 0
        ? "Sin dividir: los examenes van a tomar el libro entero"
        : $"{Modulos.Count} modulos · {Modulos.Sum(m => m.CantidadPaginas)} paginas cubiertas";

    /// <summary>Materias ya usadas, para ofrecerlas como chips y no volver a tipearlas.</summary>
    public IEnumerable<string> MateriasConocidas => _biblioteca.Libros
        .Select(l => l.Materia)
        .Where(m => !string.IsNullOrWhiteSpace(m) && m != "Sin materia")
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(m => m)
        .Take(8);

    partial void OnLibroSeleccionadoChanged(Libro? oldValue, Libro? newValue)
    {
        if (oldValue is not null)
        {
            VolcarAlModelo(oldValue);
        }

        Modulos.Clear();
        Mensaje = string.Empty;

        if (newValue is null)
        {
            TituloLibro = string.Empty;
            Materia = string.Empty;
            return;
        }

        TituloLibro = newValue.Titulo;
        Materia = newValue.Materia;

        foreach (var m in newValue.Modulos.OrderBy(m => m.DesdePagina))
        {
            Modulos.Add(m);
        }

        PartesParaDividir = Math.Clamp(PartesParaDividir, 1, Math.Max(1, newValue.CantidadPaginas));

        if (FaltaElPdf)
        {
            Avisar("No se encuentra la copia del PDF. Volve a agregar el libro.", error: true);
        }
    }

    // ------------------------------------------------------------------
    // Alta de libros
    // ------------------------------------------------------------------
    [RelayCommand]
    private async Task ElegirArchivoAsync()
    {
        string? ruta = _dialogos.ElegirPdf();
        if (ruta is not null)
        {
            await AgregarAsync(ruta);
        }
    }

    /// <summary>Recibe la ruta que suelta el usuario sobre la zona de arrastre.</summary>
    [RelayCommand]
    private async Task SoltarAsync(string? ruta)
    {
        if (!string.IsNullOrWhiteSpace(ruta))
        {
            await AgregarAsync(ruta);
        }
    }

    private async Task AgregarAsync(string ruta)
    {
        if (!File.Exists(ruta))
        {
            Avisar("Ese archivo ya no esta donde estaba.", error: true);
            return;
        }

        Ocupado = true;
        _nav.Estado("Copiando y analizando el PDF...");

        try
        {
            // El titulo sale del nombre del archivo: casi siempre alcanza y evita
            // que el alta arranque con un formulario vacio.
            string sugerido = Path.GetFileNameWithoutExtension(ruta);
            string materia = MateriasConocidas.FirstOrDefault() ?? "Sin materia";

            var libro = await _biblioteca.AgregarLibroAsync(ruta, sugerido, materia);

            LibroSeleccionado = libro;
            OnPropertyChanged(nameof(MateriasConocidas));

            _nav.Estado($"Libro agregado: {libro.Titulo} ({libro.CantidadPaginas} paginas).");

            // Los capitulos se traen solos del indice del PDF: sin esto el usuario tendria
            // que cargar veinte rangos a mano antes de poder pedir "capitulos 1, 2 y 5".
            int capitulos = await PoblarCapitulosAsync(libro);

            Avisar(capitulos > 0
                ? $"Listo: {libro.CantidadPaginas} paginas y {capitulos} capitulos leidos del indice del PDF. " +
                  "Revisa el titulo y la materia, y guarda."
                : $"Listo: {libro.CantidadPaginas} paginas. Este PDF no trae indice, asi que no hay capitulos: " +
                  "podes dividirlo en partes iguales o cargarlos a mano.");
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("AgregarLibro", ex);
            Avisar(ex.Message, error: true);
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private void Quitar()
    {
        if (LibroSeleccionado is not Libro libro)
        {
            return;
        }

        bool si = _dialogos.Confirmar(
            $"¿Quitar \"{libro.Titulo}\" de la biblioteca?\n\n" +
            "Se borra la copia interna del PDF. El archivo original de tu PC no se toca.");

        if (!si)
        {
            return;
        }

        _biblioteca.EliminarLibro(libro);
        LibroSeleccionado = _biblioteca.Libros.FirstOrDefault();
        _nav.Estado("Libro eliminado.");
    }

    // ------------------------------------------------------------------
    // Modulos
    // ------------------------------------------------------------------
    [RelayCommand]
    private void AgregarModulo()
    {
        if (LibroSeleccionado is not Libro libro)
        {
            return;
        }

        int desde = Modulos.Count == 0
            ? 1
            : Math.Min(libro.CantidadPaginas, Modulos.Max(m => m.HastaPagina) + 1);

        Modulos.Add(new Modulo
        {
            Nombre = $"Modulo {Modulos.Count + 1}",
            DesdePagina = desde,
            HastaPagina = Math.Min(libro.CantidadPaginas, desde + 19)
        });
    }

    [RelayCommand]
    private void QuitarModulo(Modulo? modulo)
    {
        if (modulo is not null)
        {
            Modulos.Remove(modulo);
        }
        else if (Modulos.Count > 0)
        {
            Modulos.RemoveAt(Modulos.Count - 1);
        }
    }

    /// <summary>
    /// Trae los capitulos del indice interno del PDF. Es lo que evita cargar a mano
    /// veinte rangos de paginas para despues poder pedir "capitulos 1, 2, 5 y 7".
    /// </summary>
    [RelayCommand]
    private async Task DetectarCapitulosAsync()
    {
        if (LibroSeleccionado is not Libro libro)
        {
            return;
        }

        if (!libro.ArchivoDisponible)
        {
            Avisar("No se encuentra la copia del PDF. Volve a agregarlo.", error: true);
            return;
        }

        Ocupado = true;
        _nav.Estado("Leyendo el indice del PDF...");

        try
        {
            var capitulos = await _pdf.DetectarCapitulosAsync(libro.RutaArchivo);

            if (capitulos.Count == 0)
            {
                Avisar(
                    "Este PDF no trae indice interno (marcadores), asi que no hay capitulos que leer. " +
                    "Es lo habitual en los escaneados. Podes dividirlo en partes iguales aca abajo, " +
                    "o cargar los capitulos a mano.");
                return;
            }

            if (Modulos.Count > 0 &&
                !_dialogos.Confirmar(
                    $"Se encontraron {capitulos.Count} capitulos en el indice del PDF.\n\n" +
                    $"Reemplazan a los {Modulos.Count} modulos actuales. ¿Seguir?"))
            {
                return;
            }

            Volcar(capitulos);

            _nav.Estado($"Se detectaron {capitulos.Count} capitulos.");
            Avisar($"Listos {capitulos.Count} capitulos. Guarda para poder elegirlos al armar el examen.");
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("DetectarCapitulos", ex);
            Avisar($"No se pudo leer el indice del PDF: {ex.Message}", error: true);
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>Detecta y vuelca sin preguntar nada. Devuelve cuantos capitulos encontro.</summary>
    private async Task<int> PoblarCapitulosAsync(Libro libro)
    {
        try
        {
            var capitulos = await _pdf.DetectarCapitulosAsync(libro.RutaArchivo);
            if (capitulos.Count == 0)
            {
                return 0;
            }

            Volcar(capitulos);
            libro.Modulos = Modulos.Select(m => m.Clonar()).ToList();
            _biblioteca.Guardar();

            return capitulos.Count;
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError("PoblarCapitulos", ex);
            return 0;
        }
    }

    private void Volcar(IEnumerable<CapituloDetectado> capitulos)
    {
        Modulos.Clear();
        foreach (var c in capitulos)
        {
            Modulos.Add(new Modulo
            {
                Nombre = c.Titulo,
                DesdePagina = c.Desde,
                HastaPagina = c.Hasta
            });
        }
    }

    [RelayCommand]
    private void Dividir()
    {
        if (LibroSeleccionado is not Libro libro)
        {
            return;
        }

        if (Modulos.Count > 0 &&
            !_dialogos.Confirmar($"Se reemplazan los {Modulos.Count} modulos actuales por {PartesParaDividir} partes iguales. ¿Seguir?"))
        {
            return;
        }

        Modulos.Clear();
        foreach (var m in BibliotecaService.GenerarModulosAutomaticos(libro.CantidadPaginas, PartesParaDividir))
        {
            Modulos.Add(m);
        }

        _nav.Estado($"Se generaron {Modulos.Count} modulos.");
    }

    [RelayCommand]
    private void UsarMateria(string? materia)
    {
        if (!string.IsNullOrWhiteSpace(materia))
        {
            Materia = materia;
        }
    }

    [RelayCommand]
    private void Guardar()
    {
        if (LibroSeleccionado is not Libro libro)
        {
            return;
        }

        VolcarAlModelo(libro);
        _biblioteca.Guardar();
        OnPropertyChanged(nameof(MateriasConocidas));

        Avisar($"Guardado. {libro.Modulos.Count} modulos definidos.");
        _nav.Estado("Biblioteca guardada.");
    }

    /// <summary>Pasa lo editado en la vista al modelo, saneando los rangos.</summary>
    private void VolcarAlModelo(Libro libro)
    {
        if (!string.IsNullOrWhiteSpace(TituloLibro))
        {
            libro.Titulo = TituloLibro.Trim();
        }

        if (!string.IsNullOrWhiteSpace(Materia))
        {
            libro.Materia = Materia.Trim();
        }

        int tope = Math.Max(1, libro.CantidadPaginas);

        foreach (var m in Modulos)
        {
            m.DesdePagina = Math.Clamp(m.DesdePagina, 1, tope);
            m.HastaPagina = Math.Clamp(m.HastaPagina, m.DesdePagina, tope);
        }

        libro.Modulos = Modulos.OrderBy(m => m.DesdePagina).ToList();
        libro.NotificarCambioResumen();
    }

    /// <summary>Guarda lo que este a medio editar. Lo llama el shell al cerrar.</summary>
    public void GuardarPendiente()
    {
        if (LibroSeleccionado is Libro libro)
        {
            VolcarAlModelo(libro);
        }
    }

    private void Avisar(string texto, bool error = false)
    {
        Mensaje = texto;
        Severidad = error ? 3 : 1;
    }

    private string TextoInsignia() => _biblioteca.Libros.Count switch
    {
        0 => string.Empty,
        1 => "1 libro",
        var n => $"{n} libros"
    };
}
