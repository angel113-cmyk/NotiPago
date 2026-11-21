using System.Globalization;
using PagoAlerta.Core.Time;

namespace PagoAlerta.Core;

/// <summary>Formato de presentacion: S/ 1,234.50 y fechas en hora de Lima.</summary>
public static class Formato
{
    private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

    public static string Monto(decimal monto, string? moneda = null)
    {
        var simbolo = string.IsNullOrEmpty(moneda) || moneda == "PEN" ? AppInfo.SimboloMoneda : moneda;
        return $"{simbolo} {monto.ToString("N2", Cultura)}";
    }

    public static string Fecha(DateTime utc) => ZonaLima.DeUtc(utc).ToString("dd/MM/yyyy", Cultura);
    /// <summary>Hora de Lima en formato 24 h (13:15). Las pantallas usan este; la exportacion conserva el de 12 h.</summary>
    public static string Hora24(DateTime utc) => ZonaLima.DeUtc(utc).ToString("HH:mm", Cultura);

    public static string Hora(DateTime utc) => ZonaLima.DeUtc(utc).ToString("hh:mm tt", Cultura);

    public static string Iniciales(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "?";
        var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var letras = partes.Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpperInvariant(p[0]));
        var r = string.Concat(letras);
        return r.Length == 0 ? "?" : r;
    }
}
