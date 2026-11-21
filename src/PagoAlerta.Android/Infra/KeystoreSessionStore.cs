using System.Text;
using System.Text.Json;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using PagoAlerta.Core.Services;

namespace PagoAlerta.Movil.Infra;

/// <summary>
/// HT-12 (Android): guarda SOLO los tokens de sesion cifrados con AES-GCM; la clave vive en el
/// Android Keystore (no sale del dispositivo). La contrasena nunca se guarda.
/// </summary>
public class KeystoreSessionStore : ISessionStore
{
    private const string Alias = "pagoalerta_sesion_v1";
    private const string Archivo = "sesion_segura";
    private readonly Context _ctx;

    public KeystoreSessionStore(Context ctx) => _ctx = ctx.ApplicationContext ?? ctx;

    private ISharedPreferences Prefs => _ctx.GetSharedPreferences(Archivo, FileCreationMode.Private)!;

    private static IKey ObtenerClave()
    {
        var ks = KeyStore.GetInstance("AndroidKeyStore")!;
        ks.Load(null);
        if (!ks.ContainsAlias(Alias))
        {
            var gen = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;
            var spec = new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeGcm)
                .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
                .SetKeySize(256)
                .Build();
            gen.Init(spec);
            gen.GenerateKey();
        }
        return ks.GetKey(Alias, null)!;
    }

    public Task GuardarAsync(SesionGuardada sesion)
    {
        try
        {
            var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            cipher.Init(CipherMode.EncryptMode, ObtenerClave());
            var iv = cipher.GetIV()!;
            var datos = cipher.DoFinal(JsonSerializer.SerializeToUtf8Bytes(sesion))!;
            Prefs.Edit()!
                .PutString("iv", Convert.ToBase64String(iv))!
                .PutString("datos", Convert.ToBase64String(datos))!
                .Apply();
        }
        catch
        {
            // Si el cifrado falla no se guarda nada (nunca en texto plano).
            Prefs.Edit()!.Clear()!.Apply();
        }
        return Task.CompletedTask;
    }

    public Task<SesionGuardada?> LeerAsync()
    {
        try
        {
            var iv64 = Prefs.GetString("iv", null);
            var datos64 = Prefs.GetString("datos", null);
            if (iv64 is null || datos64 is null) return Task.FromResult<SesionGuardada?>(null);

            var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            cipher.Init(CipherMode.DecryptMode, ObtenerClave(), new GCMParameterSpec(128, Convert.FromBase64String(iv64)));
            var plano = cipher.DoFinal(Convert.FromBase64String(datos64))!;
            return Task.FromResult(JsonSerializer.Deserialize<SesionGuardada>(Encoding.UTF8.GetString(plano)));
        }
        catch
        {
            Prefs.Edit()!.Clear()!.Apply();   // clave perdida o datos corruptos
            return Task.FromResult<SesionGuardada?>(null);
        }
    }

    public Task BorrarAsync()
    {
        Prefs.Edit()!.Clear()!.Apply();
        return Task.CompletedTask;
    }
}
