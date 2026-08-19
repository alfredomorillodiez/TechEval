namespace TechEval.Application.DTOs;

public record PendingExamDto(string Token, string ExamTitle, DateTime ExpiresAt);

public record CompletedExamDto(
    int ResultId,
    string ExamTitle,
    decimal ScorePercentage,
    bool Passed,
    DateTime CompletedAt);
