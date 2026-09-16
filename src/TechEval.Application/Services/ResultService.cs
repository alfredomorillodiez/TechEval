using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Application.Services;

public interface IResultService
{
    Task<IReadOnlyList<ExamResultSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExamResultSummaryDto>> GetByExamAsync(int examId, CancellationToken ct = default);
    Task<ExamResultDto?> GetDetailAsync(int id, CancellationToken ct = default);
    Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken ct = default);
}

public class ResultService : IResultService
{
    private readonly IExamResultRepository _resultRepo;
    private readonly IExamRepository _examRepo;
    private readonly IQuestionRepository _questionRepo;

    public ResultService(
        IExamResultRepository resultRepo,
        IExamRepository examRepo,
        IQuestionRepository questionRepo)
    {
        _resultRepo = resultRepo;
        _examRepo = examRepo;
        _questionRepo = questionRepo;
    }

    public async Task<IReadOnlyList<ExamResultSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var results = await _resultRepo.GetAllWithDetailsAsync(ct);
        return results.Select(MapToSummary).ToList();
    }

    public async Task<IReadOnlyList<ExamResultSummaryDto>> GetByExamAsync(int examId, CancellationToken ct = default)
    {
        var results = await _resultRepo.GetByExamAsync(examId, ct);
        return results.Select(MapToSummary).ToList();
    }

    private static ExamResultSummaryDto MapToSummary(ExamResult r) => new(
        r.Id, r.ExamId, r.CandidateName, r.CandidateEmail,
        r.Exam?.Title ?? "", r.ScorePercentage, r.Passed, r.Status, r.CompletedAt);

    public async Task<ExamResultDto?> GetDetailAsync(int id, CancellationToken ct = default)
    {
        var result = await _resultRepo.GetWithDetailsAsync(id, ct);
        if (result is null) return null;

        var deduped = result.ExamSession?.UserAnswers
            .GroupBy(ua => ua.QuestionId)
            .Select(g => g.OrderByDescending(ua => ua.IsCorrect.HasValue).ThenByDescending(ua => ua.Id).First())
            .ToList() ?? new List<UserAnswer>();

        var answerReviews = deduped.Select(ua =>
        {
            var correct = ua.Question?.Answers.FirstOrDefault(a => a.IsCorrect);
            // Lo que se le preguntó al candidato, no lo que la pregunta dice hoy. Se
            // recurre al banco solo para las respuestas anteriores a la copia, que el
            // guion de esquema rellena con el texto de hoy de todas formas.
            return new AnswerReviewDto(
                ua.QuestionTextSnapshot ?? ua.Question?.Text ?? "",
                ua.SelectedAnswerTextSnapshot ?? ua.SelectedAnswer?.Text,
                ua.OpenAnswer,
                ua.CorrectAnswerTextSnapshot ?? correct?.Text,
                ua.IsCorrect,
                ua.QuestionPointsSnapshot ?? ua.Question?.Points ?? 0,
                ua.AwardedPoints,
                ua.ReviewerComment);
        }).ToList();

        return new ExamResultDto(
            result.Id, result.CandidateName, result.CandidateEmail,
            result.Exam?.Title ?? "",
            result.TotalPoints, result.ObtainedPoints,
            result.ScorePercentage, result.Passed, result.Status,
            result.CompletedAt, answerReviews);
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        // El rango del mes se calcula una vez, aquí. Antes el filtro comparaba año y mes
        // contra el reloj dentro de la consulta, y eso no puede usar el índice de
        // CompletedAt. Un rango sí.
        var ahora = DateTime.UtcNow;
        var desde = new DateTime(ahora.Year, ahora.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var hasta = desde.AddMonths(1);

        // Cuatro consultas agregadas en lugar de traerse el histórico entero y el árbol
        // completo de exámenes para contarlo en memoria.
        var mes = await _resultRepo.GetPeriodStatsAsync(desde, hasta, ct);
        var activeExams = await _examRepo.CountActiveAsync(ct);
        var totalQuestions = await _questionRepo.CountAsync(q => q.IsActive, ct);
        var pendingReviewCount = await _resultRepo.CountPendingReviewAsync(ct);
        var recent = await _resultRepo.GetRecentAsync(10, ct);

        // Media y tasa de aprobación solo sobre lo ya corregido: un resultado pendiente
        // lleva una puntuación parcial que hundiría la media e inflaría los suspensos.
        var avgScore = mes.Scored > 0
            ? Math.Round(mes.ScoreSum / mes.Scored, 1)
            : 0;
        var passRate = mes.Scored > 0
            ? (int)Math.Round((double)mes.Passed / mes.Scored * 100)
            : 0;

        return new DashboardStatsDto(
            activeExams,
            totalQuestions,
            mes.Total,
            avgScore,
            passRate,
            pendingReviewCount,
            recent.Select(MapToSummary).ToList());
    }
}
