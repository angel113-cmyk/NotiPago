using System.Text.Json.Serialization;

namespace PagoAlerta.Core.Parsing;

public class ReglaParser
{
    [JsonPropertyName("app")] public string App { get; set; } = "";
    /// <summary>Fila de "Apps a monitorear" a la que pertenece (Plin llega por la app de BBVA). Vacio = la propia app.</summary>
    [JsonPropertyName("grupo")] public string? Grupo { get; set; }
    [JsonPropertyName("paquetes")] public List<string> Paquetes { get; set; } = new();
    [JsonPropertyName("tipo")] public string Tipo { get; set; } = "otro";
    [JsonPropertyName("patron")] public string Patron { get; set; } = "";
    [JsonPropertyName("buscarEn")] public string BuscarEn { get; set; } = "texto";
    [JsonPropertyName("moneda")] public string? Moneda { get; set; }
    [JsonPropertyName("provisional")] public bool Provisional { get; set; }
}

public class ConjuntoReglas
{
    [JsonPropertyName("reglas")] public List<ReglaParser> Reglas { get; set; } = new();
    [JsonPropertyName("revisarSiNoCoincide")] public List<string> RevisarSiNoCoincide { get; set; } = new();
}

/// <summary>Resultado de interpretar una notificacion. El texto original siempre se conserva.</summary>
public record PagoDetectado(
    string App,
    string Tipo,
    string? Nombre,
    decimal Monto,
    string Moneda,
    DateTime FechaHoraUtc,
    string? TituloOriginal,
    string TextoOriginal,
    string? Paquete);
