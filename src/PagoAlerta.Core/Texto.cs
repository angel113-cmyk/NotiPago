using System.Globalization;
using System.Text;

namespace PagoAlerta.Core;

/// <summary>Utilidades de texto para busquedas sin mayusculas ni tildes.</summary>
public static class Texto
{
    /// <summary>"Pérez Ñandú" -> "perez nandu".</summary>
    public static string Normalizar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var d = s.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    public static bool Contiene(string? texto, string? busqueda)
    {
        var b = Normalizar(busqueda);
        return b.Length == 0 || Normalizar(texto).Contains(b, StringComparison.Ordinal);
    }

    /// <summary>
    /// Patron ILIKE para el servidor que tolera tildes: las letras que pueden llevar tilde
    /// (a e i o u n) pasan a comodin '_'. El filtro exacto se hace luego en el cliente.
    /// </summary>
    public static string PatronIlike(string busqueda)
    {
        var sb = new StringBuilder("%");
        foreach (var c in Normalizar(busqueda))
        {
            sb.Append(c is 'a' or 'e' or 'i' or 'o' or 'u' or 'n' ? '_' : c is '%' or '_' or '\\' ? '_' : c);
        }
        return sb.Append('%').ToString();
    }
}
