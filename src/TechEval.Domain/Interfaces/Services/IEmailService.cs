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
}
