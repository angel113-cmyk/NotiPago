using PagoAlerta.Core.Config;
using PagoAlerta.Core.Time;

namespace PagoAlerta.Core.Tests;

public class ZonaYConfigTests
{
    [Fact]
    public void Lima_es_utc_menos_5()
    {
        var utc = new DateTime(2026, 11, 21, 3, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 11, 20, 22, 0, 0), ZonaLima.DeUtc(utc));
    }

    [Fact]
    public void Rango_del_dia_en_lima_cubre_24_horas_en_utc()
    {
        var (desde, hasta) = ZonaLima.RangoDia(new DateTime(2026, 11, 21));
        Assert.Equal(new DateTime(2026, 11, 21, 5, 0, 0), desde);
        Assert.Equal(new DateTime(2026, 11, 22, 5, 0, 0), hasta);
    }

    [Fact]
    public void Formato_de_monto_es_soles_con_miles()
    {
        Assert.Equal("S/ 1,234.50", Formato.Monto(1234.5m));
        Assert.Equal("S/ 25.00", Formato.Monto(25m, "PEN"));
    }

    [Fact]
    public void Iniciales_toma_hasta_dos_letras()
    {
        Assert.Equal("AR", Formato.Iniciales("Amanda Rod*"));
        Assert.Equal("?", Formato.Iniciales(null));
    }

    [Theory]
    [InlineData("sb_secret_abc123")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoic2VydmljZV9yb2xlIn0.firma")]
    public void Detecta_claves_service_role(string clave) =>
        Assert.True(SupabaseSettings.EsClaveSecreta(clave));

    [Theory]
    [InlineData("sb_publishable_abc123")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoiYW5vbiJ9.firma")]
    public void Acepta_claves_anon(string clave) =>
        Assert.False(SupabaseSettings.EsClaveSecreta(clave));

    [Fact]
    public void Validar_rechaza_clave_secreta()
    {
        var s = new SupabaseSettings { Url = "https://x.supabase.co", AnonKey = "sb_secret_zzz" };
        Assert.Throws<InvalidOperationException>(() => s.Validar());
    }
}
