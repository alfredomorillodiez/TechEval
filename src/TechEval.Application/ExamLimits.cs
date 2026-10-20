namespace TechEval.Application;

/// <summary>
/// Límites de los datos de una prueba y de una invitación. El servidor los exige y la Web los
/// usa en sus formularios: una sola definición y un solo texto, para que no discrepen. Antes
/// nada los comprobaba, y se podía crear una prueba de 0 minutos o sin preguntas.
/// </summary>
public static class ExamLimits
{
    public const int TitleMaxLength = 200;
    public const int MinTimeLimitMinutes = 1;
    public const int MaxTimeLimitMinutes = 600;
    public const int MinPassingScore = 0;
    public const int MaxPassingScore = 100;
    public const int MaxGeneratedQuestions = 200;
    public const int MinExpirationHours = 1;
    public const int MaxExpirationHours = 720;
    public const int MaxBulkCandidates = 500;

    /// <summary>
    /// El primer problema de los datos propios de una prueba, o null si están bien. Lo usa el
    /// servidor para rechazar con 400 y la Web para avisar antes de enviar: es el mismo texto.
    /// </summary>
    public static string? ExamDataProblem(string? title, int timeLimitMinutes, int passingScore)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "El título es obligatorio.";
        if (title.Trim().Length > TitleMaxLength)
            return $"El título no puede pasar de {TitleMaxLength} caracteres.";
        if (timeLimitMinutes < MinTimeLimitMinutes || timeLimitMinutes > MaxTimeLimitMinutes)
            return $"El tiempo límite debe estar entre {MinTimeLimitMinutes} y {MaxTimeLimitMinutes} minutos.";
        if (passingScore < MinPassingScore || passingScore > MaxPassingScore)
            return $"La nota de corte debe estar entre {MinPassingScore} y {MaxPassingScore}.";
        return null;
    }

    /// <summary>El problema de la expiración de una invitación, o null.</summary>
    public static string? ExpirationProblem(int hours)
        => hours < MinExpirationHours || hours > MaxExpirationHours
            ? $"La expiración debe estar entre {MinExpirationHours} y {MaxExpirationHours} horas."
            : null;
}
