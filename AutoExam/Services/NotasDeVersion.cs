using System.IO;
using System.Reflection;

namespace AutoExam.Services;

/// <summary>Un grupo de puntos de una version: "Nuevo", "Cambios", "Arreglos".</summary>
public sealed record GrupoDeNotas(string Titulo, IReadOnlyList<string> Puntos);

/// <summary>Las notas de una version concreta.</summary>
public sealed class NotasDeUnaVersion
{
    public NotasDeUnaVersion(string version, string fecha, IReadOnlyList<GrupoDeNotas> grupos)
    {
        Version = version;
        Fecha = fecha;
        Grupos = grupos;
    }

    /// <summary>Numero de version, tal como figura en el archivo (por ejemplo "1.1.0").</summary>
    public string Version { get; }

    /// <summary>Fecha en texto, como se escribio a mano. Vacia si la entrada no la trae.</summary>
    public string Fecha { get; }

    public IReadOnlyList<GrupoDeNotas> Grupos { get; }

    /// <summary>Si esta es la version que la persona tiene instalada ahora mismo.</summary>
    public bool EsLaInstalada { get; internal set; }

    public string Encabezado => EsLaInstalada
        ? $"AutoExam {Version} · la que tenés instalada"
        : $"AutoExam {Version}";

    public string Subtitulo => string.IsNullOrWhiteSpace(Fecha) ? string.Empty : Fecha;

    /// <summary>Cuantos puntos tiene en total, sumando los tres grupos.</summary>
    public int Cantidad => Grupos.Sum(g => g.Puntos.Count);
}

/// <summary>
/// Las notas de version que se muestran en Ajustes (US-040).
///
/// Salen de CHANGELOG.md, que vive en la raiz del repositorio y se embebe en el ejecutable al
/// compilar (RN-51). Dos razones para que sea un archivo del repo y no algo generado:
///
/// · RN-50 lo pide explicitamente separado de los mensajes de commit. Un commit dice "fix:
///   null en PoblarFocos"; una nota de version dice "el repaso ya no se cuelga cuando no
///   tenes preguntas falladas". Son dos textos para dos lectores distintos, y generar uno del
///   otro siempre termina publicando el primero.
/// · Al viajar adentro del build, las notas de la version recien instalada estan disponibles
///   sin conexion, que es justo cuando alguien las quiere leer: despues de que la app se
///   actualizo sola.
///
/// El formato es Markdown a proposito —asi GitHub lo muestra renderizado y se edita a mano sin
/// herramientas—, pero con una forma rigida que hace que parsearlo sea leer linea por linea:
/// <c>## version — fecha</c> abre una version, <c>### titulo</c> abre un grupo, y una linea que
/// empieza con <c>- </c> es un punto.
/// </summary>
public static class NotasDeVersion
{
    private const string RecursoEmbebido = "AutoExam.CHANGELOG.md";

    private static IReadOnlyList<NotasDeUnaVersion>? _cache;

    /// <summary>
    /// Version del ensamblado que lleva estas notas adentro.
    ///
    /// Se pregunta por el ensamblado PROPIO y no por el de entrada como hace
    /// <see cref="ActualizacionService.VersionActual"/>. En la app corriendo son el mismo, pero
    /// no siempre: bajo el runner de tests el ensamblado de entrada es el host de pruebas, y
    /// preguntarle a el devolvia su version en vez de la de AutoExam. Ademas es el emparejamiento
    /// correcto: las notas y el numero de version tienen que salir del mismo build, porque eso
    /// es exactamente lo que la pantalla afirma cuando dice "la que tenes instalada".
    /// </summary>
    public static string VersionDeEsteBuild
    {
        get
        {
            var v = typeof(NotasDeVersion).Assembly.GetName().Version;
            return v is null ? ActualizacionService.VersionActual : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>
    /// Todas las versiones con notas, de la mas nueva a la mas vieja — que es el orden en el
    /// que estan escritas en el archivo, y el que pide el criterio.
    /// </summary>
    public static IReadOnlyList<NotasDeUnaVersion> Todas => _cache ??= Cargar();

    /// <summary>Las notas de la version instalada, o null si esa version no tiene entrada.</summary>
    public static NotasDeUnaVersion? DeLaInstalada =>
        Todas.FirstOrDefault(n => n.Version == VersionDeEsteBuild);

    /// <summary>
    /// True cuando la version instalada no figura en el archivo. Pasa en un build de prueba
    /// hecho entre dos releases: el criterio pide decirlo, no dejar la seccion vacia.
    /// </summary>
    public static bool FaltanLasDeLaInstalada => DeLaInstalada is null;

    public static string AvisoSinNotas =>
        $"Todavía no hay notas cargadas para la versión {VersionDeEsteBuild}. " +
        "Suele pasar con una versión de prueba: las notas se escriben al publicar el release.";

    private static IReadOnlyList<NotasDeUnaVersion> Cargar()
    {
        string texto = LeerRecurso();

        var versiones = Parsear(texto);

        foreach (var version in versiones)
        {
            version.EsLaInstalada = version.Version == VersionDeEsteBuild;
        }

        return versiones;
    }

    private static string LeerRecurso()
    {
        try
        {
            var ensamblado = Assembly.GetExecutingAssembly();
            using var flujo = ensamblado.GetManifestResourceStream(RecursoEmbebido);

            if (flujo is null)
            {
                return string.Empty;
            }

            using var lector = new StreamReader(flujo);
            return lector.ReadToEnd();
        }
        catch (Exception ex)
        {
            // Sin notas la app funciona igual: la pantalla muestra el aviso de que no hay.
            RutasApp.RegistrarError("NotasDeVersion.Leer", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Separa el Markdown en versiones y grupos. Publico para poder probarlo con un texto
    /// armado a mano, sin depender de que el CHANGELOG real diga algo en particular.
    /// </summary>
    public static IReadOnlyList<NotasDeUnaVersion> Parsear(string? markdown)
    {
        var versiones = new List<NotasDeUnaVersion>();

        if (string.IsNullOrWhiteSpace(markdown))
        {
            return versiones;
        }

        string versionActual = string.Empty;
        string fechaActual = string.Empty;
        var grupos = new List<GrupoDeNotas>();

        string tituloGrupo = string.Empty;
        var puntos = new List<string>();
        bool enComentario = false;

        void CerrarGrupo()
        {
            if (puntos.Count > 0)
            {
                grupos.Add(new GrupoDeNotas(
                    tituloGrupo.Length == 0 ? "Novedades" : tituloGrupo, new List<string>(puntos)));
            }

            puntos.Clear();
            tituloGrupo = string.Empty;
        }

        void CerrarVersion()
        {
            CerrarGrupo();

            if (versionActual.Length > 0 && grupos.Count > 0)
            {
                versiones.Add(new NotasDeUnaVersion(versionActual, fechaActual, new List<GrupoDeNotas>(grupos)));
            }

            grupos.Clear();
            versionActual = fechaActual = string.Empty;
        }

        foreach (string cruda in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            string linea = cruda.Trim();

            // El archivo lleva una plantilla comentada para copiar y pegar al agregar una
            // version. Sin saltearla, esa plantilla se mostraria como una version "X.Y.Z".
            if (linea.StartsWith("<!--", StringComparison.Ordinal))
            {
                enComentario = true;
            }

            if (enComentario)
            {
                if (linea.Contains("-->", StringComparison.Ordinal))
                {
                    enComentario = false;
                }

                continue;
            }

            if (linea.StartsWith("## ", StringComparison.Ordinal))
            {
                CerrarVersion();

                // "1.1.0 — 4 de septiembre de 2026". Se admite el guion largo y el corto:
                // escribiendo a mano sale cualquiera de los dos.
                string encabezado = linea[3..].Trim();
                int corte = encabezado.IndexOfAny(new[] { '—', '–', '-' });

                versionActual = corte < 0 ? encabezado : encabezado[..corte].Trim();
                fechaActual = corte < 0 ? string.Empty : encabezado[(corte + 1)..].Trim();

                continue;
            }

            if (linea.StartsWith("### ", StringComparison.Ordinal))
            {
                CerrarGrupo();
                tituloGrupo = linea[4..].Trim();
                continue;
            }

            if (linea.StartsWith("- ", StringComparison.Ordinal) && versionActual.Length > 0)
            {
                puntos.Add(linea[2..].Trim());
                continue;
            }

            // Continuacion de un punto que sigue en la linea de abajo: en el archivo los
            // puntos largos se cortan a mano para que la linea no se vaya de ancho, y sin
            // esto la mitad de la frase desapareceria.
            if (linea.Length > 0 && puntos.Count > 0 && !linea.StartsWith('#'))
            {
                puntos[^1] += " " + linea;
            }
        }

        CerrarVersion();

        return versiones;
    }
}
