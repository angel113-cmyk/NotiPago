using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace PagoAlerta.Core.Models;

public static class TipoPago
{
    public const string Recibido = "recibido";
    public const string Enviado = "enviado";
    public const string Otro = "otro";
}

/// <summary>Fila de la tabla <c>pagos</c>. fecha_hora siempre en UTC.</summary>
[Table("pagos")]
public class Pago : BaseModel
{
    // id, user_id y created_at los asigna la base de datos (user_id = auth.uid()).
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }

    [Column("user_id", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public Guid UserId { get; set; }

    [Column("app")] public string? App { get; set; }
    [Column("tipo")] public string Tipo { get; set; } = TipoPago.Otro;
    [Column("remitente")] public string? Remitente { get; set; }
    [Column("monto")] public decimal Monto { get; set; }
    [Column("moneda")] public string Moneda { get; set; } = AppInfo.MonedaPorDefecto;
    [Column("fecha_hora")] public DateTime FechaHora { get; set; }
    [Column("titulo_original")] public string? TituloOriginal { get; set; }
    [Column("texto_original")] public string? TextoOriginal { get; set; }
    [Column("paquete")] public string? Paquete { get; set; }
    [Column("hash")] public string Hash { get; set; } = "";
    [Column("es_prueba")] public bool EsPrueba { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }
}


