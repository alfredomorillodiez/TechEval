namespace TechEval.Application;

/// <summary>
/// Convierte el rango de días que elige el administrador (días del calendario de su zona) en
/// instantes UTC para la API: <c>From</c> inclusivo y <c>To</c> exclusivo. El servidor no conoce la
/// zona del navegador, así que la conversión se hace en el cliente. Un día local no dura siempre
/// 24 horas (cambio de hora), por eso se convierte el día entero y no se suma un día a un instante.
/// </summary>
public static class DayRange
{
    /// <param name="fromDay">Primer día incluido. Solo cuenta la fecha.</param>
    /// <param name="toDay">Último día incluido. Solo cuenta la fecha.</param>
    public static (DateTime? From, DateTime? To) ToUtc(DateTime? fromDay, DateTime? toDay, TimeZoneInfo zone)
        => (fromDay is { } f ? StartOfDayUtc(f.Date, zone) : null,
            toDay is { } t ? StartOfDayUtc(t.Date.AddDays(1), zone) : null);

    private static DateTime StartOfDayUtc(DateTime localMidnight, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(localMidnight, DateTimeKind.Unspecified);
        // Una medianoche que no existe (salto de hora a las 00:00): se toma la primera hora que sí existe.
        if (zone.IsInvalidTime(unspecified))
            unspecified = unspecified.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
    }
}
