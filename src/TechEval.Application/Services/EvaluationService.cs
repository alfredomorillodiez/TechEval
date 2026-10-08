using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Application.Services;

/// <summary>
/// La corrección del evaluador, a ciegas. Spec evaluator-review.
/// </summary>
/// <remarks>
/// Todo lo que sale de aquí usa los DTO del evaluador, que no tienen dónde llevar el nombre
/// ni el email del candidato. El acceso se decide en una sola consulta del repositorio
/// (prueba asignada y resultado que no es suyo como candidato); si no pasa, la respuesta es
/// «no encontrado», sin distinguir si no existe, no está asignado o es suyo.
/// </remarks>
public interface IEvaluationService
{
    Task<IReadOnlyList<EvaluatorQueueItemDto>> GetQueueAsync(int evaluatorId, CancellationToken ct = default);
    Task<EvaluatorReviewDetailDto> GetDetailAsync(int resultId, int evaluatorId, CancellationToken ct = default);
    Task<ReservationDto> RenewReservationAsync(int resultId, int evaluatorId, CancellationToken ct = default);
    Task ReleaseReservationAsync(int resultId, int evaluatorId, CancellationToken ct = default);
    Task<EvaluatorReviewOutcomeDto> SubmitAsync(int resultId, SubmitReviewDto dto, int evaluatorId, CancellationToken ct = default);
    Task<IntegrityReportDto> GetIntegrityAsync(int resultId, int evaluatorId, CancellationToken ct = default);
    Task<IReadOnlyList<EvaluatorHistoryItemDto>> GetHistoryAsync(int evaluatorId, CancellationToken ct = default);
    Task<EvaluatorHistoryDetailDto> GetHistoryDetailAsync(int resultId, int evaluatorId, CancellationToken ct = default);
}

public class EvaluationService : IEvaluationService
{
    private const string NotFound = "Resultado no encontrado.";

    private readonly IExamResultRepository _resultRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IExamIntegrityService _integrity;
    private readonly ReviewWorkflow _workflow;

    public EvaluationService(
        IExamResultRepository resultRepo,
        IExamRepository examRepo,
        IRepository<UserAnswer> answerRepo,
        IRepository<User> userRepo,
        IEmailService emailService,
        IExamIntegrityService integrity,
        IUnitOfWork unitOfWork)
    {
        _resultRepo = resultRepo;
        _userRepo = userRepo;
        _integrity = integrity;
        _workflow = new ReviewWorkflow(resultRepo, examRepo, answerRepo, emailService, unitOfWork);
    }

    public async Task<IReadOnlyList<EvaluatorQueueItemDto>> GetQueueAsync(int evaluatorId, CancellationToken ct = default)
    {
        var email = await EmailOfAsync(evaluatorId, ct);
        var pending = await _resultRepo.GetPendingForEvaluatorAsync(evaluatorId, email, ct);
        var now = DateTime.UtcNow;

        return pending.Select(r =>
        {
            var byOther = r.ReservedByUserId is not null && r.ReservedByUserId != evaluatorId && r.ReservedUntil >= now;
            return new EvaluatorQueueItemDto(
                r.Id,
                r.Exam?.Title ?? "",
                CandidateAlias.For(r.Id),
                DateOnly.FromDateTime(r.CompletedAt),
                Math.Max(0, (int)(now - r.CompletedAt).TotalDays),
                ReviewWorkflow.OpenAnswersOf(r).Count,
                byOther,
                byOther ? r.ReservedUntil : null);
        }).ToList();
    }

    public async Task<EvaluatorReviewDetailDto> GetDetailAsync(int resultId, int evaluatorId, CancellationToken ct = default)
    {
        var result = await GetVisibleAsync(resultId, evaluatorId, ct);
        var until = await _workflow.ReserveAsync(result, evaluatorId, revealHolder: false, ct);

        return new EvaluatorReviewDetailDto(
            result.Id,
            result.Exam?.Title ?? "",
            CandidateAlias.For(result.Id),
            DateOnly.FromDateTime(result.CompletedAt),
            result.TotalPoints,
            result.ObtainedPoints,
            ReviewWorkflow.BuildReviewableAnswers(result),
            until);
    }

    public async Task<ReservationDto> RenewReservationAsync(int resultId, int evaluatorId, CancellationToken ct = default)
    {
        var result = await GetVisibleAsync(resultId, evaluatorId, ct);
        return new ReservationDto(await _workflow.ReserveAsync(result, evaluatorId, revealHolder: false, ct));
    }

    public async Task ReleaseReservationAsync(int resultId, int evaluatorId, CancellationToken ct = default)
    {
        await GetVisibleAsync(resultId, evaluatorId, ct);
        // Solo la suya: liberar la de otro es cosa del administrador.
        await _resultRepo.ReleaseAsync(resultId, evaluatorId, ct);
    }

    public async Task<EvaluatorReviewOutcomeDto> SubmitAsync(
        int resultId, SubmitReviewDto dto, int evaluatorId, CancellationToken ct = default)
    {
        // La asignación se comprueba otra vez aquí, no solo al abrir: el administrador puede
        // haberla quitado mientras la pantalla seguía abierta.
        var result = await GetVisibleAsync(resultId, evaluatorId, ct);
        var outcome = await _workflow.ApplyAsync(result, dto, evaluatorId, revealHolder: false, ct);

        return new EvaluatorReviewOutcomeDto(
            result.Id, CandidateAlias.For(result.Id),
            outcome.ObtainedPoints, outcome.TotalPoints, outcome.ScorePercentage, outcome.Passed);
    }

    public async Task<IntegrityReportDto> GetIntegrityAsync(int resultId, int evaluatorId, CancellationToken ct = default)
    {
        await GetVisibleAsync(resultId, evaluatorId, ct);
        return await _integrity.GetEvaluatorReportAsync(resultId, ct);
    }

    public async Task<IReadOnlyList<EvaluatorHistoryItemDto>> GetHistoryAsync(int evaluatorId, CancellationToken ct = default)
        => (await _resultRepo.GetReviewedByAsync(evaluatorId, ct))
            .Select(r => new EvaluatorHistoryItemDto(
                r.Id,
                r.Exam?.Title ?? "",
                CandidateAlias.For(r.Id),
                DateOnly.FromDateTime(r.ReviewedAt ?? r.CompletedAt),
                r.ScorePercentage,
                r.Passed ?? false))
            .ToList();

    public async Task<EvaluatorHistoryDetailDto> GetHistoryDetailAsync(
        int resultId, int evaluatorId, CancellationToken ct = default)
    {
        // Por ReviewedByUserId y no por la asignación: lo corregido es suyo aunque ya no
        // tenga asignada la prueba.
        var result = (await _resultRepo.GetReviewedByAsync(evaluatorId, ct)).FirstOrDefault(r => r.Id == resultId)
            ?? throw new NotFoundException(NotFound);

        var answers = SessionOrder.ByExamOrder(ReviewWorkflow.OpenAnswersOf(result), result.Exam)
            .Select(ua => new EvaluatorHistoryAnswerDto(
                ua.QuestionTextSnapshot ?? ua.Question?.Text ?? "",
                ua.OpenAnswer,
                ReviewWorkflow.MaxPointsOf(ua),
                ua.AwardedPoints,
                ua.ReviewerComment))
            .ToList();

        return new EvaluatorHistoryDetailDto(
            result.Id,
            result.Exam?.Title ?? "",
            CandidateAlias.For(result.Id),
            DateOnly.FromDateTime(result.ReviewedAt ?? result.CompletedAt),
            result.ObtainedPoints,
            result.TotalPoints,
            result.ScorePercentage,
            result.Passed ?? false,
            answers);
    }

    private async Task<ExamResult> GetVisibleAsync(int resultId, int evaluatorId, CancellationToken ct)
    {
        var email = await EmailOfAsync(evaluatorId, ct);
        return await _resultRepo.GetForEvaluatorAsync(resultId, evaluatorId, email, ct)
               ?? throw new NotFoundException(NotFound);
    }

    // De la base y no del token: el token lleva el email del momento en que se emitió.
    private async Task<string> EmailOfAsync(int evaluatorId, CancellationToken ct)
        => (await _userRepo.GetByIdAsync(evaluatorId, ct))?.Email
           ?? throw new NotFoundException(NotFound);
}
