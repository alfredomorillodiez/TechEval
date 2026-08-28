using TechEval.Domain.Enums;

namespace TechEval.Application.DTOs;

public record ExamResultDto(
    int Id,
    string CandidateName,
    string CandidateEmail,
    string ExamTitle,
    int TotalPoints,
    int ObtainedPoints,
    decimal ScorePercentage,
    bool? Passed,
    ExamResultStatus Status,
    DateTime CompletedAt,
    List<AnswerReviewDto> Answers);

public record AnswerReviewDto(
    string QuestionText,
    string? SelectedAnswerText,
    string? OpenAnswer,
    string? CorrectAnswerText,
    bool? IsCorrect,
    int Points,
    int? AwardedPoints,
    string? ReviewerComment);

public record ExamResultSummaryDto(
    int Id,
    int ExamId,
    string CandidateName,
    string CandidateEmail,
    string ExamTitle,
    decimal ScorePercentage,
    bool? Passed,
    ExamResultStatus Status,
    DateTime CompletedAt);

public record DashboardStatsDto(
    int TotalExams,
    int TotalQuestions,
    int TotalResultsThisMonth,
    decimal AverageScoreThisMonth,
    int PassRateThisMonth,
    int PendingReviewCount,
    List<ExamResultSummaryDto> RecentResults);

public record AuthResultDto(string Token, string Name, string Email, bool IsAdmin);

public record LoginDto(string Email, string Password);
