using System.IO.Compression;
using System.Globalization;
using System.Security;
using System.Text;
using PagoAlerta.Core.Models;

namespace PagoAlerta.Core.Exportacion;

/// <summary>
/// HU-07: columnas Fecha, Hora, Nombre, Monto y App. Los pagos de prueba NUNCA se exportan.
/// Fecha y hora en America/Lima.
/// </summary>
public static class Exportadores
{
    public static readonly string[] Columnas = { "Fecha", "Hora", "Nombre", "Monto", "App" };

    private static IEnumerable<string[]> Filas(IEnumerable<Pago> pagos) =>
        pagos.Where(p => !p.EsPrueba).Select(p => new[]
        {
            Formato.Fecha(p.FechaHora),
            Formato.Hora(p.FechaHora),
            p.Remitente ?? "",
            p.Monto.ToString("0.00", CultureInfo.InvariantCulture),
            p.App ?? ""
        });

    // ---------------- CSV ----------------
    /// <summary>CSV en UTF-8 con BOM (Excel muestra bien las tildes).</summary>
    public static void EscribirCsv(IEnumerable<Pago> pagos, Stream destino)
    {
        using var w = new StreamWriter(destino, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        w.NewLine = "\r\n";
        w.WriteLine(string.Join(',', Columnas));
        foreach (var fila in Filas(pagos))
            w.WriteLine(string.Join(',', fila.Select(CampoCsv)));
    }

    internal static string CampoCsv(string valor)
    {
        valor = NeutralizarFormula(valor);
        return valor.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? "\"" + valor.Replace("\"", "\"\"") + "\""
            : valor;
    }

    /// <summary>Evita que un nombre como "=1+1" se ejecute como formula al abrirlo en Excel.</summary>
    internal static string NeutralizarFormula(string valor) =>
        valor.Length > 0 && valor[0] is '=' or '+' or '-' or '@' && !decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
            ? "'" + valor
            : valor;

    // ---------------- XLSX (minimo, sin librerias) ----------------
    public static void EscribirXlsx(IEnumerable<Pago> pagos, Stream destino)
    {
        using var zip = new ZipArchive(destino, ZipArchiveMode.Create, leaveOpen: true);
        Agregar(zip, "[Content_Types].xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
        Agregar(zip, "_rels/.rels",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
        Agregar(zip, "xl/workbook.xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Pagos" sheetId="1" r:id="rId1"/></sheets></workbook>""");
        Agregar(zip, "xl/_rels/workbook.xml.rels",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
        // estilo 1 = encabezado en negrita; estilo 2 = monto con 2 decimales
        Agregar(zip, "xl/styles.xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="1"><numFmt numFmtId="164" formatCode="#,##0.00"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="3"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs></styleSheet>""");

        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><cols><col min="1" max="2" width="14" customWidth="1"/><col min="3" max="3" width="34" customWidth="1"/><col min="4" max="4" width="14" customWidth="1"/><col min="5" max="5" width="14" customWidth="1"/></cols><sheetData>""");

        sb.Append("<row r=\"1\">");
        for (var c = 0; c < Columnas.Length; c++)
            sb.Append($"<c r=\"{(char)('A' + c)}1\" s=\"1\" t=\"inlineStr\"><is><t>{Esc(Columnas[c])}</t></is></c>");
        sb.Append("</row>");

        var r = 2;
        foreach (var f in Filas(pagos))
        {
            sb.Append($"<row r=\"{r}\">");
            sb.Append(CeldaTexto('A', r, f[0]));
            sb.Append(CeldaTexto('B', r, f[1]));
            sb.Append(CeldaTexto('C', r, NeutralizarFormula(f[2])));
            sb.Append($"<c r=\"D{r}\" s=\"2\"><v>{f[3]}</v></c>");   // numero real, ya en formato invariante
            sb.Append(CeldaTexto('E', r, f[4]));
            sb.Append("</row>");
            r++;
        }
        sb.Append("</sheetData></worksheet>");
        Agregar(zip, "xl/worksheets/sheet1.xml", sb.ToString());
    }

    private static string CeldaTexto(char col, int fila, string texto) =>
        $"<c r=\"{col}{fila}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(texto)}</t></is></c>";

    private static string Esc(string s) => SecurityElement.Escape(s) ?? "";

    private static void Agregar(ZipArchive zip, string ruta, string contenido)
    {
        var e = zip.CreateEntry(ruta, CompressionLevel.Optimal);
        using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
        w.Write(contenido);
    }
}
