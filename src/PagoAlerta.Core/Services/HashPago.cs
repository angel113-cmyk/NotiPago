using System.Security.Cryptography;
using System.Text;

namespace PagoAlerta.Core.Services;

/// <summary>
/// HT-10: hash anti-duplicados = SHA-256 de paquete + titulo + texto + hora al minuto (UTC).
/// La misma notificacion repetida o actualizada por Android produce el mismo hash.
/// </summary>
public static class HashPago
{
    public static string Calcular(string? paquete, string? titulo, string? texto, DateTime fechaHoraUtc)
    {
        var utc = fechaHoraUtc.Kind == DateTimeKind.Local ? fechaHoraUtc.ToUniversalTime() : fechaHoraUtc;
        var minuto = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc);
        var clave = string.Join('\u001f',
            (paquete ?? "").Trim(), (titulo ?? "").Trim(), Normalizar(texto), minuto.ToString("yyyyMMddHHmm"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clave))).ToLowerInvariant();
    }

    // Espacios repetidos no deben cambiar el hash.
    private static string Normalizar(string? s) =>
        string.Join(' ', (s ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
