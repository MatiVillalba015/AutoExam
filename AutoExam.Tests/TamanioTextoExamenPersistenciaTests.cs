using System.IO;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests;

/// <summary>
/// US-005 / AC-T14 (specs/02-tech-spec.md) — "la preferencia de tamaño ajustada se lee de
/// AppConfig al reabrir la app y no vuelve al valor por defecto".
///
/// Comparte <see cref="RutasAisladasCollection"/>: el test de "reabrir la app" redirige
/// <c>RutasApp.Raiz</c> (static mutable de todo el proceso) — sin esta colección corría en
/// paralelo contra la raíz real del usuario o contra la de otra clase redirigiéndola a la vez,
/// con choques intermitentes de archivo ("config.json.tmp ... being used by another process").
///
/// Dos niveles de test, no duplicados:
/// - <see cref="JsonStore_hace_round_trip_del_campo_para_cada_nivel"/> (unit): serializacion
///   pura de <c>AppConfig</c> contra <c>JsonStore</c>, sin pasar por rutas de disco reales de
///   la app ni por <c>SesionUsuarioService</c>. Aisla el contrato de persistencia del campo en
///   si (tipo, nombre, default) de cualquier logica de saneamiento.
/// - <see cref="Reabrir_la_app_con_una_nueva_instancia_de_SesionUsuarioService_conserva_el_nivel_guardado"/>
///   (integracion): reproduce el camino real que usa la app (SesionUsuarioService.Cargar /
///   GuardarConfig contra RutasApp.ArchivoConfig) instanciando el servicio DOS veces — la
///   segunda simula el "reabrir la app" del AC — para detectar bugs de wiring que el test de
///   unidad no puede ver (ej. que Iniciar/Cargar no invoque JsonStore, o que algun saneamiento
///   pise el valor guardado).
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class TamanioTextoExamenPersistenciaTests
{
    [Fact]
    public void AppConfig_nuevo_trae_el_nivel_por_defecto_sin_persistencia_previa()
    {
        // specs/03-architecture.md §4.5: "public int TamanioTextoExamen { get; set; } = 2".
        var config = new AppConfig();

        Assert.Equal(2, config.TamanioTextoExamen);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void JsonStore_hace_round_trip_del_campo_para_cada_nivel(int nivel)
    {
        string ruta = RutaTemporal();

        try
        {
            JsonStore.Guardar(ruta, new AppConfig { TamanioTextoExamen = nivel });

            // Nueva variable, no el mismo objeto en memoria: fuerza una deserializacion real
            // desde el archivo, como pasa al reabrir la app.
            AppConfig recargado = JsonStore.Cargar(ruta, () => new AppConfig());

            Assert.Equal(nivel, recargado.TamanioTextoExamen);
        }
        finally
        {
            BorrarSiExiste(ruta);
        }
    }

    [Fact]
    public void Un_config_json_legacy_sin_el_campo_nuevo_cae_al_default_sin_romper_el_resto()
    {
        // Retrocompatibilidad: un config.json escrito antes de US-005 no tiene la clave
        // "TamanioTextoExamen". JsonSerializer debe completarla con el default del modelo
        // (2), no tirar excepcion ni pisar el resto de los campos ya guardados.
        string ruta = RutaTemporal();

        try
        {
            File.WriteAllText(ruta, /*lang=json,strict*/ """
                {
                  "ApiKey": "clave-vieja",
                  "TemaOscuro": false
                }
                """);

            AppConfig cargado = JsonStore.Cargar(ruta, () => new AppConfig());

            Assert.Equal(2, cargado.TamanioTextoExamen);
            Assert.Equal("clave-vieja", cargado.ApiKey);
            Assert.False(cargado.TemaOscuro);
        }
        finally
        {
            BorrarSiExiste(ruta);
        }
    }

    [Fact]
    public void Reabrir_la_app_con_una_nueva_instancia_de_SesionUsuarioService_conserva_el_nivel_guardado()
    {
        string raizOriginal = RutasApp.Raiz;
        string raizTemporal = Path.Combine(Path.GetTempPath(), "AutoExamTests_" + Guid.NewGuid().ToString("N"));

        try
        {
            RutasApp.RedirigirRaiz(raizTemporal);

            const int nivelDistintoDelDefault = 4;

            var sesionAntesDeCerrar = new SesionUsuarioService();
            sesionAntesDeCerrar.Cargar(); // primera apertura: no hay config.json todavia, usa default.
            Assert.Equal(2, sesionAntesDeCerrar.Config.TamanioTextoExamen);

            sesionAntesDeCerrar.Config.TamanioTextoExamen = nivelDistintoDelDefault;
            sesionAntesDeCerrar.GuardarConfig();

            // "Reabrir la app": instancia nueva, no reutiliza el objeto Config en memoria.
            var sesionDespuesDeReabrir = new SesionUsuarioService();
            sesionDespuesDeReabrir.Cargar();

            Assert.Equal(nivelDistintoDelDefault, sesionDespuesDeReabrir.Config.TamanioTextoExamen);
        }
        finally
        {
            RutasApp.RedirigirRaiz(raizOriginal);

            try
            {
                if (Directory.Exists(raizTemporal))
                {
                    Directory.Delete(raizTemporal, recursive: true);
                }
            }
            catch
            {
                // Limpieza best-effort: no debe hacer fallar el test si el archivo quedo lockeado.
            }
        }
    }

    private static string RutaTemporal() =>
        Path.Combine(Path.GetTempPath(), $"autoexam-tests-{Guid.NewGuid():N}.json");

    private static void BorrarSiExiste(string ruta)
    {
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
        }
    }
}
