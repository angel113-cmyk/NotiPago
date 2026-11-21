using System.IO.Compression;
using System.Text;
using PagoAlerta.Core.Exportacion;
using PagoAlerta.Core.Logging;
using PagoAlerta.Core.Models;

namespace PagoAlerta.Core.Tests;

public class ExportacionYLogTests
{
    private static Pago P(string nombre, decimal monto, string app = "Yape", bool prueba = false, string tipo = TipoPago.Recibido) => new()
    {
        Remitente = nombre, Monto = monto, App = app, EsPrueba = prueba, Tipo = tipo,
        // 21/11/2026 21:43 UTC = 16:43 en Lima
        FechaHora = new DateTime(2026, 11, 21, 21, 43, 0, DateTimeKind.Utc), Hash = Guid.NewGuid().ToString()
    };

    private static string Csv(params Pago[] pagos)
    {
        using var ms = new MemoryStream();
        Exportadores.EscribirCsv(pagos, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact]
    public void Csv_tiene_las_columnas_pedidas_y_hora_de_lima()
    {
        var texto = Csv(P("Amanda Rod*", 25m));
        var lineas = texto.TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Fecha,Hora,Nombre,Monto,App", lineas[0]);
        Assert.Equal("21/11/2026,04:43 PM,Amanda Rod*,25.00,Yape", lineas[1]);
    }

    [Fact]
    public void Csv_lleva_BOM_para_que_Excel_muestre_las_tildes()
    {
        using var ms = new MemoryStream();
        Exportadores.EscribirCsv(new[] { P("José Pérez", 1m) }, ms);
        var bytes = ms.ToArray();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Contains("José Pérez", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Las_pruebas_no_se_exportan()
    {
        var texto = Csv(P("Real", 10m), P("Prueba Sistema", 1m, prueba: true));
        Assert.Contains("Real", texto);
        Assert.DoesNotContain("Prueba Sistema", texto);
    }

    [Fact]
    public void Csv_protege_comas_comillas_y_formulas()
    {
        var texto = Csv(P("Perez, Juan \"Pepe\"", 5m), P("=1+1", 2m));
        Assert.Contains("\"Perez, Juan \"\"Pepe\"\"\"", texto);
        Assert.Contains("'=1+1", texto);
    }

    [Fact]
    public void Xlsx_es_un_zip_valido_con_los_datos()
    {
        using var ms = new MemoryStream();
        Exportadores.EscribirXlsx(new[] { P("José & Cía", 1234.5m), P("Prueba Sistema", 1m, prueba: true) }, ms);
        ms.Position = 0;
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
        using var r = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var hoja = r.ReadToEnd();

        Assert.Contains("Fecha", hoja);
        Assert.Contains("José &amp; Cía", hoja);          // XML escapado
        Assert.Contains("<v>1234.50</v>", hoja);           // monto como numero real
        Assert.DoesNotContain("Prueba Sistema", hoja);     // las pruebas no se exportan
        System.Xml.Linq.XDocument.Parse(hoja);             // XML bien formado
    }

    [Fact]
    public void Registro_escribe_en_archivo_y_recorta_mensajes_largos()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pagoalerta-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Registro.Iniciar(dir, "t.log");
            Registro.Error("Fallo al enviar", new InvalidOperationException(new string('x', 500)));
            var contenido = File.ReadAllText(Path.Combine(dir, "t.log"));
            Assert.Contains("[ERROR] Fallo al enviar | InvalidOperationException:", contenido);
            Assert.True(contenido.Length < 700);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Registro_rota_cuando_el_archivo_crece()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pagoalerta-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Registro.Iniciar(dir, "r.log");
            var relleno = new string('a', 1000);
            for (var i = 0; i < 1100; i++) Registro.Info(relleno);
            Assert.True(File.Exists(Path.Combine(dir, "r.log.1")));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
