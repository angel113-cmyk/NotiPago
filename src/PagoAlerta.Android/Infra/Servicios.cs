using System.Collections.Concurrent;
using Android.Provider;
using PagoAlerta.Core;
using PagoAlerta.Core.Cola;
using PagoAlerta.Core.Config;
using PagoAlerta.Core.Logging;
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

/// <summary>Contenedor unico de servicios compartidos por las pantallas, el servicio de notificaciones y el de primer plano.</summary>
public static class Servicios
{
    private static readonly SemaphoreSlim _sesionLock = new(1, 1);
    private static readonly ConcurrentQueue<NotificacionReciente> _recientes = new();
    private static readonly ConcurrentDictionary<string, DateTime> _enviados = new();

    public static Context Contexto { get; private set; } = null!;
    public static PagoService? Pago { get; private set; }
    public static NotificationParser Parser { get; private set; } = null!;
    public static PrefsApp Prefs { get; private set; } = null!;
    public static ProcesadorCola Cola { get; private set; } = null!;
    public static string? ErrorConfiguracion { get; private set; }

    public static int Pendientes => Cola.Pendientes;

    public static void Iniciar(Context ctx)
    {
        if (Contexto is not null) return;
        Contexto = ctx.ApplicationContext ?? ctx;
        Registro.Iniciar(Path.Combine(Contexto.FilesDir!.AbsolutePath, "logs"));
        Prefs = new PrefsApp(Contexto);
        Parser = NotificationParser.PorDefecto();
        Cola = new ProcesadorCola(new AlmacenColaSqlite(Contexto), EnviarDirectoAsync);
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
            Registro.Error("No se pudo cargar la configuración", ex);
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

    // ---------- Cola y envio ----------
    /// <summary>True si este hash ya se envio hace poco (evita repetir lo que Android re-publica).</summary>
    public static bool YaEnviadoRecientemente(string hash)
    {
        var limite = DateTime.UtcNow.AddMinutes(-30);
        foreach (var k in _enviados.Where(kv => kv.Value < limite).Select(kv => kv.Key).ToList())
            _enviados.TryRemove(k, out _);
        return _enviados.ContainsKey(hash);
    }

    /// <summary>Un solo intento de envio (lo usa la cola). La cola decide si reintenta.</summary>
    private static async Task<SalidaEnvio> EnviarDirectoAsync(Pago pago)
    {
        try
        {
            if (!HayInternet(Contexto)) { Prefs.UltimoError = "Sin conexión a internet."; return SalidaEnvio.SinRed; }
            if (!await AsegurarSesionAsync().ConfigureAwait(false))
            {
                Prefs.UltimoError = "La sesión venció. Inicia sesión de nuevo.";
                Registro.Aviso("Sesión no disponible: los pagos quedan en la cola");
                return SalidaEnvio.SesionVencida;
            }

            var r = await Pago!.InsertarAsync(pago).ConfigureAwait(false);
            _enviados[pago.Hash] = DateTime.UtcNow;
            if (r == ResultadoInsercion.Insertado)
            {
                Prefs.UltimoEnvioMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Prefs.UltimoError = null;
                Registro.Info($"Pago enviado ({pago.App}, {(pago.EsPrueba ? "prueba" : "real")}, hash {pago.Hash[..8]})");
                return SalidaEnvio.Enviado;
            }
            Registro.Info($"Pago ya existía en la nube (hash {pago.Hash[..8]})");
            return SalidaEnvio.YaExistia;
        }
        catch (Exception ex)
        {
            if (Reintentos.EsSesionVencida(ex))
            {
                Prefs.UltimoError = "La sesión venció. Inicia sesión de nuevo.";
                Registro.Error("Sesión vencida al enviar", ex);
                return SalidaEnvio.SesionVencida;
            }
            if (Reintentos.EsTransitorio(ex))
            {
                Prefs.UltimoError = "Sin conexión a internet.";
                Registro.Aviso("Sin conexión al enviar; se reintentará");
                return SalidaEnvio.SinRed;
            }
            Prefs.UltimoError = $"No se pudo enviar: {Registro.Recortar(ex.Message, 100)}";
            Registro.Error("Error al enviar un pago", ex);
            return SalidaEnvio.Error;
        }
    }

    /// <summary>Envia lo pendiente (en orden). <paramref name="volvioLaRed"/> ignora la espera entre reintentos.</summary>
    public static async Task<ResumenCola> ProcesarColaAsync(bool volvioLaRed = false)
    {
        var r = await Cola.ProcesarAsync(volvioLaRed).ConfigureAwait(false);
        if (r.Pendientes == 0 && r.Enviados > 0) Prefs.UltimoError = null;
        return r;
    }

    /// <summary>Guarda el pago en la cola local y lo intenta enviar enseguida. Nunca se pierde.</summary>
    public static async Task EncolarYEnviarAsync(Pago pago)
    {
        if (Cola.Encolar(pago)) Registro.Info($"Pago en cola (hash {pago.Hash[..8]})");
        await ProcesarColaAsync().ConfigureAwait(false);
    }

    /// <summary>HU-20 / HT-22: la prueba recorre el MISMO camino que un pago real (parser -> hash -> cola -> envio).</summary>
    public static async Task<EnvioInfo> EnviarPruebaAsync()
    {
        var pago = PagoFactory.CrearPruebaPorParser(Parser);
        if (pago is null)
            return new EnvioInfo(ResultadoEnvio.Error, "El parser no reconoce el mensaje de prueba (revisa parser-rules.json).");

        Cola.Encolar(pago);
        var r = await Cola.ProcesarAsync(ignorarEspera: true).ConfigureAwait(false);

        if (r.Pendientes == 0 || r.UltimaSalida is SalidaEnvio.Enviado or SalidaEnvio.YaExistia)
            return new EnvioInfo(ResultadoEnvio.Enviado, "Enviado.");

        return r.UltimaSalida switch
        {
            SalidaEnvio.SinRed => new EnvioInfo(ResultadoEnvio.SinInternet,
                "Sin conexión a internet. La prueba quedó en cola y se enviará cuando vuelva la red."),
            SalidaEnvio.SesionVencida => new EnvioInfo(ResultadoEnvio.SesionVencida,
                "La sesión venció. Inicia sesión de nuevo (la prueba quedó en cola)."),
            _ => new EnvioInfo(ResultadoEnvio.Error, Prefs.UltimoError ?? "No se pudo enviar la prueba.")
        };
    }
}
