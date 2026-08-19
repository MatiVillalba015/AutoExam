using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoExam.Services;

/// <summary>Lectura/escritura de los JSON locales, con escritura atomica y tolerancia a archivos corruptos.</summary>
public static class JsonStore
{
    public static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static T Cargar<T>(string ruta, Func<T> porDefecto)
    {
        try
        {
            if (!File.Exists(ruta))
            {
                return porDefecto();
            }

            string json = File.ReadAllText(ruta);
            if (string.IsNullOrWhiteSpace(json))
            {
                return porDefecto();
            }

            return JsonSerializer.Deserialize<T>(json, Opciones) ?? porDefecto();
        }
        catch (Exception ex)
        {
            RutasApp.RegistrarError($"JsonStore.Cargar({ruta})", ex);
            RespaldarCorrupto(ruta);
            return porDefecto();
        }
    }

    public static void Guardar<T>(string ruta, T valor)
    {
        RutasApp.AsegurarCarpetas();

        string temporal = ruta + ".tmp";
        string json = JsonSerializer.Serialize(valor, Opciones);
        File.WriteAllText(temporal, json);

        if (File.Exists(ruta))
        {
            File.Replace(temporal, ruta, null);
        }
        else
        {
            File.Move(temporal, ruta);
        }
    }

    private static void RespaldarCorrupto(string ruta)
    {
        try
        {
            if (File.Exists(ruta))
            {
                File.Move(ruta, $"{ruta}.corrupto-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
            }
        }
        catch
        {
            // Ignorado.
        }
    }
}
