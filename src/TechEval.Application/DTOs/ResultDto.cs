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
    List<AnswerReviewDto> Answers,
    string? ReviewedByName = null,
    DateTime? ReviewedAt = null);

public record AnswerReviewDto(
    string QuestionText,
    string? SelectedAnswerText,
    string? OpenAnswer,
    string? CorrectAnswerText,
    bool? IsCorrect,
    int Points,
    int? AwardedPoints,
    string? ReviewerComment,
    // Todas las opciones, en el orden del examen. Null en las preguntas abiertas y cuando
    // la pregunta se editó después del examen: entonces solo valen las copias de arriba.
    List<ResultOptionDto>? Options = null);

public record ResultOptionDto(string Text, bool IsSelected, bool IsCorrect);

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

/// <summary>
/// Cifras de los resultados, sobre todos o sobre los de una prueba (<c>ExamTitle</c> solo sale con
/// prueba). No dependen de ningún filtro del listado. Notas y distribución, solo de los corregidos.
/// La distribución son los tramos 0–49, 50–59, 60–69, 70–79, 80–89 y 90–100.
/// </summary>
public record ResultStatsDto(
    int Total,
    int Reviewed,
    int Pending,
    int Passed,
    int PassRate,
    decimal AverageScore,
    decimal MaxScore,
    decimal MinScore,
    IReadOnlyList<int> Distribution,
    string? ExamTitle);

public record DashboardStatsDto(
    int TotalExams,
    int TotalQuestions,
    int TotalResultsThisMonth,
    decimal AverageScoreThisMonth,
    int PassRateThisMonth,
    int PendingReviewCount,
    List<ExamResultSummaryDto> RecentResults,
    List<ExamRefDto>? ExamsWithoutEvaluator = null);

/// <summary>Una prueba, solo para enlazarla.</summary>
public record ExamRefDto(int Id, string Title);

public record AuthResultDto(string Token, string Name, string Email, UserRole Role);

public record LoginDto(string Email, string Password);
