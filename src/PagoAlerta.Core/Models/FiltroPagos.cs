namespace PagoAlerta.Core.Models;

/// <summary>Filtros para listar pagos. Las fechas son instantes UTC (se calculan desde Lima).</summary>
public class FiltroPagos
{
    public DateTime? DesdeUtc { get; set; }
    public DateTime? HastaUtc { get; set; }   // exclusivo
    public string? App { get; set; }
    public string? Nombre { get; set; }
    public int Limite { get; set; } = 50;
    public int Desplazamiento { get; set; } = 0;
}
