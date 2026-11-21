using System.Text.Json;
using PagoAlerta.Core.Logging;
using PagoAlerta.Core.Models;

namespace PagoAlerta.Core.Cola;

/// <summary>Datos del pago que se guardan en la cola local (sin depender de las clases de Supabase).</summary>
public record PagoCola(
    string? App, string Tipo, string? Remitente, decimal Monto, string Moneda, DateTime FechaHora,
    string? TituloOriginal, string? TextoOriginal, string? Paquete, string Hash, bool EsPrueba)
{
    public static PagoCola Desde(Pago p) => new(p.App, p.Tipo, p.Remitente, p.Monto, p.Moneda, p.FechaHora,
        p.TituloOriginal, p.TextoOriginal, p.Paquete, p.Hash, p.EsPrueba);

    public Pago ToPago() => new()
    {
        App = App, Tipo = Tipo, Remitente = Remitente, Monto = Monto, Moneda = Moneda,
        FechaHora = DateTime.SpecifyKind(FechaHora, DateTimeKind.Utc),
        TituloOriginal = TituloOriginal, TextoOriginal = TextoOriginal, Paquete = Paquete,
        Hash = Hash, EsPrueba = EsPrueba
    };
}

public record ItemCola(long Id, string Hash, string Json, int Intentos, long ProximoIntentoMs, long CreadoMs);

/// <summary>Almacen persistente de la cola (SQLite en Android; en memoria en las pruebas).</summary>
public interface IAlmacenCola
{
    /// <summary>Guarda el pago. Devuelve false si ya habia uno con el mismo hash (no se duplica).</summary>
    bool Encolar(string hash, string json, long ahoraMs);

    /// <summary>El pago mas antiguo cuyo turno ya llego (o el mas antiguo sin mas si se ignora la espera).</summary>
    ItemCola? Siguiente(long ahoraMs, bool ignorarEspera);

    void Eliminar(long id);
    void Reprogramar(long id, int intentos, long proximoIntentoMs);
    int Contar();
}

public enum SalidaEnvio { Enviado, YaExistia, SinRed, SesionVencida, Error }

public record ResumenCola(int Enviados, int Pendientes, SalidaEnvio? UltimaSalida);

/// <summary>Espera creciente entre reintentos: 10 s, 20 s, 40 s ... hasta 15 minutos.</summary>
public static class Espera
{
    public static readonly TimeSpan Maxima = TimeSpan.FromMinutes(15);

    public static TimeSpan Calcular(int intentos)
    {
        var seg = 10.0 * Math.Pow(2, Math.Clamp(intentos, 0, 20));
        return TimeSpan.FromSeconds(Math.Min(seg, Maxima.TotalSeconds));
    }
}

/// <summary>
/// HT-09: envia en orden los pagos guardados. Sin internet no se pierde nada: quedan en la cola y se
/// reenvian al volver la red, sin duplicar (el hash es unico y el servidor responde "ya existe").
/// </summary>
public class ProcesadorCola
{
    private readonly IAlmacenCola _almacen;
    private readonly Func<Pago, Task<SalidaEnvio>> _enviar;
    private readonly Func<long> _reloj;
    private readonly SemaphoreSlim _uno = new(1, 1);

    public ProcesadorCola(IAlmacenCola almacen, Func<Pago, Task<SalidaEnvio>> enviar, Func<long>? reloj = null)
    {
        _almacen = almacen;
        _enviar = enviar;
        _reloj = reloj ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public int Pendientes => _almacen.Contar();

    /// <summary>Guarda el pago en la cola (siempre antes de intentar enviarlo).</summary>
    public bool Encolar(Pago pago) =>
        _almacen.Encolar(pago.Hash, JsonSerializer.Serialize(PagoCola.Desde(pago)), _reloj());

    /// <summary>Envia lo pendiente, del mas antiguo al mas nuevo. <paramref name="ignorarEspera"/> = volvio la red.</summary>
    public async Task<ResumenCola> ProcesarAsync(bool ignorarEspera = false, CancellationToken ct = default)
    {
        if (!await _uno.WaitAsync(0, ct).ConfigureAwait(false))
            return new ResumenCola(0, _almacen.Contar(), null);   // ya hay un envio en curso

        var enviados = 0;
        SalidaEnvio? ultima = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var item = _almacen.Siguiente(_reloj(), ignorarEspera);
                if (item is null) break;

                Pago pago;
                try { pago = JsonSerializer.Deserialize<PagoCola>(item.Json)!.ToPago(); }
                catch (Exception ex)
                {
                    Registro.Error("Elemento de la cola ilegible; se descarta", ex);
                    _almacen.Eliminar(item.Id);
                    continue;
                }

                SalidaEnvio salida;
                try { salida = await _enviar(pago).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    Registro.Error("Fallo inesperado al enviar", ex);
                    salida = SalidaEnvio.Error;
                }
                ultima = salida;

                switch (salida)
                {
                    case SalidaEnvio.Enviado:
                    case SalidaEnvio.YaExistia:
                        _almacen.Eliminar(item.Id);
                        enviados++;
                        break;

                    case SalidaEnvio.SinRed:
                        // Sin internet: se detiene todo (el orden se conserva) y se reintenta mas tarde.
                        Reprogramar(item);
                        return new ResumenCola(enviados, _almacen.Contar(), ultima);

                    case SalidaEnvio.SesionVencida:
                        // Hay que volver a iniciar sesion; no se pierde nada.
                        return new ResumenCola(enviados, _almacen.Contar(), ultima);

                    default:
                        Reprogramar(item);   // error del servidor: se prueba con el siguiente y se reintenta luego
                        break;
                }
            }
        }
        finally { _uno.Release(); }

        return new ResumenCola(enviados, _almacen.Contar(), ultima);
    }

    private void Reprogramar(ItemCola item)
    {
        var intentos = item.Intentos + 1;
        _almacen.Reprogramar(item.Id, intentos, _reloj() + (long)Espera.Calcular(intentos).TotalMilliseconds);
    }
}
