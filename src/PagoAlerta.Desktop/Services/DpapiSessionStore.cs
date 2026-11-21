using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PagoAlerta.Core;
using PagoAlerta.Core.Services;

namespace PagoAlerta.Desktop.Services;

/// <summary>
/// Guarda SOLO los tokens de sesion cifrados con DPAPI (ligado al usuario de Windows).
/// La contrasena nunca se guarda.
/// </summary>
public class DpapiSessionStore : ISessionStore
{
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes(AppInfo.Nombre + ".sesion.v1");
    private readonly string _ruta;

    public DpapiSessionStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Nombre);
        Directory.CreateDirectory(dir);
        _ruta = Path.Combine(dir, "sesion.bin");
    }

    public Task<SesionGuardada?> LeerAsync()
    {
        try
        {
            if (!File.Exists(_ruta)) return Task.FromResult<SesionGuardada?>(null);
            var plano = ProtectedData.Unprotect(File.ReadAllBytes(_ruta), Entropia, DataProtectionScope.CurrentUser);
            return Task.FromResult(JsonSerializer.Deserialize<SesionGuardada>(plano));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException)
        {
            // Archivo corrupto o de otro usuario: se descarta.
            try { File.Delete(_ruta); } catch { }
            return Task.FromResult<SesionGuardada?>(null);
        }
    }

    public Task GuardarAsync(SesionGuardada sesion)
    {
        var plano = JsonSerializer.SerializeToUtf8Bytes(sesion);
        File.WriteAllBytes(_ruta, ProtectedData.Protect(plano, Entropia, DataProtectionScope.CurrentUser));
        return Task.CompletedTask;
    }

    public Task BorrarAsync()
    {
        try { if (File.Exists(_ruta)) File.Delete(_ruta); } catch { }
        return Task.CompletedTask;
    }
}
