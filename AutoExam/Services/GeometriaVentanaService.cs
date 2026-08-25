namespace AutoExam.Services;

/// <summary>
/// Logica pura de geometria de ventana (US-003), separada de <c>MainWindow</c> para
/// poder probarla sin levantar una ventana real (ver AutoExam.Tests, AC-T8/AC-T9).
/// No depende de <c>System.Windows.Forms.Screen</c> directamente: recibe las areas de
/// trabajo ya resueltas, asi el llamador decide de donde salen (en produccion,
/// <c>Screen.AllScreens</c>; en tests, rectangulos fijos).
/// </summary>
public static class GeometriaVentanaService
{
    /// <summary>
    /// False si nunca se guardo una geometria (valor centinela -1 en ancho/alto, ver
    /// <see cref="Models.AppConfig.VentanaAncho"/>). En ese caso MainWindow debe quedarse
    /// con el default del XAML (CenterScreen, 1240x820) sin evaluar nada mas.
    /// </summary>
    public static bool HayGeometriaGuardada(double ancho, double alto) => ancho >= 0 && alto >= 0;

    /// <summary>
    /// True si el rectangulo (x, y, ancho, alto) intersecta el area de trabajo de al menos
    /// uno de los monitores conectados. Si da false (por ejemplo, se desconecto el monitor
    /// donde estaba la ventana), MainWindow debe caer al comportamiento por defecto en vez
    /// de dejar la ventana fuera de vista.
    /// </summary>
    public static bool EstaVisible(
        double x, double y, double ancho, double alto,
        IEnumerable<System.Drawing.Rectangle> areasDeTrabajo)
    {
        var candidato = new System.Drawing.Rectangle((int)x, (int)y, (int)ancho, (int)alto);
        return areasDeTrabajo.Any(area => area.IntersectsWith(candidato));
    }
}
