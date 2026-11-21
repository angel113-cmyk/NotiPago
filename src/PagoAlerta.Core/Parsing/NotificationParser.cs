using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PagoAlerta.Core.Parsing;

/// <summary>
/// HT-06: parser por reglas configurables (JSON). Devuelve null si la notificacion no es un pago.
/// </summary>
public class NotificationParser
{
    private readonly List<(ReglaParser Regla, Regex Regex)> _reglas = new();

    public NotificationParser(IEnumerable<ReglaParser> reglas)
    {
        foreach (var r in reglas)
        {
            var regex = new Regex(r.Patron,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline,
                TimeSpan.FromMilliseconds(200));
            _reglas.Add((r, regex));
        }
    }

    public static NotificationParser DesdeJson(string json)
    {
        var set = JsonSerializer.Deserialize<ConjuntoReglas>(json, new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip })
                  ?? throw new InvalidDataException("Reglas del parser vacias o invalidas.");
        return new NotificationParser(set.Reglas);
    }

    public static NotificationParser DesdeArchivo(string ruta) => DesdeJson(File.ReadAllText(ruta));

    /// <summary>Reglas por defecto, incluidas como recurso embebido en Core.</summary>
    public static NotificationParser PorDefecto()
    {
        var asm = typeof(NotificationParser).Assembly;
        var nombre = asm.GetManifestResourceNames().Single(n => n.EndsWith("parser-rules.json", StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(nombre)!;
        using var reader = new StreamReader(stream);
        return DesdeJson(reader.ReadToEnd());
    }

    public PagoDetectado? Parse(string? paquete, string? titulo, string? texto, DateTime fechaHoraUtc)
    {
        if (string.IsNullOrWhiteSpace(texto) && string.IsNullOrWhiteSpace(titulo)) return null;
        var textoOriginal = texto ?? "";

        foreach (var (regla, regex) in _reglas)
        {
            if (regla.Paquetes.Count > 0 && (paquete is null ||
                !regla.Paquetes.Contains(paquete, StringComparer.OrdinalIgnoreCase)))
                continue;

            var entrada = regla.BuscarEn.Equals("ambos", StringComparison.OrdinalIgnoreCase)
                ? $"{titulo}\n{textoOriginal}"
                : textoOriginal;

            Match m;
            try { m = regex.Match(entrada); }
            catch (RegexMatchTimeoutException) { continue; }
            if (!m.Success) continue;

            if (!MontoParser.TryParse(m.Groups["monto"].Value, out var monto)) continue;

            var nombre = m.Groups["nombre"].Success ? m.Groups["nombre"].Value.Trim() : null;
            if (string.IsNullOrEmpty(nombre)) nombre = null;

            return new PagoDetectado(
                regla.App, regla.Tipo, nombre, monto,
                regla.Moneda ?? AppInfo.MonedaPorDefecto,
                fechaHoraUtc, titulo, textoOriginal, paquete);
        }
        return null;
    }
}
