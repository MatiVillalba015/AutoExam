using System.IO;
using System.Text.Json.Serialization;

namespace AutoExam.Models;

/// <summary>Modulo / Capitulo / Unidad de un libro, con su rango de paginas.</summary>
public class Modulo : ObservableBase
{
    private string _nombre = string.Empty;
    private int _desdePagina = 1;
    private int _hastaPagina = 1;
    private bool _seleccionado;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Nombre
    {
        get => _nombre;
        set => Set(ref _nombre, value);
    }

    public int DesdePagina
    {
        get => _desdePagina;
        set
        {
            if (Set(ref _desdePagina, value))
            {
                OnPropertyChanged(nameof(Descripcion));
            }
        }
    }

    public int HastaPagina
    {
        get => _hastaPagina;
        set
        {
            if (Set(ref _hastaPagina, value))
            {
                OnPropertyChanged(nameof(Descripcion));
            }
        }
    }

    /// <summary>Marcado en la vista "Configurar Examen". No se persiste.</summary>
    [JsonIgnore]
    public bool Seleccionado
    {
        get => _seleccionado;
        set => Set(ref _seleccionado, value);
    }

    [JsonIgnore]
    public int CantidadPaginas => Math.Max(0, HastaPagina - DesdePagina + 1);

    [JsonIgnore]
    public string Descripcion => $"pag. {DesdePagina} a {HastaPagina}  ({CantidadPaginas} pags.)";

    public Modulo Clonar() => new()
    {
        Id = Id,
        Nombre = Nombre,
        DesdePagina = DesdePagina,
        HastaPagina = HastaPagina
    };
}

public class Libro : ObservableBase
{
    private string _titulo = string.Empty;
    private string _materia = string.Empty;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Titulo
    {
        get => _titulo;
        set
        {
            if (Set(ref _titulo, value))
            {
                OnPropertyChanged(nameof(Resumen));
            }
        }
    }

    public string Materia
    {
        get => _materia;
        set
        {
            if (Set(ref _materia, value))
            {
                OnPropertyChanged(nameof(Resumen));
            }
        }
    }

    /// <summary>Ruta del PDF ya copiado dentro de AppData\Local\AppEstudioUBA\Biblioteca.</summary>
    public string RutaArchivo { get; set; } = string.Empty;

    public string NombreArchivoOriginal { get; set; } = string.Empty;

    public int CantidadPaginas { get; set; }

    public DateTime FechaAgregado { get; set; } = DateTime.Now;

    public List<Modulo> Modulos { get; set; } = new();

    [JsonIgnore]
    public string Resumen => $"{Materia} · {CantidadPaginas} pags. · {Modulos.Count} modulos";

    [JsonIgnore]
    public bool ArchivoDisponible => !string.IsNullOrWhiteSpace(RutaArchivo) && File.Exists(RutaArchivo);

    public void NotificarCambioResumen()
    {
        OnPropertyChanged(nameof(Resumen));
        OnPropertyChanged(nameof(ArchivoDisponible));
    }
}
