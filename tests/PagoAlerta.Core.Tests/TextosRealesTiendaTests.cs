using PagoAlerta.Core.Parsing;
using Xunit;

namespace PagoAlerta.Core.Tests;

/// <summary>Textos reales capturados de las notificaciones de la tienda (Plin por BBVA Peru y Yape).</summary>
public class TextosRealesTiendaTests
{
    private static readonly NotificationParser Parser = NotificationParser.PorDefecto();
    private static readonly DateTime Hora = new(2026, 11, 22, 16, 7, 0, DateTimeKind.Utc);
    private const string Yape = "com.bcp.innovacxion.yapeapp";
    private const string Bbva = "com.bbva.nxt_peru";

    [Fact]
    public void Plin_real_de_BBVA_Peru()
    {
        var r = Parser.Parse(Bbva, "¡Recibiste un Plin! 💵", "FIORELLA DE JESUS MA te plineó S/16.80 .", Hora);
        Assert.NotNull(r);
        Assert.Equal("Plin", r!.App);
        Assert.Equal("recibido", r.Tipo);
        Assert.Equal("FIORELLA DE JESUS MA", r.Nombre);
        Assert.Equal(16.80m, r.Monto);
    }

    [Theory]
    [InlineData("Erick Pai* te envió un pago por S/ 100. El cód. de seguridad es: 628", "Erick Pai*")]
    [InlineData("Shela Gon* te envió un pago por S/ 100. El cód. de seguridad es: 842", "Shela Gon*")]
    [InlineData("Amalda Vas* te envió un pago por S/ 100. El cód. de seguridad es: 471", "Amalda Vas*")]
    public void Yape_real(string texto, string nombre)
    {
        var r = Parser.Parse(Yape, "Confirmación de Pago", texto, Hora);
        Assert.NotNull(r);
        Assert.Equal("Yape", r!.App);
        Assert.Equal("recibido", r.Tipo);
        Assert.Equal(nombre, r.Nombre);
        Assert.Equal(100m, r.Monto);
    }
}
