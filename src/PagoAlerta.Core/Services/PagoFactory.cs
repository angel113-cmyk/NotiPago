using System.Security.Cryptography;
using System.Text;
using PagoAlerta.Core.Models;
using PagoAlerta.Core.Parsing;

namespace PagoAlerta.Core.Services;

public static class PagoFactory
{
    public const string NombrePrueba = "Prueba Sistema";

    /// <summary>Paquete simulado para el mensaje de prueba (el de Yape, para que el parser lo reconozca).</summary>
    public const string PaquetePrueba = "com.bcp.innovacxion.yapeapp";

    /// <summary>Convierte una notificacion interpretada en la fila a guardar (hash anti-duplicados incluido).</summary>
    public static Pago DesdeDetectado(PagoDetectado d, bool esPrueba = false, string? nonce = null)
    {
        var hash = HashPago.Calcular(d.Paquete, d.TituloOriginal, d.TextoOriginal, d.FechaHoraUtc);
        if (nonce is not null) hash = Sha256($"{hash}|{nonce}");   // las pruebas no deben deduplicarse entre si
        return new Pago
        {
            App = d.App,
            Tipo = d.Tipo,
            Remitente = d.Nombre,
            Monto = d.Monto,
            Moneda = d.Moneda,
            FechaHora = d.FechaHoraUtc,
            TituloOriginal = d.TituloOriginal,
            TextoOriginal = d.TextoOriginal,
            Paquete = d.Paquete,
            EsPrueba = esPrueba,
            Hash = hash
        };
    }

    /// <summary>
    /// HT-22: pago de prueba que recorre el MISMO camino que uno real: notificacion simulada -> parser -> hash.
    /// Devuelve null si el parser no reconoce el texto simulado (las reglas estan mal configuradas).
    /// </summary>
    public static Pago? CrearPruebaPorParser(NotificationParser parser, DateTime? ahoraUtc = null)
    {
        var ahora = ahoraUtc ?? DateTime.UtcNow;
        var detectado = parser.Parse(
            PaquetePrueba,
            "Confirmación de pago",
            $"{NombrePrueba} te envió un pago por S/1.00. El cód. de seguridad es:000",
            ahora);
        return detectado is null ? null : DesdeDetectado(detectado, esPrueba: true, nonce: Guid.NewGuid().ToString("N"));
    }

    /// <summary>Prueba directa (sin parser). La usa el boton "Enviar prueba" de la PC.</summary>
    public static Pago CrearPrueba(string app = "Prueba", string? paquete = null)
    {
        var ahora = DateTime.UtcNow;
        var texto = $"{NombrePrueba} te envió un pago por S/1.00. (mensaje de prueba)";
        return new Pago
        {
            App = app,
            Tipo = TipoPago.Recibido,
            Remitente = NombrePrueba,
            Monto = 1.00m,
            Moneda = AppInfo.MonedaPorDefecto,
            FechaHora = ahora,
            TituloOriginal = "Mensaje de prueba",
            TextoOriginal = texto,
            Paquete = paquete,
            EsPrueba = true,
            Hash = Sha256($"prueba|{Guid.NewGuid():N}|{ahora:O}")
        };
    }

    public static string Sha256(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}
