using System.Collections.ObjectModel;
using AutoExam.Models;

namespace AutoExam.Services;

/// <summary>Configuracion (config.json) e historial del estudiante (perfil.json).</summary>
public class SesionUsuarioService
{
    private const int MaxHistorial = 300;

    public AppConfig Config { get; private set; } = new();

    public PerfilUsuario Perfil { get; private set; } = new();

    /// <summary>Vista del historial ordenada de mas nuevo a mas viejo, enlazable a la UI.</summary>
    public ObservableCollection<ExamenRendido> Historial { get; } = new();

    public bool HayApiKey => !string.IsNullOrWhiteSpace(Config.ApiKey);

    /// <summary>Modelo retirado que se reemplazo al cargar, o null si no hubo migracion.</summary>
    public string? ModeloMigradoDesde { get; private set; }

    public void Cargar()
    {
        RutasApp.AsegurarCarpetas();

        Config = JsonStore.Cargar(RutasApp.ArchivoConfig, () => new AppConfig());
        Perfil = JsonStore.Cargar(RutasApp.ArchivoPerfil, () => new PerfilUsuario());

        // Saneamiento de valores fuera de rango escritos a mano en el JSON.
        Config.Modelo = string.IsNullOrWhiteSpace(Config.Modelo)
            ? AppConfig.ModeloPorDefecto
            : Config.Modelo.Trim();

        MigrarModeloRetirado();
        MigrarClavesDeGemini();
        MigrarTemaDeColor();

        Config.Zoom = Models.ZoomDeLaApp.Acotar(Config.Zoom);

        Config.PreguntasPorLote = Math.Clamp(Config.PreguntasPorLote, 5, 15);
        Config.PaginasPorBloque = Math.Clamp(Config.PaginasPorBloque, 5, 40);
        Config.MaxCaracteresContexto = Math.Clamp(Config.MaxCaracteresContexto, 10_000, 300_000);
        Config.MaxImagenesPorExamen = Math.Clamp(Config.MaxImagenesPorExamen, 0, 30);
        Config.TamanioTextoExamen = Math.Clamp(Config.TamanioTextoExamen, 0, 4);

        RefrescarHistorial();
    }

    /// <summary>
    /// Google retira generaciones viejas de Gemini y la API pasa a devolver 404. Si config.json
    /// quedo apuntando a una de esas, se reemplaza sola para que la app siga siendo usable.
    /// </summary>
    private void MigrarModeloRetirado()
    {
        bool retirado = AppConfig.PrefijosRetirados
            .Any(p => Config.Modelo.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!retirado)
        {
            ModeloMigradoDesde = null;
            return;
        }

        ModeloMigradoDesde = Config.Modelo;
        Config.Modelo = AppConfig.ModeloPorDefecto;
        GuardarConfig();
    }

    /// <summary>
    /// RN-52: pasa las claves guardadas en el formato viejo (un texto con varias separadas por
    /// coma) a una entrada por clave, que es lo que US-042 muestra como pestanias. Corre sola
    /// al arrancar, no le pregunta nada al usuario y solo reescribe config.json si de verdad
    /// habia algo que separar.
    /// </summary>
    private void MigrarClavesDeGemini()
    {
        if (Config.MigrarClavesASeparadas())
        {
            GuardarConfig();
        }
    }

    /// <summary>
    /// US-049: hasta esta version lo unico elegible era claro u oscuro, guardado en
    /// <c>TemaOscuro</c>. Un config.json anterior no tiene <c>TemaDeColor</c>, asi que al
    /// deserializar queda con el default —violeta, que es oscuro— y alguien que venia usando el
    /// tema claro abriria en oscuro sin haber tocado nada.
    ///
    /// Se detecta por la contradiccion: el tema resuelto dice oscuro y el campo viejo dice
    /// claro. En ese caso manda el campo viejo, que es el que el usuario efectivamente eligio.
    /// Al reves no hace falta mirar: el default ya coincide con <c>TemaOscuro = true</c>.
    /// </summary>
    private void MigrarTemaDeColor()
    {
        var tema = Models.PaletaDeApp.Resolver(Config.TemaDeColor);

        if (tema.Oscuro && !Config.TemaOscuro)
        {
            Config.TemaDeColor = Models.PaletaDeApp.DesdeTemaOscuro(false);
            GuardarConfig();
            return;
        }

        // Una clave desconocida (config.json a mano, o de una version futura) se normaliza a la
        // que se resolvio, para que lo guardado y lo que se ve no digan cosas distintas.
        Config.TemaDeColor = tema.Clave;
        Config.TemaOscuro = tema.Oscuro;
    }

    public void GuardarConfig() => JsonStore.Guardar(RutasApp.ArchivoConfig, Config);

    public void GuardarPerfil() => JsonStore.Guardar(RutasApp.ArchivoPerfil, Perfil);

    public void RegistrarExamen(ExamenRendido examen)
    {
        Perfil.Historial.Add(examen);

        if (Perfil.Historial.Count > MaxHistorial)
        {
            Perfil.Historial.RemoveRange(0, Perfil.Historial.Count - MaxHistorial);
        }

        GuardarPerfil();
        RefrescarHistorial();
    }

    /// <summary>Actualiza un intento ya guardado (por ejemplo al cerrar una ronda de revancha).</summary>
    public void ActualizarExamen(ExamenRendido examen)
    {
        int indice = Perfil.Historial.FindIndex(e => e.Id == examen.Id);
        if (indice >= 0)
        {
            Perfil.Historial[indice] = examen;
            GuardarPerfil();
            RefrescarHistorial();
        }
    }

    public void BorrarHistorial()
    {
        Perfil.Historial.Clear();
        GuardarPerfil();
        RefrescarHistorial();
    }

    /// <summary>
    /// Borra un unico intento del historial (US-012). Las estadisticas de
    /// <see cref="PerfilUsuario"/> son getters calculados sobre <c>Historial</c>, asi que
    /// se recalculan solas. Id inexistente = no-op sin excepcion.
    /// </summary>
    public void BorrarExamen(string id)
    {
        int quitados = Perfil.Historial.RemoveAll(e => e.Id == id);
        if (quitados == 0)
        {
            return;
        }

        GuardarPerfil();
        RefrescarHistorial();
    }

    private void RefrescarHistorial()
    {
        Historial.Clear();
        foreach (var examen in Perfil.Historial.OrderByDescending(e => e.Fecha))
        {
            Historial.Add(examen);
        }
    }
}
