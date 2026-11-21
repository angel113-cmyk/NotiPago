using System.Text;
using System.Text.Json;

namespace PagoAlerta.Core.Config;

/// <summary>HT-13: URL y clave anon/publishable desde archivo; nunca la service_role.</summary>
public class SupabaseSettings
{
    public string Url { get; set; } = "";
    public string AnonKey { get; set; } = "";

    public static SupabaseSettings Cargar(string rutaJson)
    {
        if (!File.Exists(rutaJson))
            throw new FileNotFoundException(
                $"No se encontro '{Path.GetFileName(rutaJson)}'. Copia appsettings.example.json como appsettings.json y completa la URL y la clave anon.",
                rutaJson);

        using var doc = JsonDocument.Parse(File.ReadAllText(rutaJson));
        var sec = doc.RootElement.GetProperty("Supabase");
        var s = new SupabaseSettings
        {
            Url = sec.GetProperty("Url").GetString()?.Trim().TrimEnd('/') ?? "",
            AnonKey = sec.GetProperty("AnonKey").GetString()?.Trim() ?? ""
        };
        s.Validar();
        return s;
    }

    public void Validar()
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Supabase:Url invalida (debe ser https://...).");
        if (string.IsNullOrWhiteSpace(AnonKey) || AnonKey.Contains("TU_CLAVE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Supabase:AnonKey no configurada.");
        if (EsClaveSecreta(AnonKey))
            throw new InvalidOperationException(
                "La clave configurada es secreta/service_role. Usa SOLO la clave anon/publishable.");
    }

    /// <summary>Detecta claves service_role (JWT con role=service_role) o 'sb_secret_'.</summary>
    public static bool EsClaveSecreta(string clave)
    {
        if (clave.StartsWith("sb_secret_", StringComparison.Ordinal)) return true;
        var partes = clave.Split('.');
        if (partes.Length != 3) return false;
        try
        {
            var b64 = partes[1].Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b64)));
            return doc.RootElement.TryGetProperty("role", out var r) && r.GetString() == "service_role";
        }
        catch { return false; }
    }
}
