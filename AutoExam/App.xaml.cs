using System.Windows;
using System.Windows.Threading;
using AutoExam.Services;
using AutoExam.ViewModels;

namespace AutoExam;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        RutasApp.AsegurarCarpetas();

        // La limpieza de imagenes viejas se movio a ShellViewModel.IniciarAsync: aca todavia
        // no se leyo perfil.json, asi que no hay forma de saber que examenes siguen en el
        // historial y sus figuras se borraban a los siete dias (US-018/US-025).

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                RutasApp.RegistrarError("UnhandledException", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            RutasApp.RegistrarError("UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);

        // Raiz de composicion: es el unico lugar donde se instancian servicios.
        // Los ViewModels los reciben por constructor, asi que se pueden probar
        // con dobles sin levantar la ventana.
        var sesion = new SesionUsuarioService();

        var shell = new ShellViewModel(
            new BibliotecaService(),
            sesion,
            new PdfExtractorService(),
            new GeminiApiService(),
            new DialogoService());

        // El actualizador necesita config.json para acordarse, entre reinicios, de que
        // version intento instalar: es lo que le permite cortar un bucle de actualizacion.
        ActualizacionService.UsarSesion(sesion);

        MainWindow = new MainWindow { DataContext = shell };
        MainWindow.Show();

        // Despues de Show() a proposito: la comprobacion sale por la red y bloquear el
        // arranque con ella dejaria la pantalla en negro cuando la conexion esta lenta.
        ActualizacionService.ComprobarAlIniciar();
    }

    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        RutasApp.RegistrarError("DispatcherUnhandledException", e.Exception);

        MessageBox.Show(
            $"Ocurrio un error inesperado:\n\n{e.Exception.Message}\n\nEl detalle quedo en:\n{RutasApp.ArchivoLog}",
            "AutoExam",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
