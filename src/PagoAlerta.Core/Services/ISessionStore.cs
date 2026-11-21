namespace PagoAlerta.Core.Services;

/// <summary>Tokens de sesion (nunca la contrasena).</summary>
public record SesionGuardada(string AccessToken, string RefreshToken);

/// <summary>Cada plataforma lo implementa con su almacen seguro (DPAPI en Windows, Keystore en Android).</summary>
public interface ISessionStore
{
    Task<SesionGuardada?> LeerAsync();
    Task GuardarAsync(SesionGuardada sesion);
    Task BorrarAsync();
}
