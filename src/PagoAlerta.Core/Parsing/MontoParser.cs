using System.Globalization;
using System.Text.RegularExpressions;

namespace PagoAlerta.Core.Parsing;

/// <summary>Convierte "S/ 10", "S/10.00", "S/ 1,234.50", "$1,324.00" a decimal.</summary>
public static class MontoParser
{
    private static readonly Regex Numero = new(@"\d[\d.,]*", RegexOptions.Compiled);

    public static bool TryParse(string? texto, out decimal monto)
    {
        monto = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var m = Numero.Match(texto);
        if (!m.Success) return false;

        var s = m.Value.TrimEnd('.', ',');
        int punto = s.LastIndexOf('.'), coma = s.LastIndexOf(',');

        if (punto >= 0 && coma >= 0)
        {
            // El ultimo separador es el decimal; el otro es de miles.
            var decimalSep = punto > coma ? '.' : ',';
            var milesSep = decimalSep == '.' ? ',' : '.';
            s = s.Replace(milesSep.ToString(), "").Replace(decimalSep, '.');
        }
        else if (coma >= 0)
        {
            // "10,50" -> decimal; "1,234" o "1,234,567" -> miles.
            var tras = s.Length - coma - 1;
            s = (s.Count(c => c == ',') == 1 && tras is 1 or 2) ? s.Replace(',', '.') : s.Replace(",", "");
        }
        else if (punto >= 0 && s.Count(c => c == '.') > 1)
        {
            s = s.Replace(".", ""); // "1.234.567" -> miles
        }

        return decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out monto)
               && monto > 0;
    }
}
