using PagoAlerta.Core.Models;

namespace PagoAlerta.Core.Tests;

public class AvisosTests
{
    private static Pago P(string tipo = TipoPago.Recibido, string? nombre = "María Quispe", string? app = "Yape",
        decimal monto = 35m, bool prueba = false) => new()
    {
        Tipo = tipo, Remitente = nombre, App = app, Monto = monto, EsPrueba = prueba, Moneda = "PEN"
    };

    [Fact]
    public void Pago_recibido_muestra_monto_nombre_y_app()
    {
        var (titulo, cuerpo) = Avisos.DePago(P());
        Assert.Equal("Pago recibido", titulo);
        Assert.Equal("S/ 35.00 · María Quispe (Yape)", cuerpo);
    }

    [Fact]
    public void Formatea_miles_con_coma()
    {
        Assert.Equal("S/ 1,234.50 · María Quispe (Yape)", Avisos.DePago(P(monto: 1234.5m)).Cuerpo);
    }

    [Fact]
    public void Sin_nombre_ni_app_solo_el_monto()
    {
        Assert.Equal("S/ 35.00", Avisos.DePago(P(nombre: null, app: null)).Cuerpo);
        Assert.Equal("S/ 35.00 (BBVA)", Avisos.DePago(P(nombre: " ", app: "BBVA")).Cuerpo);
    }

    [Fact]
    public void La_prueba_se_identifica_en_el_titulo()
    {
        Assert.Equal("Prueba: pago de ejemplo", Avisos.DePago(P(prueba: true)).Titulo);
    }

    [Theory]
    [InlineData(TipoPago.Recibido, true)]
    [InlineData(TipoPago.Enviado, false)]
    [InlineData(TipoPago.Otro, false)]
    public void Solo_los_pagos_recibidos_avisan(string tipo, bool esperado) =>
        Assert.Equal(esperado, Avisos.DebeAvisar(P(tipo)));
}
