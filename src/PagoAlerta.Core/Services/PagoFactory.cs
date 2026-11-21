using System.Security.Cryptography;
using System.Text;
using PagoAlerta.Core.Models;

namespace PagoAlerta.Core.Services;

public static class PagoFactory
{
    public const string NombrePrueba = "Prueba Sistema";

    /// <summary>HU-20: pago simulado de S/ 1.00 marcado como prueba. El hash es unico en cada llamada.</summary>
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
