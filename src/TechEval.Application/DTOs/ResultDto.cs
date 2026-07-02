namespace TechEval.Application.DTOs;

public record ExamResultDto(
    int Id,
    string CandidateName,
    string CandidateEmail,
    string ExamTitle,
    int TotalPoints,
    int ObtainedPoints,
    decimal ScorePercentage,
    bool Passed,
    DateTime CompletedAt,
    List<AnswerReviewDto> Answers);

public record AnswerReviewDto(
    string QuestionText,
    string? SelectedAnswerText,
    string? OpenAnswer,
    string? CorrectAnswerText,
    bool? IsCorrect,
    int Points);

public record ExamResultSummaryDto(
    int Id,
    int ExamId,
    string CandidateName,
    string CandidateEmail,
    string ExamTitle,
    decimal ScorePercentage,
    bool Passed,
    DateTime CompletedAt);

public record DashboardStatsDto(
    int TotalExams,
    int TotalQuestions,
    int TotalResultsThisMonth,
    decimal AverageScoreThisMonth,
    int PassRateThisMonth,
    List<ExamResultSummaryDto> RecentResults);

public record AuthResultDto(string Token, string Name, string Email);

public record LoginDto(string Email, string Password);
