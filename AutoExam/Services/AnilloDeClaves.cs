using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>
/// Juego de API Keys con rotacion ante 429.
///
/// El nivel gratuito de Gemini limita por clave y por dia (20 generaciones por dia en los
/// modelos flash, segun el propio mensaje de error de Google). Cuando esa cuota se agota,
/// esperar no sirve: se renueva recien al otro dia. Lo unico que permite seguir es cambiar
/// de clave, y eso es lo que hace este anillo.
///
/// Distingue dos agotamientos distintos, porque la respuesta correcta es distinta:
///  · cuota DIARIA  → la clave queda quemada para toda la sesion, se salta y no se vuelve.
///  · cuota POR MINUTO → la clave se recupera sola en segundos, se posterga pero no se quema.
/// </summary>
public sealed class AnilloDeClaves
{
    private readonly List<string> _claves;
    private readonly HashSet<int> _quemadas = new();
    private readonly object _candado = new();
    private int _indice;

    public AnilloDeClaves(IEnumerable<string> claves)
    {
        _claves = claves
            .Select(GeminiApiService.NormalizarApiKey)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public AnilloDeClaves(AppConfig config) : this(config.ClavesDisponibles) { }

    /// <summary>Anillo de una sola clave, para los caminos que todavia manejan una suelta.</summary>
    public static AnilloDeClaves DeUna(string clave) => new(new[] { clave });

    public int Cantidad => _claves.Count;

    public bool Vacio => _claves.Count == 0;

    /// <summary>Cuantas claves quedan sin quemar por cuota diaria.</summary>
    public int Vivas
    {
        get
        {
            lock (_candado)
            {
                return _claves.Count - _quemadas.Count;
            }
        }
    }

    /// <summary>Numero de la clave en uso (1..N), para mostrarle al usuario cual esta corriendo.</summary>
    public int NumeroActual
    {
        get
        {
            lock (_candado)
            {
                return _indice + 1;
            }
        }
    }

    /// <summary>La clave que hay que usar ahora.</summary>
    public string Actual
    {
        get
        {
            lock (_candado)
            {
                if (_claves.Count == 0)
                {
                    throw new GeminiException(
                        "No hay API Key configurada. Carga tu clave de Gemini en la pestania Ajustes.");
                }

                return _claves[_indice];
            }
        }
    }

    /// <summary>
    /// Pasa a la siguiente clave utilizable. Devuelve false si ya no queda ninguna, que es
    /// la unica situacion en la que el error de cuota tiene que llegar al usuario.
    /// </summary>
    /// <param name="quemarActual">
    /// True cuando el 429 fue por cuota diaria: la clave actual no vuelve a intentarse en
    /// toda la sesion. False para un limite por minuto, que se recupera solo.
    /// </param>
    public bool Rotar(bool quemarActual)
    {
        lock (_candado)
        {
            if (_claves.Count == 0)
            {
                return false;
            }

            if (quemarActual)
            {
                _quemadas.Add(_indice);
            }

            // Se recorren las demas una vuelta completa buscando la primera no quemada.
            for (int salto = 1; salto <= _claves.Count; salto++)
            {
                int candidata = (_indice + salto) % _claves.Count;

                if (!_quemadas.Contains(candidata))
                {
                    _indice = candidata;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Describe el cambio de clave para la barra de progreso.</summary>
    public string DescribirRotacion() => Cantidad <= 1
        ? string.Empty
        : $"Cambiando a la clave {NumeroActual} de {Cantidad}...";
}
