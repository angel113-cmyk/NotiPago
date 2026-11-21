using PagoAlerta.Core.Parsing;

namespace PagoAlerta.Movil.Infra;

/// <summary>Preferencias locales no sensibles: monitoreo, apps elegidas, ultimo envio.</summary>
public class PrefsApp
{
    private readonly ISharedPreferences _p;

    public PrefsApp(Context ctx) =>
        _p = (ctx.ApplicationContext ?? ctx).GetSharedPreferences("preferencias", FileCreationMode.Private)!;

    public bool MonitoreoActivo
    {
        get => _p.GetBoolean("monitoreo", true);
        set => _p.Edit()!.PutBoolean("monitoreo", value)!.Apply();
    }

    /// <summary>Nombres de app marcados (Yape, Plin, BBVA). Por defecto, todos.</summary>
    public HashSet<string>? AppsMarcadas
    {
        get
        {
            var s = _p.GetStringSet("apps", null);
            return s is null ? null : new HashSet<string>(s, StringComparer.OrdinalIgnoreCase);
        }
        set => _p.Edit()!.PutStringSet("apps", value is null ? null : new List<string>(value))!.Apply();
    }

    public HashSet<string> PaquetesExtra
    {
        get => new(_p.GetStringSet("paquetes_extra", new List<string>())!, StringComparer.OrdinalIgnoreCase);
        set => _p.Edit()!.PutStringSet("paquetes_extra", new List<string>(value))!.Apply();
    }

    /// <summary>Notificacion con sonido y vibracion al detectar un pago. Encendida por defecto.</summary>
    public bool AvisoPago
    {
        get => _p.GetBoolean("aviso_pago", true);
        set => _p.Edit()!.PutBoolean("aviso_pago", value)!.Apply();
    }

    public long UltimoEnvioMs
    {
        get => _p.GetLong("ultimo_envio", 0);
        set => _p.Edit()!.PutLong("ultimo_envio", value)!.Apply();
    }

    public string? UltimoError
    {
        get => _p.GetString("ultimo_error", null);
        set => _p.Edit()!.PutString("ultimo_error", value)!.Apply();
    }

    public bool AppMarcada(string app) => AppsMarcadas?.Contains(app) ?? true;

    public void MarcarApp(string app, bool marcada, IEnumerable<string> todas)
    {
        var set = AppsMarcadas ?? new HashSet<string>(todas, StringComparer.OrdinalIgnoreCase);
        if (marcada) set.Add(app); else set.Remove(app);
        AppsMarcadas = set;
    }

    /// <summary>Paquetes que se vigilan: los de las apps marcadas mas los agregados a mano.</summary>
    public HashSet<string> PaquetesMonitoreados(NotificationParser parser)
    {
        var r = new HashSet<string>(PaquetesExtra, StringComparer.OrdinalIgnoreCase);
        foreach (var (app, paquetes) in parser.PaquetesPorApp)
            if (AppMarcada(app)) foreach (var pk in paquetes) r.Add(pk);
        return r;
    }
}
