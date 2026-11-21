using PagoAlerta.Core.Models;
using PagoAlerta.Core.Services;

namespace PagoAlerta.Core.Tests;

public class TextoYPruebasTests
{
    [Theory]
    [InlineData("Pérez", "perez")]
    [InlineData("ÑANDÚ López", "nandu lopez")]
    [InlineData("  María  ", "maria")]
    [InlineData(null, "")]
    public void Normalizar_quita_tildes_y_mayusculas(string? entrada, string esperado) =>
        Assert.Equal(esperado, Texto.Normalizar(entrada));

    [Theory]
    [InlineData("José Pérez Soto", "perez", true)]
    [InlineData("José Pérez Soto", "JOSE", true)]
    [InlineData("José Pérez Soto", "soto", true)]
    [InlineData("José Pérez Soto", "garcia", false)]
    [InlineData("Amanda Rod*", "", true)]
    [InlineData(null, "ana", false)]
    public void Contiene_ignora_tildes_y_mayusculas(string? nombre, string busqueda, bool esperado) =>
        Assert.Equal(esperado, Texto.Contiene(nombre, busqueda));

    [Fact]
    public void PatronIlike_tolera_tildes_con_comodines()
    {
        // "perez" debe poder coincidir con "Pérez" en el servidor.
        Assert.Equal("%p_r_z%", Texto.PatronIlike("perez"));
        Assert.Equal("%m_r__%", Texto.PatronIlike("María"));
    }

    [Fact]
    public void Pago_de_prueba_es_de_un_sol_y_marcado()
    {
        var p = PagoFactory.CrearPrueba();
        Assert.True(p.EsPrueba);
        Assert.Equal(1.00m, p.Monto);
        Assert.Equal("Prueba Sistema", p.Remitente);
        Assert.Equal(TipoPago.Recibido, p.Tipo);
        Assert.Equal(DateTimeKind.Utc, p.FechaHora.Kind);
    }

    [Fact]
    public void Cada_prueba_tiene_hash_distinto()
    {
        Assert.NotEqual(PagoFactory.CrearPrueba().Hash, PagoFactory.CrearPrueba().Hash);
    }

    [Fact]
    public void Filtro_excluye_pruebas_por_defecto()
    {
        Assert.False(new FiltroPagos().IncluirPruebas);
    }
}
