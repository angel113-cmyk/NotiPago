using PagoAlerta.Core.Models;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace PagoAlerta.Core.Services;

/// <summary>
/// HT-11: avisa de pagos nuevos por Realtime (WebSocket) con reconexion automatica y, como respaldo,
/// una consulta periodica por created_at. Cada pago se notifica una sola vez.
/// </summary>
public sealed class PagoMonitor : IAsyncDisposable
{
    private readonly PagoService _servicio;
    private readonly HashSet<Guid> _vistos = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _bucleRealtime, _bucleRespaldo;
    private RealtimeChannel? _canal;
    private DateTime _ultimoCreadoUtc;
    private volatile bool _realtimeActivo;

    public TimeSpan IntervaloRespaldo { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan IntervaloRespaldoSinRealtime { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Se dispara desde un hilo de fondo: la UI debe marshalar al hilo principal.</summary>
    public event Action<Pago>? PagoNuevo;
    /// <summary>true = Realtime conectado; false = solo consulta periodica.</summary>
    public event Action<bool>? EstadoRealtimeCambio;

    public bool RealtimeActivo => _realtimeActivo;

    public PagoMonitor(PagoService servicio) => _servicio = servicio;

    /// <param name="desdeCreadoUtc">Solo se notifican pagos creados despues de este instante.</param>
    public void Iniciar(DateTime desdeCreadoUtc)
    {
        if (_cts is not null) return;
        _ultimoCreadoUtc = desdeCreadoUtc;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _bucleRealtime = Task.Run(() => BucleRealtimeAsync(ct), ct);
        _bucleRespaldo = Task.Run(() => BucleRespaldoAsync(ct), ct);
    }

    /// <summary>Registra pagos ya mostrados para no volver a avisarlos.</summary>
    public void MarcarVisto(Guid id) { lock (_lock) _vistos.Add(id); }

    private void Notificar(Pago p)
    {
        lock (_lock)
        {
            if (!_vistos.Add(p.Id)) return;
            if (p.CreatedAt > _ultimoCreadoUtc) _ultimoCreadoUtc = p.CreatedAt;
        }
        try { PagoNuevo?.Invoke(p); } catch { /* un suscriptor con error no debe tumbar el monitor */ }
    }

    private void CambiarEstado(bool activo)
    {
        if (_realtimeActivo == activo) return;
        _realtimeActivo = activo;
        try { EstadoRealtimeCambio?.Invoke(activo); } catch { }
    }

    // ---------- Realtime con reconexion ----------
    private async Task BucleRealtimeAsync(CancellationToken ct)
    {
        var espera = TimeSpan.FromSeconds(2);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConectarAsync(ct).ConfigureAwait(false);
                CambiarEstado(true);
                espera = TimeSpan.FromSeconds(2);

                // Se queda aqui mientras el socket siga vivo; si cae, se reconecta.
                var client = _servicio.Client;
                while (!ct.IsCancellationRequested && client.Realtime.Socket is { IsConnected: true })
                    await Task.Delay(2000, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch { /* se reintenta abajo */ }

            CambiarEstado(false);
            try { _canal?.Unsubscribe(); } catch { }
            _canal = null;
            try { await Task.Delay(espera, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            espera = TimeSpan.FromSeconds(Math.Min(espera.TotalSeconds * 2, 60));
        }
    }

    private async Task ConectarAsync(CancellationToken ct)
    {
        var client = _servicio.Client;
        await client.Realtime.ConnectAsync().ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        _canal = await client.From<Pago>().On(ListenType.Inserts, (_, cambio) =>
        {
            var pago = cambio.Model<Pago>();
            if (pago is not null) Notificar(pago);
        }).ConfigureAwait(false);
    }

    // ---------- Respaldo por consulta periodica ----------
    private async Task BucleRespaldoAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_realtimeActivo ? IntervaloRespaldo : IntervaloRespaldoSinRealtime, ct).ConfigureAwait(false);
                await ConsultarNuevosAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch { /* sin red / sesion: se reintenta en el siguiente ciclo */ }
        }
    }

    public async Task ConsultarNuevosAsync(CancellationToken ct = default)
    {
        DateTime desde;
        lock (_lock) desde = _ultimoCreadoUtc.AddSeconds(-2); // margen; _vistos evita duplicar
        var nuevos = await _servicio.ListarAsync(new FiltroPagos
        {
            CreadosDesdeUtc = desde, IncluirPruebas = true, Limite = 100
        }, ct).ConfigureAwait(false);

        foreach (var p in nuevos.OrderBy(p => p.CreatedAt)) Notificar(p);
    }

    public async ValueTask DisposeAsync()
    {
        var cts = _cts; _cts = null;
        if (cts is null) return;
        cts.Cancel();
        try { if (_bucleRealtime is not null) await _bucleRealtime.ConfigureAwait(false); } catch { }
        try { if (_bucleRespaldo is not null) await _bucleRespaldo.ConfigureAwait(false); } catch { }
        try { _canal?.Unsubscribe(); } catch { }
        try { _servicio.Client.Realtime.Disconnect(); } catch { }
        cts.Dispose();
    }
}
