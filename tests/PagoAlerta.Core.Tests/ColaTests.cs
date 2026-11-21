using PagoAlerta.Core.Cola;
using PagoAlerta.Core.Models;

namespace PagoAlerta.Core.Tests;

/// <summary>Almacen en memoria que imita al de SQLite (orden por Id, hash unico).</summary>
internal class AlmacenMemoria : IAlmacenCola
{
    private readonly List<ItemCola> _items = new();
    private long _id;

    public bool Encolar(string hash, string json, long ahoraMs)
    {
        if (_items.Any(i => i.Hash == hash)) return false;
        _items.Add(new ItemCola(++_id, hash, json, 0, 0, ahoraMs));
        return true;
    }

    public ItemCola? Siguiente(long ahoraMs, bool ignorarEspera) =>
        _items.OrderBy(i => i.Id).FirstOrDefault(i => ignorarEspera || i.ProximoIntentoMs <= ahoraMs);

    public void Eliminar(long id) => _items.RemoveAll(i => i.Id == id);

    public void Reprogramar(long id, int intentos, long proximoMs)
    {
        var i = _items.FindIndex(x => x.Id == id);
        if (i >= 0) _items[i] = _items[i] with { Intentos = intentos, ProximoIntentoMs = proximoMs };
    }

    public int Contar() => _items.Count;
    public ItemCola? Buscar(string hash) => _items.FirstOrDefault(i => i.Hash == hash);
}

public class ColaTests
{
    private static Pago NuevoPago(string hash, string nombre = "Ana") => new()
    {
        App = "Yape", Tipo = TipoPago.Recibido, Remitente = nombre, Monto = 10m,
        FechaHora = new DateTime(2026, 11, 21, 15, 0, 0, DateTimeKind.Utc), Hash = hash, Paquete = "p"
    };

    [Fact]
    public async Task Envia_en_orden_y_vacia_la_cola()
    {
        var almacen = new AlmacenMemoria();
        var enviados = new List<string>();
        var cola = new ProcesadorCola(almacen, p => { enviados.Add(p.Hash); return Task.FromResult(SalidaEnvio.Enviado); }, () => 1000);

        cola.Encolar(NuevoPago("a")); cola.Encolar(NuevoPago("b")); cola.Encolar(NuevoPago("c"));
        var r = await cola.ProcesarAsync();

        Assert.Equal(new[] { "a", "b", "c" }, enviados);
        Assert.Equal(3, r.Enviados);
        Assert.Equal(0, r.Pendientes);
    }

    [Fact]
    public async Task Sin_internet_no_se_pierde_nada_y_se_reenvia_al_volver_la_red()
    {
        var almacen = new AlmacenMemoria();
        var hayRed = false;
        var enviados = new List<string>();
        long ahora = 1000;
        var cola = new ProcesadorCola(almacen, p =>
        {
            if (!hayRed) return Task.FromResult(SalidaEnvio.SinRed);
            enviados.Add(p.Hash);
            return Task.FromResult(SalidaEnvio.Enviado);
        }, () => ahora);

        cola.Encolar(NuevoPago("a")); cola.Encolar(NuevoPago("b"));

        var sinRed = await cola.ProcesarAsync();
        Assert.Equal(0, sinRed.Enviados);
        Assert.Equal(2, sinRed.Pendientes);          // nada se perdio
        Assert.Empty(enviados);

        hayRed = true;
        var conRed = await cola.ProcesarAsync(ignorarEspera: true);   // volvio la red
        Assert.Equal(new[] { "a", "b" }, enviados);                    // en orden
        Assert.Equal(2, conRed.Enviados);
        Assert.Equal(0, conRed.Pendientes);
    }

    [Fact]
    public async Task Respeta_la_espera_creciente_si_no_se_ignora()
    {
        var almacen = new AlmacenMemoria();
        long ahora = 1000;
        var intentosDeEnvio = 0;
        var cola = new ProcesadorCola(almacen, _ => { intentosDeEnvio++; return Task.FromResult(SalidaEnvio.SinRed); }, () => ahora);
        cola.Encolar(NuevoPago("a"));

        await cola.ProcesarAsync();                 // 1er intento: falla y se reprograma (+20 s)
        await cola.ProcesarAsync();                 // aun no es su turno
        Assert.Equal(1, intentosDeEnvio);

        ahora += 21_000;                            // pasa la espera
        await cola.ProcesarAsync();
        Assert.Equal(2, intentosDeEnvio);
        Assert.True(almacen.Buscar("a")!.Intentos == 2);
    }

    [Fact]
    public void La_espera_crece_y_tiene_tope()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), Espera.Calcular(0));
        Assert.Equal(TimeSpan.FromSeconds(20), Espera.Calcular(1));
        Assert.Equal(TimeSpan.FromSeconds(40), Espera.Calcular(2));
        Assert.Equal(Espera.Maxima, Espera.Calcular(30));
        Assert.True(Espera.Calcular(5) > Espera.Calcular(4));
    }

    [Fact]
    public async Task No_duplica_el_mismo_pago()
    {
        var almacen = new AlmacenMemoria();
        var cola = new ProcesadorCola(almacen, _ => Task.FromResult(SalidaEnvio.Enviado), () => 1);
        Assert.True(cola.Encolar(NuevoPago("x")));
        Assert.False(cola.Encolar(NuevoPago("x")));      // misma notificacion repetida
        Assert.Equal(1, cola.Pendientes);
    }

    [Fact]
    public async Task Ya_existia_en_el_servidor_cuenta_como_enviado_sin_duplicar()
    {
        var almacen = new AlmacenMemoria();
        var cola = new ProcesadorCola(almacen, _ => Task.FromResult(SalidaEnvio.YaExistia), () => 1);
        cola.Encolar(NuevoPago("x"));
        var r = await cola.ProcesarAsync();
        Assert.Equal(1, r.Enviados);
        Assert.Equal(0, r.Pendientes);
    }

    [Fact]
    public async Task Sesion_vencida_conserva_todo_para_cuando_inicie_sesion()
    {
        var almacen = new AlmacenMemoria();
        var cola = new ProcesadorCola(almacen, _ => Task.FromResult(SalidaEnvio.SesionVencida), () => 1);
        cola.Encolar(NuevoPago("a")); cola.Encolar(NuevoPago("b"));
        var r = await cola.ProcesarAsync();
        Assert.Equal(0, r.Enviados);
        Assert.Equal(2, r.Pendientes);
        Assert.Equal(SalidaEnvio.SesionVencida, r.UltimaSalida);
    }

    [Fact]
    public async Task Un_error_no_bloquea_los_demas_pagos()
    {
        var almacen = new AlmacenMemoria();
        var enviados = new List<string>();
        var cola = new ProcesadorCola(almacen, p =>
        {
            if (p.Hash == "malo") return Task.FromResult(SalidaEnvio.Error);
            enviados.Add(p.Hash);
            return Task.FromResult(SalidaEnvio.Enviado);
        }, () => 1);

        cola.Encolar(NuevoPago("malo")); cola.Encolar(NuevoPago("bueno"));
        var r = await cola.ProcesarAsync();

        Assert.Equal(new[] { "bueno" }, enviados);
        Assert.Equal(1, r.Pendientes);                 // el malo se reintenta luego
    }

    [Fact]
    public async Task El_contenido_del_pago_sobrevive_al_guardado()
    {
        var almacen = new AlmacenMemoria();
        Pago? recibido = null;
        var cola = new ProcesadorCola(almacen, p => { recibido = p; return Task.FromResult(SalidaEnvio.Enviado); }, () => 1);
        cola.Encolar(NuevoPago("h", "José Pérez"));
        await cola.ProcesarAsync();

        Assert.NotNull(recibido);
        Assert.Equal("José Pérez", recibido!.Remitente);
        Assert.Equal(10m, recibido.Monto);
        Assert.Equal("Yape", recibido.App);
        Assert.Equal(DateTimeKind.Utc, recibido.FechaHora.Kind);
    }
}
