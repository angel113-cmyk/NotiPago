using PagoAlerta.Core.Logging;
using PagoAlerta.Movil.Infra;

namespace PagoAlerta.Movil;

[Application(Label = "@string/app_name")]
public class PagoAlertaApp : Application
{
    public PagoAlertaApp(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }

    public override void OnCreate()
    {
        base.OnCreate();
        Servicios.Iniciar(this);

        // HT-17: errores no controlados al registro (solo tipo y mensaje recortado).
        AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
        {
            Registro.Error("Error no controlado", e.Exception);
            e.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Registro.Error("Error fatal", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Registro.Error("Error en tarea de fondo", e.Exception);
            e.SetObserved();
        };
    }
}
