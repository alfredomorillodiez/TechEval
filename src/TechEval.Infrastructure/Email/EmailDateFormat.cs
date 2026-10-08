namespace TechEval.Infrastructure.Email;

/// <summary>
/// Fecha de un correo en la zona horaria configurada. Antes los correos decían «expira el
/// 09/10/2026 12:30 UTC»: el candidato leía la hora en su reloj local y se le pasaba el plazo.
/// El valor guardado sigue siendo UTC; solo cambia cómo se escribe.
/// </summary>
public static class EmailDateFormat
{
    public const string DefaultTimeZoneId = "Europe/Madrid";

    /// <summary>
    /// Resuelve la zona. Si el equipo no la conoce (un id mal escrito, o Windows sin ICU),
    /// devuelve UTC y `found = false`: un correo con la hora en UTC y bien etiquetada es mejor
    /// que un envío que falla.
    /// </summary>
    public static TimeZoneInfo Resolve(string? timeZoneId, out bool found)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim();
        try
        {
            found = true;
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            found = false;
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// «09/10/2026 12:30 (hora de Madrid, UTC+02:00)», o «09/10/2026 10:30 (UTC)» con la zona
    /// UTC. La entrada se trata como UTC aunque llegue con `Kind` sin definir: SQL Server
    /// devuelve `DateTime` sin zona.
    /// </summary>
    public static string Format(DateTime utc, TimeZoneInfo zone, string? timeZoneId = null)
    {
        var instant = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        if (zone.Equals(TimeZoneInfo.Utc))
            return $"{instant:dd/MM/yyyy HH:mm} (UTC)";

        var local = TimeZoneInfo.ConvertTimeFromUtc(instant, zone);
        var offset = zone.GetUtcOffset(instant);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var city = CityOf(timeZoneId ?? zone.Id);
        // En un formato de TimeSpan, los dos puntos hay que escaparlos.
        var hours = offset.Duration().ToString("hh\\:mm");
        return $"{local:dd/MM/yyyy HH:mm} (hora de {city}, UTC{sign}{hours})";
    }

    // «Europe/Madrid» → «Madrid»; «America/Argentina/Buenos_Aires» → «Buenos Aires».
    private static string CityOf(string id)
        => id[(id.LastIndexOf('/') + 1)..].Replace('_', ' ');
}
