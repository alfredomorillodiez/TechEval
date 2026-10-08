using TechEval.Domain.Enums;

namespace TechEval.Application.DTOs;

/// <summary>
/// Invitación que el alumno todavía puede resolver. `InProgress` distingue la prueba sin
/// empezar de la que dejó a medias, para que el portal ofrezca "Comenzar" o "Continuar".
/// </summary>
public record PendingExamDto(string Token, string ExamTitle, DateTime ExpiresAt, bool InProgress);

public record CompletedExamDto(
    int ResultId,
    string ExamTitle,
    decimal? ScorePercentage,
    bool? Passed,
    ExamResultStatus Status,
    DateTime CompletedAt);
