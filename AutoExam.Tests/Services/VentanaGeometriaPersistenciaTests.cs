using System.IO;
using System.Windows;
using AutoExam.Models;
using AutoExam.Services;
using AutoExam.Tests.Infraestructura;

namespace AutoExam.Tests.Services;

/// <summary>
/// US-003 / AC-T8 (specs/02-tech-spec.md) — "cerrar la app con geometría distinta a la
/// default y reabrirla reproduce tamaño, posición y estado exactos, leídos de AppConfig".
/// Contrato de campos/tipos/defaults en specs/03-architecture.md §4.3.
///
/// No cubre AC-T9 (fallback de monitor ausente) — eso es <see cref="GeometriaVentanaServiceTests"/>,
/// que prueba <c>GeometriaVentanaService.EstaVisible</c> en aislamiento total. Este archivo
/// prueba la otra mitad del contrato: que lo que se guarda vuelve exacto, sin pasar por ninguna
/// decisión de "¿está visible?" — eso corresponde a <c>MainWindow.Ventana_Loaded</c>
/// (code-behind con Screen real, fuera de alcance de test automatizado por diseño, ver
/// specs/03-architecture.md §1.3).
///
/// Comparte <see cref="RutasAisladasCollection"/> con el resto de la suite que toca
/// <c>RutasApp.Raiz</c> (redirección de raíz + serialización de tests en esa colección):
/// evita la carrera de archivo que produce <c>SesionUsuarioService.GuardarConfig()</c> cuando
/// dos clases de test distintas redirigen la raíz global en paralelo (ver
/// ExamenTamanioTextoMapeoTests / TamanioTextoExamenPersistenciaTests, que no la comparten y
/// por eso a veces chocan en "config.json.tmp ... being used by another process").
/// </summary>
[Collection(RutasAisladasCollection.Nombre)]
public class VentanaGeometriaPersistenciaTests
{
    [Fact]
    public void AppConfig_nuevo_trae_los_defaults_del_contrato_arquitectura_4_3()
    {
        var config = new AppConfig();

        Assert.Equal(-1, config.VentanaAncho);
        Assert.Equal(-1, config.VentanaAlto);
        Assert.Equal(-1, config.VentanaX);
        Assert.Equal(-1, config.VentanaY);
        Assert.Equal(WindowState.Normal, config.VentanaEstado);
    }

    [Theory]
    [InlineData(WindowState.Normal)]
    [InlineData(WindowState.Maximized)]
    public void JsonStore_hace_round_trip_exacto_de_tamanio_posicion_y_estado(WindowState estado)
    {
        string ruta = RutaTemporal();

        try
        {
            JsonStore.Guardar(ruta, new AppConfig
            {
                VentanaAncho = 1366,
                VentanaAlto = 900,
                VentanaX = 137.5,
                VentanaY = -40, // multi-monitor: coordenadas negativas son validas (NFR-07)
                VentanaEstado = estado,
            });

            // Nueva variable, no el mismo objeto en memoria: fuerza una deserializacion real
            // desde el archivo, como pasa al reabrir la app.
            AppConfig recargado = JsonStore.Cargar(ruta, () => new AppConfig());

            Assert.Equal(1366, recargado.VentanaAncho);
            Assert.Equal(900, recargado.VentanaAlto);
            Assert.Equal(137.5, recargado.VentanaX);
            Assert.Equal(-40, recargado.VentanaY);
            Assert.Equal(estado, recargado.VentanaEstado);
        }
        finally
        {
            BorrarSiExiste(ruta);
        }
    }

    [Fact]
    public void Un_config_json_legacy_sin_los_campos_de_ventana_cae_al_centinela_sin_romper_el_resto()
    {
        // Retrocompatibilidad: un config.json escrito antes de US-003 no tiene las claves
        // "Ventana*". JsonSerializer debe completarlas con el default del modelo (-1 / Normal),
        // no tirar excepcion ni pisar el resto de los campos ya guardados.
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

            Assert.Equal(-1, cargado.VentanaAncho);
            Assert.Equal(-1, cargado.VentanaAlto);
            Assert.Equal(WindowState.Normal, cargado.VentanaEstado);
            Assert.Equal("clave-vieja", cargado.ApiKey);
            Assert.False(cargado.TemaOscuro);
        }
        finally
        {
            BorrarSiExiste(ruta);
        }
    }

    [Fact]
    public void Reabrir_la_app_con_una_nueva_instancia_de_SesionUsuarioService_conserva_la_geometria_guardada()
    {
        const double anchoGuardado = 1600;
        const double altoGuardado = 950;
        const double xGuardado = 60;
        const double yGuardado = 30;
        const WindowState estadoGuardado = WindowState.Maximized;

        var sesionAntesDeCerrar = new SesionUsuarioService();
        sesionAntesDeCerrar.Cargar(); // primera apertura: no hay config.json todavia, usa default.
        Assert.Equal(-1, sesionAntesDeCerrar.Config.VentanaAncho);

        sesionAntesDeCerrar.Config.VentanaAncho = anchoGuardado;
        sesionAntesDeCerrar.Config.VentanaAlto = altoGuardado;
        sesionAntesDeCerrar.Config.VentanaX = xGuardado;
        sesionAntesDeCerrar.Config.VentanaY = yGuardado;
        sesionAntesDeCerrar.Config.VentanaEstado = estadoGuardado;
        sesionAntesDeCerrar.GuardarConfig();

        // "Reabrir la app": instancia nueva, no reutiliza el objeto Config en memoria — mismo
        // patron que MainWindow.Ventana_Loaded / RestaurarGeometria en la app real.
        var sesionDespuesDeReabrir = new SesionUsuarioService();
        sesionDespuesDeReabrir.Cargar();

        Assert.Equal(anchoGuardado, sesionDespuesDeReabrir.Config.VentanaAncho);
        Assert.Equal(altoGuardado, sesionDespuesDeReabrir.Config.VentanaAlto);
        Assert.Equal(xGuardado, sesionDespuesDeReabrir.Config.VentanaX);
        Assert.Equal(yGuardado, sesionDespuesDeReabrir.Config.VentanaY);
        Assert.Equal(estadoGuardado, sesionDespuesDeReabrir.Config.VentanaEstado);
    }

    private static string RutaTemporal() =>
        Path.Combine(Path.GetTempPath(), $"autoexam-tests-ventana-{Guid.NewGuid():N}.json");

    private static void BorrarSiExiste(string ruta)
    {
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
        }
    }
}
