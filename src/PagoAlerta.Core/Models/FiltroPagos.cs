namespace PagoAlerta.Core.Models;

/// <summary>Filtros para listar pagos. Las fechas son instantes UTC (se calculan desde Lima).</summary>
public class FiltroPagos
{
    public DateTime? DesdeUtc { get; set; }
    public DateTime? HastaUtc { get; set; }   // exclusivo
    public DateTime? CreadosDesdeUtc { get; set; } // para el respaldo por consulta periodica
    public string? App { get; set; }
    public string? Nombre { get; set; }
    /// <summary>HT-20: por defecto los pagos de prueba NO se incluyen.</summary>
    public bool IncluirPruebas { get; set; }
    public int Limite { get; set; } = 50;
    public int Desplazamiento { get; set; } = 0;

    public FiltroPagos Clonar() => (FiltroPagos)MemberwiseClone();
}

/// <summary>HU-05: total y cantidad del rango. Solo cuenta pagos recibidos y nunca pruebas.</summary>
public record ResumenPagos(decimal Total, int Cantidad)
{
    public static readonly ResumenPagos Vacio = new(0m, 0);
}
