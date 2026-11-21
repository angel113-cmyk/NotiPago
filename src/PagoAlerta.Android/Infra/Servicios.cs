using System.Collections.Concurrent;
using Android.Provider;
using PagoAlerta.Core;
using PagoAlerta.Core.Config;
using PagoAlerta.Core.Models;
using PagoAlerta.Core.Parsing;
using PagoAlerta.Core.Services;

namespace PagoAlerta.Movil.Infra;

public enum ResultadoEnvio { Enviado, YaExistia, SinInternet, SesionVencida, Error }

public record EnvioInfo(ResultadoEnvio Resultado, string Mensaje)
{
    public bool Ok => Resultado is ResultadoEnvio.Enviado or ResultadoEnvio.YaExistia;
}

/// <summary>Notificacion vista por el servicio (solo paquete y hora: sin contenido, por privacidad).</summary>
public record NotificacionReciente(string Paquete, DateTime HoraLocal, bool Monitoreada, bool EraPago);

/// <summary>Contenedor unico de servicios compartidos por las pantallas y el servicio de notificaciones.</summary>
public static class Servicios
{
    private static readonly SemaphoreSlim _sesionLock = new(1, 1);
    private static readonly ConcurrentQueue<NotificacionReciente> _recientes = new();
    private static readonly ConcurrentDictionary<string, DateTime> _enviados = new();

    public static Context Contexto { get; private set; } = null!;
    public static PagoService? Pago { get; private set; }
    public static NotificationParser Parser { get; private set; } = null!;
    public static PrefsApp Prefs { get; private set; } = null!;
    public static string? ErrorConfiguracion { get; private set; }

    public static void Iniciar(Context ctx)
    {
        if (Contexto is not null) return;
        Contexto = ctx.ApplicationContext ?? ctx;
        Prefs = new PrefsApp(Contexto);
        Parser = NotificationParser.PorDefecto();
        try
        {
            using var stream = Contexto.Assets!.Open("appsettings.json");
            using var reader = new StreamReader(stream);
            var settings = SupabaseSettings.DesdeJson(reader.ReadToEnd());
            Pago = new PagoService(settings, new KeystoreSessionStore(Contexto));
        }
        catch (Exception ex)
        {
            ErrorConfiguracion = $"Falta o es invalida la configuracion (appsettings.json): {ex.Message}";
        }
    }

    // ---------- Sesion ----------
    public static async Task<bool> AsegurarSesionAsync()
    {
        if (Pago is null) return false;
        if (Pago.TieneSesion) return true;
        await _sesionLock.WaitAsync().ConfigureAwait(false);
        try { return Pago.TieneSesion || await Pago.RestaurarSesionAsync().ConfigureAwait(false); }
        finally { _sesionLock.Release(); }
    }

    // ---------- Permisos y red ----------
    public static bool ListenerHabilitado(Context ctx)
    {
        var lista = Settings.Secure.GetString(ctx.ContentResolver, "enabled_notification_listeners");
        return !string.IsNullOrEmpty(lista) && lista.Contains(ctx.PackageName!, StringComparison.Ordinal);
    }

    public static bool IgnoraBateria(Context ctx) =>
        ((PowerManager?)ctx.GetSystemService(Context.PowerService))?.IsIgnoringBatteryOptimizations(ctx.PackageName) ?? false;

    public static bool HayInternet(Context ctx)
    {
        var cm = (ConnectivityManager?)ctx.GetSystemService(Context.ConnectivityService);
        var net = cm?.ActiveNetwork;
        var caps = net is null ? null : cm!.GetNetworkCapabilities(net);
        return caps?.HasCapability(NetCapability.Internet) ?? false;
    }

    // ---------- Registro de notificaciones vistas (para descubrir paquetes) ----------
    public static void Registrar(NotificacionReciente n)
    {
        _recientes.Enqueue(n);
        while (_recientes.Count > 40) _recientes.TryDequeue(out _);
    }

    public static IReadOnlyList<NotificacionReciente> Recientes() => _recientes.Reverse().ToList();

    // ---------- Envio ----------
    /// <summary>True si este hash ya se envio hace poco (evita repetir lo que Android re-publica).</summary>
    public static bool YaEnviadoRecientemente(string hash)
    {
        var limite = DateTime.UtcNow.AddMinutes(-30);
        foreach (var k in _enviados.Where(kv => kv.Value < limite).Select(kv => kv.Key).ToList())
            _enviados.TryRemove(k, out _);
        return _enviados.ContainsKey(hash);
    }

    public static async Task<EnvioInfo> EnviarAsync(Pago pago)
    {
        try
        {
            if (!HayInternet(Contexto)) return Falla(ResultadoEnvio.SinInternet, "Sin conexión a internet.");
            if (!await AsegurarSesionAsync().ConfigureAwait(false))
                return Falla(ResultadoEnvio.SesionVencida, "La sesión venció. Inicia sesión de nuevo.");

            var r = await Pago!.InsertarAsync(pago).ConfigureAwait(false);
            _enviados[pago.Hash] = DateTime.UtcNow;
            if (r == ResultadoInsercion.Insertado)
            {
                Prefs.UltimoEnvioMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Prefs.UltimoError = null;
                return new EnvioInfo(ResultadoEnvio.Enviado, "Enviado.");
            }
            return new EnvioInfo(ResultadoEnvio.YaExistia, "Ya estaba enviado.");
        }
        catch (Exception ex)
        {
            if (Reintentos.EsSesionVencida(ex)) return Falla(ResultadoEnvio.SesionVencida, "La sesión venció. Inicia sesión de nuevo.");
            if (Reintentos.EsTransitorio(ex)) return Falla(ResultadoEnvio.SinInternet, "Sin conexión a internet.");
            return Falla(ResultadoEnvio.Error, $"No se pudo enviar: {ex.Message}");
        }
    }

    private static EnvioInfo Falla(ResultadoEnvio r, string mensaje)
    {
        Prefs.UltimoError = mensaje;
        return new EnvioInfo(r, mensaje);
    }

    /// <summary>HU-20 / HT-22: la prueba recorre el mismo camino que un pago real (parser -> hash -> envio).</summary>
    public static async Task<EnvioInfo> EnviarPruebaAsync()
    {
        var pago = PagoFactory.CrearPruebaPorParser(Parser);
        if (pago is null)
            return new EnvioInfo(ResultadoEnvio.Error, "El parser no reconoce el mensaje de prueba (revisa parser-rules.json).");
        return await EnviarAsync(pago).ConfigureAwait(false);
    }
}
