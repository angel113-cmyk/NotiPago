using PagoAlerta.Core;
using PagoAlerta.Core.Config;
using PagoAlerta.Core.Logging;
using PagoAlerta.Core.Services;
using PagoAlerta.Desktop.Services;

namespace PagoAlerta.Desktop;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // HT-17: registro de errores en %LocalAppData%\PagoAlerta\logs (sin datos de pagos).
        Registro.Iniciar(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Nombre, "logs"));
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            Registro.Error("Error no controlado en la interfaz", e.Exception);
            MessageBox.Show("Ocurrió un error inesperado. Si se repite, revisa el archivo de registro en " +
                            Path.GetDirectoryName(Registro.RutaActual), AppInfo.Nombre, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Registro.Error("Error fatal", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Registro.Error("Error en tarea de fondo", e.Exception);
            e.SetObserved();
        };

        PagoService servicio;
        try
        {
            var settings = SupabaseSettings.Cargar(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
            servicio = new PagoService(settings, new DpapiSessionStore());
        }
        catch (Exception ex)
        {
            Registro.Error("No se pudo cargar la configuración", ex);
            MessageBox.Show(ex.Message, AppInfo.Nombre, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Login -> principal; al cerrar sesion se vuelve al login.
        while (true)
        {
            using var login = new LoginForm(servicio);
            if (login.ShowDialog() != DialogResult.OK) return;

            using var principal = new MainForm(servicio);
            Application.Run(principal);
            if (!principal.CerrarSesionSolicitado) return;
        }
    }
}
