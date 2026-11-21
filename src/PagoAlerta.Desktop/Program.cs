using PagoAlerta.Core;
using PagoAlerta.Core.Config;
using PagoAlerta.Core.Services;
using PagoAlerta.Desktop.Services;

namespace PagoAlerta.Desktop;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        PagoService servicio;
        try
        {
            var settings = SupabaseSettings.Cargar(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
            servicio = new PagoService(settings, new DpapiSessionStore());
        }
        catch (Exception ex)
        {
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
