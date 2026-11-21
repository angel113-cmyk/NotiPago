using PagoAlerta.Core.Models;
using PagoAlerta.Core.Parsing;
using PagoAlerta.Core.Services;

namespace PagoAlerta.Core.Tests;

public class HashYFlujoTests
{
    private static readonly NotificationParser Parser = NotificationParser.PorDefecto();
    private const string Yape = "com.bcp.innovacxion.yapeapp";
    private static readonly DateTime T = new(2026, 11, 21, 16, 43, 10, DateTimeKind.Utc);

    [Fact]
    public void Misma_notificacion_mismo_minuto_mismo_hash()
    {
        var a = HashPago.Calcular(Yape, "Confirmación de pago", "Ana te envió un pago por S/25.", T);
        var b = HashPago.Calcular(Yape, "Confirmación de pago", "Ana te envió un pago por S/25.", T.AddSeconds(40));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Espacios_repetidos_no_cambian_el_hash()
    {
        var a = HashPago.Calcular(Yape, "T", "Ana  te envió   un pago", T);
        var b = HashPago.Calcular(Yape, "T", "Ana te envió un pago", T);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Distinto_minuto_texto_o_paquete_cambia_el_hash()
    {
        var baseHash = HashPago.Calcular(Yape, "T", "Ana te envió un pago por S/25.", T);
        Assert.NotEqual(baseHash, HashPago.Calcular(Yape, "T", "Ana te envió un pago por S/25.", T.AddMinutes(1)));
        Assert.NotEqual(baseHash, HashPago.Calcular(Yape, "T", "Ana te envió un pago por S/26.", T));
        Assert.NotEqual(baseHash, HashPago.Calcular("otro.paquete", "T", "Ana te envió un pago por S/25.", T));
    }

    [Fact]
    public void Dos_pagos_reales_iguales_en_el_mismo_minuto_se_consideran_el_mismo()
    {
        const string texto = "Amanda Rod* te envió un pago por S/25. El cód. de seguridad es:004";
        var d = Parser.Parse(Yape, "Confirmación de pago", texto, T)!;
        var p1 = PagoFactory.DesdeDetectado(d);
        var p2 = PagoFactory.DesdeDetectado(d);
        Assert.Equal(p1.Hash, p2.Hash);
        Assert.False(p1.EsPrueba);
        Assert.Equal("Amanda Rod*", p1.Remitente);
        Assert.Equal(25m, p1.Monto);
    }

    [Fact]
    public void Prueba_por_parser_pasa_por_el_mismo_camino_que_un_pago_real()
    {
        var p = PagoFactory.CrearPruebaPorParser(Parser, "Yape", T);
        Assert.NotNull(p);
        Assert.True(p!.EsPrueba);
        Assert.Equal("Prueba Sistema", p.Remitente);
        Assert.Equal(1.00m, p.Monto);
        Assert.Equal("Yape", p.App);
        Assert.Equal(TipoPago.Recibido, p.Tipo);
        Assert.Contains("te envió un pago por S/1.00", p.TextoOriginal);
    }

    [Fact]
    public void Cada_prueba_por_parser_tiene_hash_distinto()
    {
        Assert.NotEqual(PagoFactory.CrearPruebaPorParser(Parser, "Yape", T)!.Hash, PagoFactory.CrearPruebaPorParser(Parser, "Yape", T)!.Hash);
    }

    [Fact]
    public void Catalogo_de_apps_sale_de_las_reglas()
    {
        var cat = Parser.PaquetesPorApp;
        Assert.Contains(Yape, cat["Yape"]);
        Assert.True(cat.ContainsKey("Plin"));
        Assert.True(cat.ContainsKey("BBVA"));
    }
}
