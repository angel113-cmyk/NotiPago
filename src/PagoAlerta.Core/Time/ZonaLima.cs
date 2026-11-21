namespace PagoAlerta.Core.Time;

/// <summary>HT-14: la BD guarda UTC; la conversion a America/Lima se hace solo al mostrar.</summary>
public static class ZonaLima
{
    public static readonly TimeZoneInfo Zona = Resolver();

    private static TimeZoneInfo Resolver()
    {
        foreach (var id in new[] { "America/Lima", "SA Pacific Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        // Lima es UTC-5 todo el ano (sin horario de verano).
        return TimeZoneInfo.CreateCustomTimeZone("America/Lima", TimeSpan.FromHours(-5), "Lima", "Lima");
    }

    public static DateTime DeUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);

    public static DateTime AUtc(DateTime lima) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(lima, DateTimeKind.Unspecified), Zona);

    /// <summary>Rango UTC [inicio, fin) que cubre el dia calendario de Lima indicado.</summary>
    public static (DateTime DesdeUtc, DateTime HastaUtc) RangoDia(DateTime diaLima) =>
        (AUtc(diaLima.Date), AUtc(diaLima.Date.AddDays(1)));

    public static DateTime AhoraLima => DeUtc(DateTime.UtcNow);
}
