using PagoAlerta.Core.Models;
using PagoAlerta.Core.Parsing;
using PagoAlerta.Core.Services;

namespace PagoAlerta.Core.Tests;

/// <summary>El mensaje de prueba se ve como un pago real de cada app (pasa por el parser real).</summary>
public class PruebaPorAppTests
{
    private static readonly NotificationParser Parser = NotificationParser.PorDefecto();

    [Theory]
    [InlineData("Yape")]
    [InlineData("Plin")]
    [InlineData("BBVA")]
    public void Cada_app_genera_una_prueba_reconocida_por_el_parser(string app)
    {
        var p = PagoFactory.CrearPruebaPorParser(Parser, app);

        Assert.NotNull(p);
        Assert.True(p!.EsPrueba);
        Assert.Equal(app, p.App);
        Assert.Equal(TipoPago.Recibido, p.Tipo);
        Assert.Equal(1.00m, p.Monto);
        Assert.Equal("PEN", p.Moneda);
        Assert.False(string.IsNullOrEmpty(p.Paquete));
        Assert.True(Avisos.DebeAvisar(p));
    }

    [Fact]
    public void Plin_y_Yape_traen_el_nombre_de_prueba_y_BBVA_no_trae_nombre_como_el_real()
    {
        Assert.Equal("Prueba Sistema", PagoFactory.CrearPruebaPorParser(Parser, "Yape")!.Remitente);
        Assert.Equal("Prueba Sistema", PagoFactory.CrearPruebaPorParser(Parser, "Plin")!.Remitente);
        Assert.Null(PagoFactory.CrearPruebaPorParser(Parser, "BBVA")!.Remitente);
    }

    [Fact]
    public void La_prueba_de_Plin_usa_el_texto_de_Plin_dentro_de_la_app_de_BBVA()
    {
        var p = PagoFactory.CrearPruebaPorParser(Parser, "Plin")!;
        Assert.Equal("com.bbva.nxt_peru", p.Paquete);
        Assert.Contains("¡Recibiste un Plin!", p.TituloOriginal);
        Assert.Contains("te plineó", p.TextoOriginal);
    }

    [Fact]
    public void Sin_indicar_app_se_elige_una_de_las_tres()
    {
        var vistas = new HashSet<string?>();
        for (var i = 0; i < 200; i++) vistas.Add(PagoFactory.CrearPruebaPorParser(Parser)!.App);
        Assert.Equal(new HashSet<string?> { "Yape", "Plin", "BBVA" }, vistas);
    }

    [Fact]
    public void Dos_pruebas_de_la_misma_app_no_se_deduplican()
    {
        var hora = new DateTime(2026, 11, 21, 16, 43, 0, DateTimeKind.Utc);
        Assert.NotEqual(PagoFactory.CrearPruebaPorParser(Parser, "Plin", hora)!.Hash,
                        PagoFactory.CrearPruebaPorParser(Parser, "Plin", hora)!.Hash);
    }

    [Fact]
    public void La_lista_de_apps_de_prueba_coincide_con_las_del_parser()
    {
        // Plin no tiene fila propia: llega por la app de BBVA (su paquete esta en la fila BBVA).
        foreach (var app in PagoFactory.AppsDePrueba)
            Assert.Contains(app == "Plin" ? "BBVA" : app, Parser.PaquetesPorApp.Keys);
    }
}
