namespace PagoAlerta.Core.Services;

/// <summary>Reintentos con espera creciente solo para fallos de red transitorios.</summary>
public static class Reintentos
{
    public static async Task<T> EjecutarAsync<T>(
        Func<Task<T>> operacion, int maxIntentos = 3, int esperaInicialMs = 500, CancellationToken ct = default)
    {
        for (var intento = 1; ; intento++)
        {
            try { return await operacion().ConfigureAwait(false); }
            catch (Exception ex) when (intento < maxIntentos && EsTransitorio(ex) && !ct.IsCancellationRequested)
            {
                await Task.Delay(esperaInicialMs * (1 << (intento - 1)), ct).ConfigureAwait(false);
            }
        }
    }

    public static bool EsTransitorio(Exception ex) => ex switch
    {
        HttpRequestException => true,
        TaskCanceledException => true,   // timeout
        IOException => true,
        _ => ex.InnerException is not null && EsTransitorio(ex.InnerException)
    };
}
