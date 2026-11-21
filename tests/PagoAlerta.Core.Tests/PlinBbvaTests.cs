using PagoAlerta.Core.Models;
using PagoAlerta.Core.Parsing;

namespace PagoAlerta.Core.Tests;

/// <summary>
/// Plin llega DENTRO de la app del banco (icono de BBVA). Texto segun la descripcion del negocio:
/// titulo "¡Recibiste un Plin!" / "Operación realizada" y cuerpo "Recibiste S/ 50.00 de [Nombre] vía Plin".
/// (Reglas provisionales hasta tener una captura real.)
/// </summary>
public class PlinBbvaTests
{
    private static readonly NotificationParser Parser = NotificationParser.PorDefecto();
    private static readonly DateTime Hora = new(2026, 11, 21, 16, 43, 0, DateTimeKind.Utc);
    private const string AppBbvaPeru = "com.bbva.nxt_peru";

    [Fact]
    public void Plin_recibido_con_via_Plin_extrae_nombre_sin_la_cola()
    {
        var r = Parser.Parse(AppBbvaPeru, "¡Recibiste un Plin!", "Recibiste S/ 50.00 de Juan Pérez López vía Plin", Hora);

        Assert.NotNull(r);
        Assert.Equal("Plin", r!.App);
        Assert.Equal(TipoPago.Recibido, r.Tipo);
        Assert.Equal("Juan Pérez López", r.Nombre);      // sin "vía Plin"
        Assert.Equal(50.00m, r.Monto);
        Assert.Equal("PEN", r.Moneda);
        Assert.Equal(AppBbvaPeru, r.Paquete);
    }

    [Theory]
    [InlineData("Recibiste S/ 120.50 de MARÍA QUISPE R. vía Plin", "MARÍA QUISPE R.", 120.50)]
    [InlineData("Recibiste S/ 1,234.50 de Lucía Salazar vía Plin a las 10:35 p. m.", "Lucía Salazar", 1234.50)]
    [InlineData("Recibiste S/10 de Carlos H* vía Plin", "Carlos H*", 10.00)]
    [InlineData("Recibiste S/ 50.00 de Juan Pérez.", "Juan Pérez", 50.00)]
    [InlineData("Recibiste S/ 75.00 de Ana Torres", "Ana Torres", 75.00)]
    public void Plin_recibido_acepta_variantes_de_nombre_y_monto(string texto, string nombre, double monto)
    {
        var r = Parser.Parse(AppBbvaPeru, "Operación realizada", texto, Hora);
        Assert.NotNull(r);
        Assert.Equal("Plin", r!.App);
        Assert.Equal(nombre, r.Nombre);
        Assert.Equal((decimal)monto, r.Monto);
    }

    [Fact]
    public void Plin_con_titulo_y_cuerpo_separados()
    {
        // Titulo "¡Recibiste un Plin!" y el cuerpo solo trae monto y nombre.
        var r = Parser.Parse(AppBbvaPeru, "¡Recibiste un Plin!", "S/ 35.00 de Rosa Delgado Vega", Hora);
        Assert.NotNull(r);
        Assert.Equal("Plin", r!.App);
        Assert.Equal("Rosa Delgado Vega", r.Nombre);
        Assert.Equal(35.00m, r.Monto);
    }

    [Fact]
    public void Plin_enviado_no_es_un_pago_recibido()
    {
        var r = Parser.Parse(AppBbvaPeru, "Operación realizada", "Enviaste S/ 20.00 a Pedro Alarcón vía Plin", Hora);
        Assert.NotNull(r);
        Assert.Equal(TipoPago.Enviado, r!.Tipo);
        Assert.Equal("Pedro Alarcón", r.Nombre);
        Assert.False(Avisos.DebeAvisar(PagoAlerta.Core.Services.PagoFactory.DesdeDetectado(r)));   // un envio no avisa
    }

    [Fact]
    public void Plin_recibido_genera_aviso()
    {
        var d = Parser.Parse(AppBbvaPeru, "¡Recibiste un Plin!", "Recibiste S/ 50.00 de Juan Pérez vía Plin", Hora)!;
        var pago = PagoAlerta.Core.Services.PagoFactory.DesdeDetectado(d);
        Assert.True(Avisos.DebeAvisar(pago));
        Assert.Equal("S/ 50.00 · Juan Pérez (Plin)", Avisos.DePago(pago).Cuerpo);
        Assert.Equal("Juan Pérez · Plin", Avisos.Detalle(pago));
    }

    [Fact]
    public void Plin_tambien_funciona_desde_otras_apps_de_banco_marcadas()
    {
        foreach (var paquete in new[] { "pe.com.interbank.mobilebanking", "com.scotiabank.mobile.pe", "pe.com.bcp.mobile" })
        {
            var r = Parser.Parse(paquete, "¡Recibiste un Plin!", "Recibiste S/ 18.00 de Ana Ruiz vía Plin", Hora);
            Assert.NotNull(r);
            Assert.Equal("Ana Ruiz", r!.Nombre);
        }
    }

    [Fact]
    public void Una_promocion_del_banco_no_se_confunde_con_un_pago()
    {
        Assert.Null(Parser.Parse(AppBbvaPeru, "Promo BBVA", "Gana S/ 100 de regalo con tu tarjeta. Conoce más.", Hora));
        Assert.Null(Parser.Parse(AppBbvaPeru, "Recordatorio", "Tu saldo está disponible. Revisa tu cuenta.", Hora));
    }

    [Fact]
    public void El_abono_de_cuenta_de_BBVA_sigue_funcionando()
    {
        var r = Parser.Parse(AppBbvaPeru, "BBVA", "Abono a tu cuenta *29400 de $1,324.00", Hora);
        Assert.NotNull(r);
        Assert.Equal("BBVA", r!.App);
        Assert.Equal(TipoPago.Recibido, r.Tipo);
        Assert.Equal(1324.00m, r.Monto);
    }
}
