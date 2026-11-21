using Android.Media;
using PagoAlerta.Core;
using PagoAlerta.Core.Logging;
using PagoAlerta.Core.Models;

namespace PagoAlerta.Movil.Infra;

/// <summary>
/// Aviso en el celular cuando se detecta un pago: notificacion con sonido y vibracion
/// ("Pago recibido · S/ 35.00 · María Quispe (Yape)"). Se puede apagar desde la pestaña Cuenta.
/// </summary>
public static class AvisoPago
{
    // El sonido de un canal no se puede cambiar despues de crearlo: si cambia, se usa un id nuevo.
    private const string IdCanal = "pagos_v1";

    public static void Notificar(Context ctx, Pago pago)
    {
        try
        {
            if (!Servicios.Prefs.AvisoPago || !Avisos.DebeAvisar(pago)) return;

            var nm = (NotificationManager?)ctx.GetSystemService(Context.NotificationService);
            if (nm is null) return;
            CrearCanal(nm);

            var (titulo, cuerpo) = Avisos.DePago(pago);
            var abrir = new Intent(ctx, typeof(PrincipalActivity));
            abrir.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
            var pendiente = PendingIntent.GetActivity(ctx, 0, abrir, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var b = Build.VERSION.SdkInt >= BuildVersionCodes.O
                ? new Notification.Builder(ctx, IdCanal)
                : new Notification.Builder(ctx);

            b.SetContentTitle(titulo)
             .SetContentText(cuerpo)
             .SetStyle(new Notification.BigTextStyle().BigText(cuerpo))
             .SetSmallIcon(Resource.Drawable.ic_stat_campana)
             .SetColor(Android.Graphics.Color.ParseColor("#1F4FE0"))
             .SetContentIntent(pendiente)
             .SetAutoCancel(true)
             .SetCategory(Notification.CategoryMessage)
             .SetVisibility(NotificationVisibility.Public)
             .SetShowWhen(true);

            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
#pragma warning disable CA1422
                b.SetPriority((int)NotificationPriority.High);
                b.SetDefaults(NotificationDefaults.All);   // sonido + vibracion + luz
#pragma warning restore CA1422
            }

            // Un id distinto por pago para que no se reemplacen entre si.
            nm.Notify(2000 + (pago.Hash.GetHashCode() & 0x0FFFFFFF) % 100000, b.Build());
            Registro.Info("Aviso de pago mostrado");
        }
        catch (Exception ex) { Registro.Error("No se pudo mostrar el aviso de pago", ex); }
    }

    private static void CrearCanal(NotificationManager nm)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        if (nm.GetNotificationChannel(IdCanal) is not null) return;

        var canal = new NotificationChannel(IdCanal, "Avisos de pago", NotificationImportance.High)
        {
            Description = "Avisa con sonido y vibración cuando se detecta un pago."
        };
        var sonido = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
        var atributos = new AudioAttributes.Builder()!
            .SetUsage(AudioUsageKind.Notification)!
            .SetContentType(AudioContentType.Sonification)!
            .Build();
        canal.SetSound(sonido, atributos);
        canal.EnableVibration(true);
        canal.SetVibrationPattern(new long[] { 0, 250, 120, 250 });
        canal.EnableLights(true);
        canal.LightColor = Android.Graphics.Color.ParseColor("#1F4FE0");
        canal.LockscreenVisibility = NotificationVisibility.Public;
        nm.CreateNotificationChannel(canal);
    }
}
