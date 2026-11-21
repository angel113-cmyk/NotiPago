using PagoAlerta.Core.Config;
using PagoAlerta.Core.Models;
using Supabase.Postgrest;
using Supabase.Postgrest.Exceptions;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using static Supabase.Postgrest.Constants;

namespace PagoAlerta.Core.Services;

public enum ResultadoLogin { Ok, CredencialesInvalidas, SinConexion, Error }
public enum ResultadoInsercion { Insertado, YaExistia }

/// <summary>HT-05: cliente Supabase (login, insertar, listar, filtrar) con reintentos.</summary>
public class PagoService
{
    private readonly Supabase.Client _client;
    private readonly ISessionStore? _store;
    private bool _inicializado;

    public PagoService(SupabaseSettings settings, ISessionStore? store = null)
    {
        settings.Validar();
        _store = store;
        _client = new Supabase.Client(settings.Url, settings.AnonKey, new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = false
        });
    }

    public Supabase.Client Client => _client;
    public bool TieneSesion => _client.Auth.CurrentSession?.AccessToken is not null;
    public string? CorreoActual => _client.Auth.CurrentUser?.Email;

    private async Task AsegurarInicioAsync()
    {
        if (_inicializado) return;
        await _client.InitializeAsync().ConfigureAwait(false);
        _inicializado = true;
    }

    /// <summary>Login con correo y contrasena. Si <paramref name="recordar"/>, guarda solo los tokens.</summary>
    public async Task<ResultadoLogin> LoginAsync(string correo, string contrasena, bool recordar)
    {
        try
        {
            await AsegurarInicioAsync().ConfigureAwait(false);
            var sesion = await Reintentos.EjecutarAsync(() => _client.Auth.SignIn(correo.Trim(), contrasena))
                .ConfigureAwait(false);
            if (sesion?.AccessToken is null) return ResultadoLogin.CredencialesInvalidas;

            if (_store is not null)
            {
                if (recordar && sesion.RefreshToken is not null)
                    await _store.GuardarAsync(new SesionGuardada(sesion.AccessToken, sesion.RefreshToken)).ConfigureAwait(false);
                else
                    await _store.BorrarAsync().ConfigureAwait(false);
            }
            return ResultadoLogin.Ok;
        }
        catch (GotrueException) { return ResultadoLogin.CredencialesInvalidas; }
        catch (Exception ex) when (Reintentos.EsTransitorio(ex)) { return ResultadoLogin.SinConexion; }
        catch { return ResultadoLogin.Error; }
    }

    /// <summary>Restaura la sesion guardada (si existe y sigue siendo valida).</summary>
    public async Task<bool> RestaurarSesionAsync()
    {
        if (_store is null) return false;
        try
        {
            var g = await _store.LeerAsync().ConfigureAwait(false);
            if (g is null) return false;
            await AsegurarInicioAsync().ConfigureAwait(false);
            var sesion = await _client.Auth.SetSession(g.AccessToken, g.RefreshToken).ConfigureAwait(false);
            if (sesion?.AccessToken is null || sesion.RefreshToken is null) { await _store.BorrarAsync(); return false; }
            await _store.GuardarAsync(new SesionGuardada(sesion.AccessToken, sesion.RefreshToken)).ConfigureAwait(false);
            return true;
        }
        catch (GotrueException) { await _store.BorrarAsync().ConfigureAwait(false); return false; }
        catch { return false; }
    }

    public async Task CerrarSesionAsync()
    {
        try { await _client.Auth.SignOut().ConfigureAwait(false); } catch { /* sin red: igual se limpia lo local */ }
        if (_store is not null) await _store.BorrarAsync().ConfigureAwait(false);
    }

    /// <summary>Inserta un pago. Si el hash ya existe (UNIQUE), se considera "ya enviado".</summary>
    public async Task<ResultadoInsercion> InsertarAsync(Pago pago, CancellationToken ct = default)
    {
        await AsegurarInicioAsync().ConfigureAwait(false);
        try
        {
            await Reintentos.EjecutarAsync(
                () => _client.From<Pago>().Insert(pago), ct: ct).ConfigureAwait(false);
            return ResultadoInsercion.Insertado;
        }
        catch (PostgrestException ex) when (
            ex.Message.Contains("23505") || ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoInsercion.YaExistia;
        }
    }

    /// <summary>Lista pagos (mas recientes primero) aplicando filtros y paginacion.</summary>
    public async Task<List<Pago>> ListarAsync(FiltroPagos? filtro = null, CancellationToken ct = default)
    {
        await AsegurarInicioAsync().ConfigureAwait(false);
        filtro ??= new FiltroPagos();

        var resp = await Reintentos.EjecutarAsync(() =>
        {
            Supabase.Postgrest.Interfaces.IPostgrestTable<Pago> q = _client.From<Pago>();
            if (filtro.DesdeUtc is { } d) q = q.Filter("fecha_hora", Operator.GreaterThanOrEqual, d.ToString("o"));
            if (filtro.HastaUtc is { } h) q = q.Filter("fecha_hora", Operator.LessThan, h.ToString("o"));
            if (!string.IsNullOrWhiteSpace(filtro.App)) q = q.Filter("app", Operator.Equals, filtro.App!);
            if (!string.IsNullOrWhiteSpace(filtro.Nombre))
                q = q.Filter("remitente", Operator.ILike, $"%{filtro.Nombre!.Trim()}%");
            return q.Order("fecha_hora", Ordering.Descending)
                    .Range(filtro.Desplazamiento, filtro.Desplazamiento + filtro.Limite - 1)
                    .Get(ct);
        }, ct: ct).ConfigureAwait(false);

        return resp.Models;
    }
}


