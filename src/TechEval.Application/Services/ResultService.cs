using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
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
        return results.Select(r => new ExamResultSummaryDto(
            r.Id, r.ExamId, r.CandidateName, r.CandidateEmail,
            r.Exam?.Title ?? "", r.ScorePercentage, r.Passed, r.CompletedAt)).ToList();
    }

    public async Task<IReadOnlyList<ExamResultSummaryDto>> GetByExamAsync(int examId, CancellationToken ct = default)
    {
        var results = await _resultRepo.GetByExamAsync(examId, ct);
        return results.Select(r => new ExamResultSummaryDto(
            r.Id, r.ExamId, r.CandidateName, r.CandidateEmail,
            r.Exam?.Title ?? "", r.ScorePercentage, r.Passed, r.CompletedAt)).ToList();
    }

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
            return new AnswerReviewDto(
                ua.Question?.Text ?? "",
                ua.SelectedAnswer?.Text,
                ua.OpenAnswer,
                correct?.Text,
                ua.IsCorrect,
                ua.Question?.Points ?? 0);
        }).ToList();

        return new ExamResultDto(
            result.Id, result.CandidateName, result.CandidateEmail,
            result.Exam?.Title ?? "",
            result.TotalPoints, result.ObtainedPoints,
            result.ScorePercentage, result.Passed,
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

        var avgScore = thisMonth.Any()
            ? Math.Round(thisMonth.Average(r => r.ScorePercentage), 1)
            : 0;
        var passRate = thisMonth.Any()
            ? (int)Math.Round((double)thisMonth.Count(r => r.Passed) / thisMonth.Count * 100)
            : 0;

        var recent = allResults
            .OrderByDescending(r => r.CompletedAt).Take(10)
            .Select(r => new ExamResultSummaryDto(
                r.Id, r.ExamId, r.CandidateName, r.CandidateEmail,
                r.Exam?.Title ?? "", r.ScorePercentage, r.Passed, r.CompletedAt))
            .ToList();

        return new DashboardStatsDto(
            allExams.Count(e => e.IsActive),
            totalQuestions,
            thisMonth.Count,
            avgScore,
            passRate,
            recent);
    }
}
