using System.IO;
using AutoExam.Services;

namespace AutoExam.Tests.Infraestructura;

/// <summary>
/// Redirige <see cref="RutasApp.Raiz"/> a una carpeta descartable bajo %TEMP% para toda la
/// colección de tests que la use, usando la puerta que <c>RutasApp.RedirigirRaiz</c> ya expone
/// para este mismo propósito ("La app nunca la llama", ver RutasApp.cs). Sin esto, cualquier
/// test que dispare una escritura de <c>errores.log</c> (p. ej. una excepción de red real
/// durante <see cref="ActualizacionService.PaqueteDisponible"/>) tocaría el
/// %LOCALAPPDATA%\AppEstudioUBA real del usuario que corre la suite.
/// </summary>
public sealed class RutasAisladasFixture : IDisposable
{
    private readonly string _carpetaTemporal;

    public RutasAisladasFixture()
    {
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), "AutoExam.Tests", Guid.NewGuid().ToString("N"));
        RutasApp.RedirigirRaiz(_carpetaTemporal);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_carpetaTemporal))
            {
                Directory.Delete(_carpetaTemporal, recursive: true);
            }
        }
        catch
        {
            // Limpieza best-effort: no puede tumbar la corrida de tests.
        }
    }
}

[CollectionDefinition(Nombre)]
public sealed class RutasAisladasCollection : ICollectionFixture<RutasAisladasFixture>
{
    public const string Nombre = "RutasAisladas";
}
