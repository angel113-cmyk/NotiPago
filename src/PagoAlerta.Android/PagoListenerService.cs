using Android.Service.Notification;
using PagoAlerta.Core.Logging;
using PagoAlerta.Core.Parsing;
using PagoAlerta.Core.Services;
using PagoAlerta.Movil.Infra;

namespace PagoAlerta.Movil;

/// <summary>
/// HT-07: lee las notificaciones de las apps elegidas (titulo, texto y texto expandido), las interpreta con el
/// parser del Core y envia los pagos a la nube sin abrir la app (HU-12, HU-13). HU-18: no duplica.
/// </summary>
[Service(Name = "com.pagoalerta.app.PagoListenerService", Label = "@string/app_name", Exported = true,
         Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE")]
[IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
public class PagoListenerService : NotificationListenerService
{
    public override void OnListenerConnected()
    {
        base.OnListenerConnected();
        Registro.Info("Lector de notificaciones conectado");
        if (Servicios.Prefs.MonitoreoActivo) MonitorService.Iniciar(this);
    }

    public override void OnListenerDisconnected()
    {
        base.OnListenerDisconnected();
        Registro.Aviso("Lector de notificaciones desconectado");
        try
        {
            // Android puede soltar el lector (memoria, ahorro de bateria): se pide reconectarlo.
            RequestRebind(new ComponentName(this, Java.Lang.Class.FromType(typeof(PagoListenerService))));
        }
        catch (Exception ex) { Registro.Error("No se pudo pedir la reconexión", ex); }
    }

    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        if (sbn?.Notification is null || sbn.PackageName is null) return;
        var paquete = sbn.PackageName;
        if (paquete == PackageName) return;                                   // nunca nuestras propias notificaciones

        var prefs = Servicios.Prefs;
        var monitoreada = prefs.PaquetesMonitoreados(Servicios.Parser).Contains(paquete);

        // Notificaciones agrupadas ("resumen"): el detalle llega en sus hijas.
        if ((sbn.Notification.Flags & NotificationFlags.GroupSummary) != 0)
        {
            return;
        }

        // Avisos permanentes de la app (servicio en primer plano, "en curso"): no son movimientos ni merecen "Revisar".
        if ((sbn.Notification.Flags & (NotificationFlags.OngoingEvent | NotificationFlags.ForegroundService)) != 0)
        {
            return;
        }

        // Se extraen datos ahora (el objeto puede reciclarse) y el trabajo pesado va a un hilo de fondo.
        var extras = sbn.Notification.Extras;
        var titulo = extras?.GetCharSequence(Notification.ExtraTitle)?.ToString()
                  ?? extras?.GetCharSequence(Notification.ExtraTitleBig)?.ToString();
        var candidatos = new List<string>();
        AgregarSiHay(candidatos, extras?.GetCharSequence(Notification.ExtraBigText)?.ToString());
        AgregarSiHay(candidatos, extras?.GetCharSequence(Notification.ExtraText)?.ToString());
        var lineas = extras?.GetCharSequenceArray(Notification.ExtraTextLines);
        if (lineas is { Length: > 0 })
            AgregarSiHay(candidatos, string.Join("\n", lineas.Select(l => l?.ToString() ?? "")));

        // Hora de la notificacion. 'When' no cambia cuando Android la actualiza, asi que sirve para no duplicar.
        var cuando = sbn.Notification.When > 0 ? sbn.Notification.When : sbn.PostTime;
        var fechaUtc = DateTimeOffset.FromUnixTimeMilliseconds(cuando).UtcDateTime;

        if (!monitoreada || !prefs.MonitoreoActivo)
        {
            Servicios.Registrar(new NotificacionReciente(paquete, DateTime.Now, monitoreada, false));
            return;
        }

        _ = Task.Run(() => ProcesarAsync(paquete, titulo, candidatos, fechaUtc));
    }

    private static void AgregarSiHay(List<string> lista, string? s)
    {
        if (!string.IsNullOrWhiteSpace(s) && !lista.Contains(s)) lista.Add(s);
    }

    private static async Task ProcesarAsync(string paquete, string? titulo, List<string> candidatos, DateTime fechaUtc)
    {
        try
        {
            // Una notificacion de BBVA sin regla NO se descarta: se guarda como "no reconocido" (etiqueta Revisar).
            var detectado = Servicios.Parser.ParseORevisar(paquete, titulo, candidatos, fechaUtc);

            Servicios.Registrar(new NotificacionReciente(paquete, DateTime.Now, true, detectado is not null));
            if (detectado is null) return;                                    // paquete sin "revisar" y sin regla: se ignora (HU-12)

            var pago = PagoFactory.DesdeDetectado(detectado);
            if (Servicios.YaEnviadoRecientemente(pago.Hash)) return;           // Android repitio la notificacion (HU-18)

            // HU-14: primero a la cola local (no se pierde nada).
            if (!Servicios.EncolarPago(pago)) return;               // ya estaba en la cola: no se avisa dos veces

            // Aviso en el celular (sonido + vibracion), sin esperar a la red; luego se intenta enviar.
            AvisoPago.Notificar(Servicios.Contexto, pago);
            await Servicios.ProcesarColaAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Servicios.Prefs.UltimoError = "Error procesando una notificación.";
            Registro.Error("Error procesando una notificación", ex);
        }
    }
}
