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
        var allResults = await _resultRepo.GetAllWithDetailsAsync(ct);
        var allExams = await _examRepo.GetWithStatsAsync(ct);
        var totalQuestions = await _questionRepo.CountAsync(q => q.IsActive, ct);

        var thisMonth = allResults.Where(r =>
            r.CompletedAt.Year == DateTime.UtcNow.Year &&
            r.CompletedAt.Month == DateTime.UtcNow.Month).ToList();

        // Media y tasa de aprobación solo sobre lo ya corregido: un resultado pendiente
        // lleva una puntuación parcial que hundiría la media e inflaría los suspensos.
        var scored = thisMonth.Where(r => r.Status == ExamResultStatus.Reviewed).ToList();

        var avgScore = scored.Any()
            ? Math.Round(scored.Average(r => r.ScorePercentage), 1)
            : 0;
        var passRate = scored.Any()
            ? (int)Math.Round((double)scored.Count(r => r.Passed == true) / scored.Count * 100)
            : 0;

        var pendingReviewCount = allResults.Count(r => r.Status == ExamResultStatus.PendingReview);

        var recent = allResults
            .OrderByDescending(r => r.CompletedAt).Take(10)
            .Select(MapToSummary)
            .ToList();

        return new DashboardStatsDto(
            allExams.Count(e => e.IsActive),
            totalQuestions,
            thisMonth.Count,
            avgScore,
            passRate,
            pendingReviewCount,
            recent);
    }
}
