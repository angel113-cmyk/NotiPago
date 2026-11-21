using PagoAlerta.Core.Parsing;

namespace PagoAlerta.Core.Tests;

public class MontoParserTests
{
    [Theory]
    [InlineData("S/ 10", 10.00)]
    [InlineData("S/10", 10.00)]
    [InlineData("S/10.00", 10.00)]
    [InlineData("S/25", 25.00)]
    [InlineData("S/ 1,234.50", 1234.50)]
    [InlineData("S/1,234.50", 1234.50)]
    [InlineData("$1,324.00", 1324.00)]
    [InlineData("S/ 0.50", 0.50)]
    [InlineData("S/ 10,50", 10.50)]
    [InlineData("S/ 1,000", 1000)]
    [InlineData("S/ 25.", 25)]
    public void Acepta_formatos_de_monto(string texto, double esperado)
    {
        Assert.True(MontoParser.TryParse(texto, out var monto));
        Assert.Equal((decimal)esperado, monto);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("sin numeros")]
    [InlineData("S/ 0.00")]
    public void Rechaza_texto_sin_monto_valido(string? texto)
    {
        Assert.False(MontoParser.TryParse(texto, out _));
    }
}
