namespace TechEval.Application.DTOs;

/// <summary>Entrada de la cola de correcciones pendientes</summary>
public record PendingReviewSummaryDto(
    int ResultId,
    int ExamId,
    string ExamTitle,
    string CandidateName,
    string CandidateEmail,
    DateTime CompletedAt,
    int DaysWaiting,
    int OpenAnswerCount);

/// <summary>Una respuesta abierta a corregir, con la referencia para el corrector</summary>
public record ReviewableAnswerDto(
    int UserAnswerId,
    int QuestionId,
    string QuestionText,
    string? OpenAnswer,
    int MaxPoints,
    int? AwardedPoints,
    string? ReviewerComment,
    string? SampleAnswer);

/// <summary>Detalle completo de la corrección de un resultado pendiente</summary>
public record PendingReviewDetailDto(
    int ResultId,
    string ExamTitle,
    string CandidateName,
    string CandidateEmail,
    DateTime CompletedAt,
    int TotalPoints,
    int AutoScoredPoints,
    List<ReviewableAnswerDto> Answers);

/// <summary>Puntuación otorgada a una respuesta abierta concreta</summary>
public record ReviewAnswerInputDto(
    int UserAnswerId,
    int AwardedPoints,
    string? ReviewerComment);

/// <summary>Corrección atómica: debe cubrir todas las respuestas abiertas del resultado</summary>
public record SubmitReviewDto(
    List<ReviewAnswerInputDto> Answers);
