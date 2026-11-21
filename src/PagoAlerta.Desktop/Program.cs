using PagoAlerta.Core;
using PagoAlerta.Core.Config;
using PagoAlerta.Core.Logging;
using PagoAlerta.Core.Services;
using PagoAlerta.Desktop.Services;

namespace PagoAlerta.Desktop;

static class Program
{
    /// <summary>Con este argumento (inicio con Windows) la app arranca oculta junto al reloj si ya hay sesion guardada.</summary>
    internal const string ArgumentoSegundoPlano = "--segundo-plano";

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Una sola copia: si ya corre (p. ej. oculta en la bandeja), se le avisa y esta se cierra.
        if (!InstanciaUnica.EsPrimera()) return;

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

        // Puente al hilo de la interfaz para cuando otra copia pida mostrar la ventana.
        var puente = new Control();
        _ = puente.Handle;
        InstanciaUnica.Escuchar(() =>
        {
            try { puente.BeginInvoke(new Action(MostrarVentanaActual)); } catch { }
        });

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

        var enSegundoPlano = args.Contains(ArgumentoSegundoPlano, StringComparer.OrdinalIgnoreCase);

        // Login -> principal; al cerrar sesion se vuelve al login.
        while (true)
        {
            using var login = new LoginForm(servicio, silencioso: enSegundoPlano);
            if (login.ShowDialog() != DialogResult.OK) return;

            using var principal = new MainForm(servicio, iniciarOculto: enSegundoPlano);
            enSegundoPlano = false;               // solo el primer arranque va oculto
            Application.Run(principal);
            if (!principal.CerrarSesionSolicitado) return;
        }
    }

    /// <summary>Trae al frente la ventana que este abierta (la principal, si estaba oculta en la bandeja).</summary>
    private static void MostrarVentanaActual()
    {
        var f = Application.OpenForms.Cast<Form>().LastOrDefault();
        if (f is null) return;
        if (f is MainForm m) { m.MostrarDesdeBandeja(); return; }
        f.Opacity = 1;
        f.ShowInTaskbar = true;
        if (f.WindowState == FormWindowState.Minimized) f.WindowState = FormWindowState.Normal;
        f.Activate();
    }
}
