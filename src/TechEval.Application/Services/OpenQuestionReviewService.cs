using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Application.Services;

/// <summary>Se lanza cuando el resultado ya fue corregido: la API la traduce a 409.</summary>
public class AlreadyReviewedException : ConflictException
{
    public AlreadyReviewedException(string message) : base(message) { }
}

/// <summary>Se lanza cuando la corrección no es válida: la API la traduce a 400.</summary>
public class InvalidReviewException : ValidationException
{
    public InvalidReviewException(string message) : base(message) { }
}

/// <summary>La corrección desde la consola del administrador, que ve la identidad del candidato.</summary>
public interface IOpenQuestionReviewService
{
    Task<IReadOnlyList<PendingReviewSummaryDto>> GetPendingAsync(CancellationToken ct = default);

    /// <summary>El detalle de corrección. Abrirlo reserva el resultado para quien lo abre.</summary>
    Task<PendingReviewDetailDto?> GetDetailAsync(int resultId, int reviewerId, CancellationToken ct = default);

    /// <summary>Renueva la reserva de quien la tiene. Devuelve el fin nuevo.</summary>
    Task<DateTime> RenewReservationAsync(int resultId, int reviewerId, CancellationToken ct = default);

    /// <summary>Libera la reserva, sea de quien sea: el administrador puede liberar la de otro.</summary>
    Task ReleaseReservationAsync(int resultId, CancellationToken ct = default);

    Task<ExamResultDto> SubmitReviewAsync(
        int resultId, SubmitReviewDto dto, int reviewedByUserId, CancellationToken ct = default);
}

public class OpenQuestionReviewService : IOpenQuestionReviewService
{
    private readonly IExamResultRepository _resultRepo;
    private readonly IResultService _resultService;
    private readonly ReviewWorkflow _workflow;

    public OpenQuestionReviewService(
        IExamResultRepository resultRepo,
        IExamRepository examRepo,
        IRepository<UserAnswer> answerRepo,
        IEmailService emailService,
        IResultService resultService,
        IUnitOfWork unitOfWork)
    {
        _resultRepo = resultRepo;
        _resultService = resultService;
        _workflow = new ReviewWorkflow(resultRepo, examRepo, answerRepo, emailService, unitOfWork);
    }

    public async Task<IReadOnlyList<PendingReviewSummaryDto>> GetPendingAsync(CancellationToken ct = default)
    {
        var pending = await _resultRepo.GetPendingReviewAsync(ct);
        var now = DateTime.UtcNow;

        return pending.Select(r =>
        {
            // Una reserva caducada ya no ocupa el resultado: no se enseña.
            var reserved = r.ReservedByUserId is not null && r.ReservedUntil >= now;
            return new PendingReviewSummaryDto(
                r.Id,
                r.ExamId,
                r.Exam?.Title ?? "",
                r.CandidateName,
                r.CandidateEmail,
                r.CompletedAt,
                Math.Max(0, (int)(now - r.CompletedAt).TotalDays),
                ReviewWorkflow.OpenAnswersOf(r).Count,
                reserved ? r.ReservedByUser?.Name : null,
                reserved ? r.ReservedUntil : null);
        }).ToList();
    }

    public async Task<PendingReviewDetailDto?> GetDetailAsync(
        int resultId, int reviewerId, CancellationToken ct = default)
    {
        var result = await _resultRepo.GetForReviewAsync(resultId, ct);
        if (result is null) return null;

        var until = await _workflow.ReserveAsync(result, reviewerId, revealHolder: true, ct);

        return new PendingReviewDetailDto(
            result.Id,
            result.Exam?.Title ?? "",
            result.CandidateName,
            result.CandidateEmail,
            result.CompletedAt,
            result.TotalPoints,
            result.ObtainedPoints,
            ReviewWorkflow.BuildReviewableAnswers(result),
            until);
    }

    public async Task<DateTime> RenewReservationAsync(int resultId, int reviewerId, CancellationToken ct = default)
    {
        var result = await _resultRepo.GetForReviewAsync(resultId, ct)
            ?? throw new NotFoundException("Resultado no encontrado.");
        return await _workflow.ReserveAsync(result, reviewerId, revealHolder: true, ct);
    }

    public async Task ReleaseReservationAsync(int resultId, CancellationToken ct = default)
        => await _resultRepo.ReleaseAsync(resultId, userId: null, ct);

    public async Task<ExamResultDto> SubmitReviewAsync(
        int resultId, SubmitReviewDto dto, int reviewedByUserId, CancellationToken ct = default)
    {
        var result = await _resultRepo.GetForReviewAsync(resultId, ct)
            ?? throw new InvalidReviewException("Resultado no encontrado.");

        await _workflow.ApplyAsync(result, dto, reviewedByUserId, revealHolder: true, ct);

        return await _resultService.GetDetailAsync(result.Id, ct)
            ?? throw new InvalidOperationException("No se pudo recuperar el resultado corregido.");
    }
}
