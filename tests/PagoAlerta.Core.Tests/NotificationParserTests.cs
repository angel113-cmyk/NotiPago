using PagoAlerta.Core.Models;
using PagoAlerta.Core.Parsing;

namespace PagoAlerta.Core.Tests;

public class NotificationParserTests
{
    private static readonly NotificationParser Parser = NotificationParser.PorDefecto();
    private static readonly DateTime Hora = new(2026, 11, 21, 16, 43, 0, DateTimeKind.Utc);

    private const string PaqueteYape = "com.bcp.innovacxion.yapeapp";
    private const string PaqueteBbva = "com.bancomer.mbanking";

    // Ejemplo real (captura de la tienda/pruebas).
    [Fact]
    public void Yape_recibido_extrae_nombre_y_monto()
    {
        const string texto = "Amanda Rod* te envió un pago por S/25. El cód. de seguridad es:004";
        var r = Parser.Parse(PaqueteYape, "Confirmación de pago", texto, Hora);

        Assert.NotNull(r);
        Assert.Equal("Yape", r!.App);
        Assert.Equal(TipoPago.Recibido, r.Tipo);
        Assert.Equal("Amanda Rod*", r.Nombre);
        Assert.Equal(25.00m, r.Monto);
        Assert.Equal("PEN", r.Moneda);
        Assert.Equal(Hora, r.FechaHoraUtc);
    }

    [Fact]
    public void Siempre_conserva_el_texto_original()
    {
        const string texto = "Amanda Rod* te envió un pago por S/25. El cód. de seguridad es:004";
        var r = Parser.Parse(PaqueteYape, "Confirmación de pago", texto, Hora)!;
        Assert.Equal(texto, r.TextoOriginal);
        Assert.Equal("Confirmación de pago", r.TituloOriginal);
        Assert.Equal(PaqueteYape, r.Paquete);
    }

    [Theory]
    [InlineData("Juan Pérez Soto te envió un pago por S/10.00", "Juan Pérez Soto", 10.00)]
    [InlineData("María López te envió un pago por S/ 1,234.50", "María López", 1234.50)]
    [InlineData("Carlos R* te envió un pago por S/10", "Carlos R*", 10.00)]
    public void Yape_acepta_variantes_de_monto(string texto, string nombre, double monto)
    {
        var r = Parser.Parse(PaqueteYape, "Confirmación de pago", texto, Hora);
        Assert.NotNull(r);
        Assert.Equal(nombre, r!.Nombre);
        Assert.Equal((decimal)monto, r.Monto);
    }

    [Fact]
    public void Bbva_abono_es_recibido()
    {
        var r = Parser.Parse(PaqueteBbva, "BBVA", "Abono a tu cuenta *29400 de $1,324.00", Hora);
        Assert.NotNull(r);
        Assert.Equal("BBVA", r!.App);
        Assert.Equal(TipoPago.Recibido, r.Tipo);
        Assert.Equal(1324.00m, r.Monto);
        Assert.Null(r.Nombre); // la notificacion de BBVA no trae nombre
    }

    [Fact]
    public void Bbva_cargo_es_enviado()
    {
        var r = Parser.Parse(PaqueteBbva, "BBVA", "Cargo a tu cuenta *9400 de $1,324.00", Hora);
        Assert.NotNull(r);
        Assert.Equal(TipoPago.Enviado, r!.Tipo);
        Assert.Equal(1324.00m, r.Monto);
    }

    [Fact]
    public void Notificacion_que_no_es_pago_devuelve_null()
    {
        Assert.Null(Parser.Parse(PaqueteYape, "Yape", "Promo: gana premios este mes", Hora));
        Assert.Null(Parser.Parse(PaqueteYape, null, "", Hora));
    }

    [Fact]
    public void Paquete_no_monitoreado_se_ignora()
    {
        var r = Parser.Parse("com.otra.app", "Confirmación de pago",
            "Amanda Rod* te envió un pago por S/25.", Hora);
        Assert.Null(r);
    }

    [Fact]
    public void Reglas_son_configurables_por_json()
    {
        const string json = """
        { "reglas": [ { "app": "Prueba", "tipo": "recibido",
          "patron": "(?<nombre>\\w+) pagó (?<monto>S/\\s*[\\d.,]+)" } ] }
        """;
        var p = NotificationParser.DesdeJson(json);
        var r = p.Parse("cualquier.paquete", null, "Luis pagó S/ 7.50", Hora);
        Assert.NotNull(r);
        Assert.Equal("Prueba", r!.App);
        Assert.Equal("Luis", r.Nombre);
        Assert.Equal(7.50m, r.Monto);
    }
}
