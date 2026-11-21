using PagoAlerta.Core.Logging;
using PagoAlerta.Movil.Infra;

namespace PagoAlerta.Movil;

/// <summary>
/// HT-08 / HU-17: servicio en primer plano con notificacion persistente. Mantiene viva la app en segundo plano
/// y reenvia lo pendiente de la cola cada 30 s y apenas vuelve la red (HU-14).
/// </summary>
[Service(Name = "com.pagoalerta.app.MonitorService", Exported = false, ForegroundServiceType = ForegroundService.TypeSpecialUse)]
public class MonitorService : Service
{
    private const int IdNotificacion = 1001;
    private const string IdCanal = "monitoreo";
    private CancellationTokenSource? _cts;
    private ConnectivityManager.NetworkCallback? _callbackRed;
    private int _ultimoPendiente = -1;

    public static void Iniciar(Context ctx)
    {
        try
        {
            var i = new Intent(ctx, typeof(MonitorService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O) ctx.StartForegroundService(i); else ctx.StartService(i);
        }
        catch (Exception ex)
        {
            // Android 12+ puede negar el inicio si la app esta en segundo plano; se reintenta al abrir la app.
            Registro.Error("No se pudo iniciar el servicio en primer plano", ex);
        }
    }

    public static void Detener(Context ctx)
    {
        try { ctx.StopService(new Intent(ctx, typeof(MonitorService))); } catch { }
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CrearCanal();
        var notif = Construir(Servicios.Pendientes);
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                StartForeground(IdNotificacion, notif, ForegroundService.TypeSpecialUse);
            else
                StartForeground(IdNotificacion, notif);
        }
        catch (Exception ex)
        {
            Registro.Error("StartForeground fallo", ex);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        if (_cts is null)
        {
            _cts = new CancellationTokenSource();
            RegistrarCallbackRed();
            var ct = _cts.Token;
            _ = Task.Run(() => BucleAsync(ct));
            Registro.Info("Servicio de monitoreo iniciado");
        }
        return StartCommandResult.Sticky;   // Android lo reinicia si lo cierra por memoria
    }

    public override void OnDestroy()
    {
        _cts?.Cancel();
        _cts = null;
        try { if (_callbackRed is not null) ((ConnectivityManager?)GetSystemService(ConnectivityService))?.UnregisterNetworkCallback(_callbackRed); } catch { }
        Registro.Info("Servicio de monitoreo detenido");
        base.OnDestroy();
    }

    private async Task BucleAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var r = await Servicios.ProcesarColaAsync();
                ActualizarNotificacion(r.Pendientes);
            }
            catch (Exception ex) { Registro.Error("Fallo al procesar la cola", ex); }

            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (System.OperationCanceledException) { break; }
        }
    }

    /// <summary>HU-14: al volver la red se reenvia todo enseguida, ignorando la espera de los reintentos.</summary>
    private void RegistrarCallbackRed()
    {
        try
        {
            var cm = (ConnectivityManager?)GetSystemService(ConnectivityService);
            _callbackRed = new RedCallback();
            cm?.RegisterDefaultNetworkCallback(_callbackRed);
        }
        catch (Exception ex) { Registro.Error("No se pudo escuchar los cambios de red", ex); }
    }

    private sealed class RedCallback : ConnectivityManager.NetworkCallback
    {
        public override void OnAvailable(Network network)
        {
            Registro.Info("Red disponible: reenviando pendientes");
            _ = Task.Run(async () =>
            {
                try { await Servicios.ProcesarColaAsync(volvioLaRed: true); }
                catch (Exception ex) { Registro.Error("Fallo al reenviar al volver la red", ex); }
            });
        }
    }

    // ---------- Notificacion persistente ----------
    private void CrearCanal()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        var nm = (NotificationManager?)GetSystemService(NotificationService);
        if (nm?.GetNotificationChannel(IdCanal) is not null) return;
        var canal = new NotificationChannel(IdCanal, "Monitoreo de pagos", NotificationImportance.Low)
        {
            Description = "Mantiene PagoAlerta activo para detectar tus pagos."
        };
        canal.SetShowBadge(false);
        nm?.CreateNotificationChannel(canal);
    }

    private Notification Construir(int pendientes)
    {
        var abrir = PendingIntent.GetActivity(this, 0, new Intent(this, typeof(PrincipalActivity)),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var b = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, IdCanal)
            : new Notification.Builder(this);

        b.SetContentTitle("PagoAlerta está monitoreando tus pagos")
         .SetContentText(pendientes > 0 ? $"Pagos pendientes de enviar: {pendientes}" : "Todo al día")
         .SetSmallIcon(Resource.Drawable.ic_stat_campana)
         .SetOngoing(true)
         .SetContentIntent(abrir);
        return b.Build()!;
    }

    private void ActualizarNotificacion(int pendientes)
    {
        if (pendientes == _ultimoPendiente) return;
        _ultimoPendiente = pendientes;
        try { ((NotificationManager?)GetSystemService(NotificationService))?.Notify(IdNotificacion, Construir(pendientes)); }
        catch { }
    }
}
