using AutoExam.ViewModels;

namespace AutoExam.Tests.ViewModels;

/// <summary>
/// US-019 / RN-16 — el resultado de "Probar conexion" siempre dice qué pasó, en lenguaje simple.
///
/// El servicio ya devolvía un mensaje exacto, pero técnico: "Error 403 de la API de Gemini",
/// "Se agoto la cuota DIARIA (429)". Para alguien que acaba de pegar una clave, eso no contesta
/// la única pregunta que tiene —¿la clave sirve o no?— y encima los cuatro casos se ven igual
/// bajo el mismo título "No se pudo conectar".
///
/// Estos tests fijan la clasificación, que es la parte con reglas: qué titular le corresponde a
/// cada falla. El detalle técnico se conserva debajo, así que no se pierde información.
/// </summary>
public class AjustesProbarConexionTests
{
    [Theory]
    // Falta la clave: no es un fallo de red ni de Google.
    [InlineData("Falta la API Key.", "Falta la clave")]
    [InlineData("No hay API Key configurada. Carga tu clave de Gemini en la pestania Ajustes.", "Falta la clave")]

    // Google rechaza la clave.
    [InlineData("API Key rechazada por Google (403). La clave viaja en la cabecera x-goog-api-key", "Clave invalida")]
    [InlineData("Peticion rechazada por Gemini (400). API key not valid. Please pass a valid API key.", "Clave invalida")]

    // Cuota: hay que distinguir la diaria de la del minuto, porque una se espera y la otra no.
    [InlineData("Se agoto la cuota DIARIA de Gemini (429) en tu clave.", "Cuota agotada")]
    [InlineData("Se agoto la cuota por minuto de tu clave (429) y los reintentos tampoco alcanzaron.", "Demasiados pedidos seguidos")]

    // El modelo, no la clave.
    [InlineData("El modelo no existe o ya fue retirado por Google (404).", "Modelo no disponible")]

    // Red.
    [InlineData("No se pudo contactar a la API de Gemini: No such host is known.", "Sin conexion a internet")]
    [InlineData("La API de Gemini no respondio a tiempo (timeout).", "Sin conexion a internet")]
    public void CadaFalla_RecibeUnTitularEnLenguajeSimple(string mensajeTecnico, string tituloEsperado)
    {
        var (titulo, _) = AjustesViewModel.ClasificarFalla(mensajeTecnico);

        Assert.Equal(tituloEsperado, titulo);
    }

    [Fact]
    public void UnaFallaDesconocida_NoSeQuedaSinTitulo_RN16()
    {
        // RN-16: nunca un estado neutro o sin respuesta visible. Aunque el mensaje no encaje en
        // ninguna categoría conocida, tiene que salir un titular y el detalle.
        var (titulo, motivo) = AjustesViewModel.ClasificarFalla("Algo raro que nadie previo.");

        Assert.False(string.IsNullOrWhiteSpace(titulo));
        Assert.Contains("Algo raro", motivo);
    }

    [Fact]
    public void UnMensajeVacio_NoRompeLaClasificacion()
    {
        var (titulo, _) = AjustesViewModel.ClasificarFalla(null);

        Assert.False(string.IsNullOrWhiteSpace(titulo));
    }

    [Fact]
    public void ElDetalleTecnico_NoSePierdeAlTraducir()
    {
        // El titular es para decidir; el detalle sigue estando para diagnosticar.
        const string tecnico = "API Key rechazada por Google (403). Detalle puntual de Google.";

        var (_, motivo) = AjustesViewModel.ClasificarFalla(tecnico);

        Assert.Contains(tecnico, motivo);
    }

    [Theory]
    [InlineData("Se agoto la cuota DIARIA de Gemini (429) en tu clave.")]
    [InlineData("El modelo no existe o ya fue retirado por Google (404).")]
    public void LasFallasQueNoSonDeLaClave_NoLaAcusanDeInvalida(string mensaje)
    {
        // Distinción que importa: con la cuota agotada o un modelo retirado, la clave está bien.
        // Decirle al usuario "clave invalida" lo mandaría a generar una nueva sin necesidad.
        var (titulo, _) = AjustesViewModel.ClasificarFalla(mensaje);

        Assert.NotEqual("Clave invalida", titulo);
    }
}
