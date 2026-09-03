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

    /// <summary>
    /// Familia de la fuente. Los registros viejos de libros.json no lo traen: al
    /// deserializar cae en el default <see cref="TipoFuente.Pdf"/> (valor 0), y
    /// <see cref="Services.BibliotecaService.Cargar"/> completa el resto.
    /// </summary>
    public TipoFuente Tipo { get; set; } = TipoFuente.Pdf;

    /// <summary>
    /// Ruta del archivo (PDF/Office) o de la primera imagen ya copiada dentro de
    /// AppData\Local\AppEstudioUBA\Biblioteca. Para tipos de archivo unico coincide
    /// con <see cref="Archivos"/>[0]. Se conserva por compatibilidad de deserializacion.
    /// </summary>
    public string RutaArchivo { get; set; } = string.Empty;

    /// <summary>
    /// Rutas internas de todos los archivos de la fuente, en orden. Un unico elemento
    /// para PDF/Office; N imagenes ordenadas para <see cref="TipoFuente.SetImagenes"/>.
    /// Los registros viejos (sin este campo) se rellenan con [<see cref="RutaArchivo"/>]
    /// en <see cref="Services.BibliotecaService.Cargar"/>.
    /// </summary>
    public List<string> Archivos { get; set; } = new();

    public string NombreArchivoOriginal { get; set; } = string.Empty;

    public int CantidadPaginas { get; set; }

    /// <summary>
    /// Medida de tamanio en texto libre segun el formato ("34 diapositivas",
    /// "5 hojas · ~1.2k filas", "8 imagenes", "documento unico"). La puebla
    /// <see cref="Services.BibliotecaService.AgregarFuenteAsync"/> via el contrato
    /// <c>IExtractorContenido.MedirAsync</c> (arquitectura Inc-4 §4.1).
    /// </summary>
    public string MedidaTamanio { get; set; } = string.Empty;

    public DateTime FechaAgregado { get; set; } = DateTime.Now;

    public List<Modulo> Modulos { get; set; } = new();

    /// <summary>
    /// Resumen de "de que trata" este material, generado por IA bajo demanda (US-020).
    ///
    /// Vacio mientras el alumno no lo pida: RN-17 prohibe generarlo al subir el archivo, para
    /// no gastar cuota en materiales que quiza nunca use. Se persiste una vez generado —de ahi
    /// que no lleve JsonIgnore, a diferencia de <see cref="Resumen"/>, que es texto calculado—
    /// porque volver a pedirlo cada vez que se abre el libro gastaria otra peticion del dia por
    /// un texto que no cambia.
    /// </summary>
    public string DeQueTrata
    {
        get => _deQueTrata;
        set => Set(ref _deQueTrata, value);
    }

    private string _deQueTrata = string.Empty;

    /// <summary>true si este material ya tiene un resumen generado y guardado.</summary>
    [JsonIgnore]
    public bool TieneResumen => !string.IsNullOrWhiteSpace(DeQueTrata);

    /// <summary>
    /// Marcado en el paso "Material" del asistente para entrar en un examen combinado
    /// (US-024). No se persiste: es una eleccion de un examen puntual, no un atributo del
    /// material, y dejarlo guardado haria que el proximo examen arrancara con documentos
    /// tildados que el alumno no eligio.
    /// </summary>
    [JsonIgnore]
    public bool Seleccionado
    {
        get => _seleccionado;
        set => Set(ref _seleccionado, value);
    }

    private bool _seleccionado;

    /// <summary>true para las familias que se guardan como un unico archivo (todo salvo el set de imagenes).</summary>
    [JsonIgnore]
    public bool EsArchivoUnico => Tipo != TipoFuente.SetImagenes;

    [JsonIgnore]
    public string Resumen => Tipo == TipoFuente.Pdf
        ? $"{Materia} · {CantidadPaginas} pags. · {Modulos.Count} modulos"
        : $"{Materia} · {(string.IsNullOrWhiteSpace(MedidaTamanio) ? "material" : MedidaTamanio)}";

    [JsonIgnore]
    public bool ArchivoDisponible => !string.IsNullOrWhiteSpace(RutaArchivo) && File.Exists(RutaArchivo);

    /// <summary>
    /// Color de identidad de la materia de este libro (US-027). Se resuelve por NOMBRE en
    /// tiempo de dibujado y no se guarda con el libro (RN-30): asi, cambiarle el color a una
    /// materia repinta todo su material de una, sin reescribir libros.json.
    /// </summary>
    [JsonIgnore]
    public string ColorMateria => PaletaMaterias.ColorDe(Materia);

    public void NotificarCambioResumen()
    {
        OnPropertyChanged(nameof(Resumen));
        OnPropertyChanged(nameof(ArchivoDisponible));
        OnPropertyChanged(nameof(ColorMateria));
    }
}
