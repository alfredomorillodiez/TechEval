using TechEval.Domain.Enums;

namespace TechEval.Application.DTOs;

/// <summary>
/// Estado completo de una sesión de examen, tanto al empezarla como al reanudarla.
/// `RemainingSeconds` lo calcula el servidor desde `StartedAt`: si lo calculase el cliente,
/// recargar la página devolvería el tiempo completo y la prueba no tendría límite real.
/// `SavedAnswers` reutiliza `SubmitAnswerDto` a propósito — ese record no tiene sitio para
/// `IsCorrect` ni para la puntuación, así que no puede filtrar el solucionario.
/// </summary>
public record ExamSessionInfoDto(
    int SessionId,
    string ExamTitle,
    string CandidateName,
    int TimeLimitMinutes,
    DateTime StartedAt,
    int RemainingSeconds,
    List<SessionQuestionDto> Questions,
    List<SubmitAnswerDto> SavedAnswers);

public record SessionQuestionDto(
    int QuestionId,
    string Text,
    QuestionType Type,
    int Points,
    int Order,
    List<AnswerOptionDto> Answers);

public record SubmitAnswerDto(
    int QuestionId,
    int? SelectedAnswerId,
    string? OpenAnswer);

public record SubmitExamDto(
    int SessionId,
    List<SubmitAnswerDto> Answers);

public record ExamTokenValidationDto(
    bool IsValid,
    string? Error,
    int? SessionId,
    string? ExamTitle,
    string? CandidateName,
    string? AuthToken);

/// <summary>
/// Acuse que recibe el candidato al enviar la prueba. Deliberadamente NO incluye la lista
/// de respuestas: el endpoint de envío es anónimo y devolver el solucionario permitiría
/// extraer las respuestas correctas del banco con una prueba de sacrificio.
/// Cuando el resultado queda pendiente de corrección, tampoco lleva cifras.
/// </summary>
public record ExamSubmissionReceiptDto(
    int ResultId,
    string ExamTitle,
    ExamResultStatus Status,
    int? TotalPoints,
    int? ObtainedPoints,
    decimal? ScorePercentage,
    bool? Passed,
    DateTime CompletedAt);
