using TechEval.Domain.Enums;

namespace TechEval.Application.DTOs;

public record ExamSessionInfoDto(
    int SessionId,
    string ExamTitle,
    string CandidateName,
    int TimeLimitMinutes,
    DateTime StartedAt,
    List<SessionQuestionDto> Questions);

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
