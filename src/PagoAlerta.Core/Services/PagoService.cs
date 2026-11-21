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
public enum ResultadoRegistro
{
    Ok, RequiereConfirmacion, CorreoInvalido, ContrasenaDebil, CorreoYaRegistrado,
    RegistroDeshabilitado, SinConexion, Error
}

/// <summary>HT-05: cliente Supabase (login, insertar, listar, filtrar) con reintentos.</summary>
public class PagoService
{
    private readonly Supabase.Client _client;
    private readonly ISessionStore? _store;
    private bool _inicializado;
    private volatile bool _recordar;

    public PagoService(SupabaseSettings settings, ISessionStore? store = null)
    {
        settings.Validar();
        _store = store;
        _client = new Supabase.Client(settings.Url, settings.AnonKey, new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = false
        });

        // HT-12: cada vez que el token se renueva, se actualiza el almacen seguro (si el usuario eligio recordar).
        _client.Auth.AddStateChangedListener((remitente, estado) =>
        {
            if (estado == Supabase.Gotrue.Constants.AuthState.TokenRefreshed && _recordar && _store is not null)
            {
                var s = _client.Auth.CurrentSession;
                if (s?.AccessToken is not null && s.RefreshToken is not null)
                    _ = _store.GuardarAsync(new SesionGuardada(s.AccessToken, s.RefreshToken));
            }
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

            _recordar = recordar;
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

    /// <summary>
    /// Crea una cuenta nueva (el registro esta abierto por decision del dueno). Si Supabase no exige
    /// confirmar el correo, deja la sesion iniciada.
    /// </summary>
    public async Task<ResultadoRegistro> RegistrarAsync(string correo, string contrasena, bool recordar)
    {
        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@')) return ResultadoRegistro.CorreoInvalido;
        if (contrasena is null || contrasena.Length < 8) return ResultadoRegistro.ContrasenaDebil;
        try
        {
            await AsegurarInicioAsync().ConfigureAwait(false);
            var estado = await Reintentos.EjecutarAsync(() => _client.Auth.SignUpAsync(
                    Supabase.Gotrue.Constants.SignUpType.Email, correo.Trim(), contrasena))
                .ConfigureAwait(false);
            var sesion = estado?.Session;

            // Sin sesion = Supabase pide confirmar el correo antes de entrar.
            if (sesion?.AccessToken is null) return ResultadoRegistro.RequiereConfirmacion;

            _recordar = recordar;
            if (_store is not null)
            {
                if (recordar && sesion.RefreshToken is not null)
                    await _store.GuardarAsync(new SesionGuardada(sesion.AccessToken, sesion.RefreshToken)).ConfigureAwait(false);
                else
                    await _store.BorrarAsync().ConfigureAwait(false);
            }
            return ResultadoRegistro.Ok;
        }
        catch (GotrueException ex)
        {
            var m = ex.Message ?? "";
            if (m.Contains("already", StringComparison.OrdinalIgnoreCase) || m.Contains("registered", StringComparison.OrdinalIgnoreCase))
                return ResultadoRegistro.CorreoYaRegistrado;
            if (m.Contains("password", StringComparison.OrdinalIgnoreCase)) return ResultadoRegistro.ContrasenaDebil;
            if (m.Contains("signup", StringComparison.OrdinalIgnoreCase) && m.Contains("disabled", StringComparison.OrdinalIgnoreCase))
                return ResultadoRegistro.RegistroDeshabilitado;
            return ResultadoRegistro.Error;
        }
        catch (Exception ex) when (Reintentos.EsTransitorio(ex)) { return ResultadoRegistro.SinConexion; }
        catch { return ResultadoRegistro.Error; }
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
            _recordar = true;
            await _store.GuardarAsync(new SesionGuardada(sesion.AccessToken, sesion.RefreshToken)).ConfigureAwait(false);
            return true;
        }
        catch (GotrueException) { await _store.BorrarAsync().ConfigureAwait(false); return false; }
        catch { return false; }
    }

    public async Task CerrarSesionAsync()
    {
        try { await _client.Auth.SignOut().ConfigureAwait(false); } catch { /* sin red: igual se limpia lo local */ }
        _recordar = false;
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


    // ---------- Listado, resumen y borrado de pruebas ----------

    private Supabase.Postgrest.Interfaces.IPostgrestTable<Pago> Consulta(FiltroPagos f)
    {
        Supabase.Postgrest.Interfaces.IPostgrestTable<Pago> q = _client.From<Pago>();
        if (f.DesdeUtc is { } d) q = q.Filter("fecha_hora", Operator.GreaterThanOrEqual, d.ToString("o"));
        if (f.HastaUtc is { } h) q = q.Filter("fecha_hora", Operator.LessThan, h.ToString("o"));
        if (f.CreadosDesdeUtc is { } c) q = q.Filter("created_at", Operator.GreaterThan, c.ToString("o"));
        if (!string.IsNullOrWhiteSpace(f.App)) q = q.Filter("app", Operator.Equals, f.App!);
        if (!f.IncluirPruebas) q = q.Filter("es_prueba", Operator.Equals, "false");
        if (!string.IsNullOrWhiteSpace(f.Nombre))
            q = q.Filter("remitente", Operator.ILike, Texto.PatronIlike(f.Nombre!));
        return q;
    }

    /// <summary>Una pagina de pagos. <see cref="HayMas"/> se calcula antes del filtro exacto por nombre.</summary>
    public record PaginaPagos(List<Pago> Items, bool HayMas);

    /// <summary>Lista pagos (mas recientes primero) con filtros; busqueda por nombre sin tildes ni mayusculas.</summary>
    public async Task<PaginaPagos> ListarPaginaAsync(FiltroPagos? filtro = null, CancellationToken ct = default)
    {
        await AsegurarInicioAsync().ConfigureAwait(false);
        filtro ??= new FiltroPagos();

        var resp = await Reintentos.EjecutarAsync(() =>
            Consulta(filtro).Order("fecha_hora", Ordering.Descending)
                .Range(filtro.Desplazamiento, filtro.Desplazamiento + filtro.Limite - 1)
                .Get(ct), ct: ct).ConfigureAwait(false);

        var items = resp.Models;
        var hayMas = items.Count >= filtro.Limite;
        if (!string.IsNullOrWhiteSpace(filtro.Nombre))
            items = items.Where(p => Texto.Contiene(p.Remitente, filtro.Nombre)).ToList();
        return new PaginaPagos(items, hayMas);
    }

    public async Task<List<Pago>> ListarAsync(FiltroPagos? filtro = null, CancellationToken ct = default) =>
        (await ListarPaginaAsync(filtro, ct).ConfigureAwait(false)).Items;

    /// <summary>HU-05: total y cantidad del rango. Solo recibidos y sin pruebas.</summary>
    public async Task<ResumenPagos> ResumenAsync(FiltroPagos filtro, CancellationToken ct = default)
    {
        var f = filtro.Clonar();
        f.IncluirPruebas = false;   // las pruebas nunca suman
        f.Limite = 1000;
        decimal total = 0; var cantidad = 0;
        for (f.Desplazamiento = 0; ; f.Desplazamiento += f.Limite)
        {
            var pagina = await ListarPaginaAsync(f, ct).ConfigureAwait(false);
            foreach (var p in pagina.Items.Where(p => p.Tipo == TipoPago.Recibido && !p.EsPrueba))
            { total += p.Monto; cantidad++; }
            if (!pagina.HayMas) break;
        }
        return new ResumenPagos(total, cantidad);
    }

    /// <summary>HT-21: borra solo pagos de prueba (la base de datos tambien lo impone con RLS).</summary>
    public async Task BorrarPruebasAsync(CancellationToken ct = default)
    {
        await AsegurarInicioAsync().ConfigureAwait(false);
        await Reintentos.EjecutarAsync(() =>
            _client.From<Pago>().Filter("es_prueba", Operator.Equals, "true").Delete(cancellationToken: ct),
            ct: ct).ConfigureAwait(false);
    }
}


