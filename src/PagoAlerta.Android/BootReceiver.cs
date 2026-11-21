using Android.Service.Notification;
using PagoAlerta.Core.Logging;
using PagoAlerta.Movil.Infra;

namespace PagoAlerta.Movil;

/// <summary>HT-08 / HU-17: al reiniciar el celular (o actualizar la app) se reactiva el monitoreo sin abrir la app.</summary>
[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = false)]
[IntentFilter(new[] { Intent.ActionBootCompleted, "android.intent.action.QUICKBOOT_POWERON", Intent.ActionMyPackageReplaced })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;
        Servicios.Iniciar(context);
        Registro.Info($"Evento del sistema: {intent?.Action}");

        if (!Servicios.Prefs.MonitoreoActivo) return;

        MonitorService.Iniciar(context);

        // Pide a Android que vuelva a conectar el lector de notificaciones.
        try
        {
            NotificationListenerService.RequestRebind(
                new ComponentName(context, Java.Lang.Class.FromType(typeof(PagoListenerService))));
        }
        catch (Exception ex) { Registro.Error("No se pudo reconectar el lector de notificaciones", ex); }
    }
}
