namespace AutoExam.Models;

/// <summary>
/// Un tema de color de la app (US-049): el acento general y si va sobre fondo oscuro o claro.
///
/// No confundir con <see cref="PaletaMaterias"/>: aquel pinta cada materia y lo elige el
/// alumno materia por materia; este pinta los botones principales, los bordes de foco y los
/// elementos destacados de toda la app. RN-57 los deja explicitamente independientes —
/// cambiar el tema general no toca ningun color de materia.
/// </summary>
/// <param name="Clave">Identificador que se guarda en config.json. Estable: es lo que se lee al arrancar.</param>
/// <param name="Nombre">Como se llama en pantalla.</param>
/// <param name="Oscuro">Sobre que juego de tokens se monta (Tokens.Oscuro o Tokens.Claro).</param>
/// <param name="Marca">Acento principal. Vacio = se deja el que trae el juego de tokens.</param>
/// <param name="MarcaFuerte">Variante mas luminosa, para texto e iconos sobre fondo tenue.</param>
/// <param name="MarcaSuave">Fondo tenue del acento (chips, insignias, superficies elegidas).</param>
/// <param name="SobreMarca">Color del texto que va ENCIMA del acento pleno.</param>
/// <param name="IconoDesde">Extremo claro del degradado del cuadrado de icono (US-041).</param>
/// <param name="IconoHasta">Extremo oscuro del mismo degradado.</param>
public sealed record TemaDeApp(
    string Clave,
    string Nombre,
    bool Oscuro,
    string Marca = "",
    string MarcaFuerte = "",
    string MarcaSuave = "",
    string SobreMarca = "",
    string IconoDesde = "",
    string IconoHasta = "")
{
    /// <summary>
    /// True cuando el tema se conforma con el acento que ya trae su juego de tokens. Los dos
    /// temas violeta (oscuro y claro) son asi: son la paleta original de la app, y repetir
    /// sus colores aca los dejaria escritos en dos lugares que se desincronizan.
    /// </summary>
    public bool UsaElAcentoDeLosTokens => Marca.Length == 0;
}

/// <summary>
/// Catalogo cerrado de temas de la app (US-049 / RN-57).
///
/// <b>Por que una lista fija y no un selector RGB:</b> el acento se dibuja sobre el fondo de
/// la app, adentro de chips tenues y debajo de texto blanco. Un color libre deja elegir uno
/// que en alguno de esos tres usos no llega al contraste minimo, y el alumno no tiene por que
/// estar calculando contraste. Estos cuatro estan elegidos para cumplirlo en los tres.
///
/// <b>Por que el verde no choca con la correccion:</b> RN-27 reserva verde y rojo para
/// correcta e incorrecta. El verde de este catalogo es un verde azulado bien corrido del
/// verde de acierto (#00FE7F), justamente para que un boton principal no se lea como una
/// respuesta bien contestada.
/// </summary>
public static class PaletaDeApp
{
    public const string PorDefecto = "violeta";

    /// <summary>Los temas ofrecibles, en el orden en que se muestran.</summary>
    public static readonly IReadOnlyList<TemaDeApp> Temas = new[]
    {
        new TemaDeApp(PorDefecto, "Violeta", Oscuro: true),

        new TemaDeApp("azul", "Azul", Oscuro: true,
            Marca: "#6FA5F0", MarcaFuerte: "#93BEFF", MarcaSuave: "#1D2740",
            SobreMarca: "#0C111C", IconoDesde: "#2C3A62", IconoHasta: "#222C4A"),

        new TemaDeApp("verde", "Verde", Oscuro: true,
            Marca: "#45BFA0", MarcaFuerte: "#68D8BB", MarcaSuave: "#152F29",
            SobreMarca: "#08150F", IconoDesde: "#1F4A40", IconoHasta: "#193A33"),

        new TemaDeApp("claro", "Claro", Oscuro: false),
    };

    /// <summary>
    /// El tema guardado, o el violeta si la clave no existe. Una clave desconocida —de una
    /// version futura, o de un config.json editado a mano— no puede dejar la app sin paleta.
    /// </summary>
    public static TemaDeApp Resolver(string? clave)
    {
        var elegido = Temas.FirstOrDefault(
            t => string.Equals(t.Clave, clave, StringComparison.OrdinalIgnoreCase));

        return elegido ?? Temas[0];
    }

    /// <summary>
    /// Clave del tema que corresponde a un <c>TemaOscuro</c> del formato anterior a US-049,
    /// cuando la unica eleccion era claro u oscuro y el acento siempre era violeta.
    /// </summary>
    public static string DesdeTemaOscuro(bool oscuro) => oscuro ? PorDefecto : "claro";
}

/// <summary>
/// Limites del zoom general de la app (US-048).
///
/// Los topes no son arbitrarios: por debajo de 80% los rotulos de 11pt de la app dejan de
/// leerse, y por encima de 150% la ventana en su ancho minimo (980 px) ya no puede mostrar
/// las dos columnas de ninguna pantalla. Fuera de ese rango el control se apaga en vez de
/// dejar la app inutilizable, que es lo que pide el ultimo criterio de la historia.
/// </summary>
public static class ZoomDeLaApp
{
    public const double Minimo = 0.8;

    public const double Normal = 1.0;

    public const double Maximo = 1.5;

    /// <summary>Salto de cada toque de "+" o "-". Diez puntos: menos no se nota, mas salta.</summary>
    public const double Paso = 0.1;

    /// <summary>Deja el valor dentro de los limites y redondeado al paso, para que no acumule coma flotante.</summary>
    public static double Acotar(double escala)
    {
        if (double.IsNaN(escala) || double.IsInfinity(escala))
        {
            return Normal;
        }

        double redondeado = Math.Round(escala / Paso) * Paso;

        return Math.Clamp(Math.Round(redondeado, 2), Minimo, Maximo);
    }

    public static double Aumentar(double escala) => Acotar(escala + Paso);

    public static double Reducir(double escala) => Acotar(escala - Paso);

    public static bool PuedeAumentar(double escala) => Acotar(escala) < Maximo;

    public static bool PuedeReducir(double escala) => Acotar(escala) > Minimo;

    /// <summary>El porcentaje que se muestra al lado del control ("100%").</summary>
    public static string ComoPorcentaje(double escala) =>
        Math.Round(Acotar(escala) * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
