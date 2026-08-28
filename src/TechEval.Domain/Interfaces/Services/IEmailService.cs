namespace TechEval.Domain.Interfaces.Services;

public interface IEmailService
{
    Task SendExamInvitationAsync(
        string toEmail,
        string toName,
        string examTitle,
        string examLink,
        DateTime expiresAt,
        CancellationToken ct = default);

    Task SendExamResultAsync(
        string toEmail,
        string toName,
        string examTitle,
        decimal scorePercentage,
        bool passed,
        CancellationToken ct = default);

    /// <summary>
    /// Acuse de recibo para pruebas que quedan pendientes de corrección manual.
    /// Sin parámetros de puntuación: así es estructuralmente imposible filtrar cifras
    /// de un resultado que todavía no tiene veredicto.
    /// </summary>
    Task SendExamPendingReviewAsync(
        string toEmail,
        string toName,
        string examTitle,
        CancellationToken ct = default);
}
