using PagoAlerta.Core.Models;

namespace PagoAlerta.Core;

/// <summary>Texto de los avisos de pago (notificacion de Windows y del celular).</summary>
public static class Avisos
{
    /// <summary>Titulo y cuerpo: "Pago recibido" / "S/ 35.00 · María Quispe (Yape)".</summary>
    public static (string Titulo, string Cuerpo) DePago(Pago p)
    {
        var titulo = p.EsPrueba ? "Prueba: pago de ejemplo"
            : p.Tipo == TipoPago.Recibido ? "Pago recibido"
            : p.Tipo == TipoPago.Enviado ? "Pago enviado"
            : "Movimiento";

        var cuerpo = Formato.Monto(p.Monto, p.Moneda);
        if (!string.IsNullOrWhiteSpace(p.Remitente)) cuerpo += " · " + p.Remitente!.Trim();
        if (!string.IsNullOrWhiteSpace(p.App)) cuerpo += $" ({p.App!.Trim()})";
        return (titulo, cuerpo);
    }

    /// <summary>Solo los pagos recibidos (y las pruebas) generan aviso; un "cargo" no.</summary>
    public static bool DebeAvisar(Pago p) => p.Tipo == TipoPago.Recibido;
}
