namespace TechEval.Application.DTOs;

// Lo que recibe el evaluador. Ningún tipo tiene sitio para el nombre ni el email del
// candidato, a propósito: un campo que existe acaba rellenándose por error; uno que no
// existe, no. Las fechas son DateOnly para que la hora, que ayuda a identificar a quien hizo
// la prueba, no pueda viajar.

/// <summary>Entrada de la cola del evaluador.</summary>
/// <param name="ReservedByOther">Otra persona la está corrigiendo; no se dice quién.</param>
public record EvaluatorQueueItemDto(
    int ResultId,
    string ExamTitle,
    string CandidateAlias,
    DateOnly CompletedOn,
    int DaysWaiting,
    int OpenAnswerCount,
    bool ReservedByOther,
    DateTime? ReservedUntil);

/// <summary>Detalle de corrección a ciegas. Pedirlo reserva el resultado.</summary>
public record EvaluatorReviewDetailDto(
    int ResultId,
    string ExamTitle,
    string CandidateAlias,
    DateOnly CompletedOn,
    int TotalPoints,
    int AutoScoredPoints,
    List<ReviewableAnswerDto> Answers,
    DateTime ReservedUntil);

/// <summary>Respuesta del envío de una corrección.</summary>
public record EvaluatorReviewOutcomeDto(
    int ResultId,
    string CandidateAlias,
    int ObtainedPoints,
    int TotalPoints,
    decimal ScorePercentage,
    bool Passed);

/// <summary>Una corrección propia, en el historial.</summary>
public record EvaluatorHistoryItemDto(
    int ResultId,
    string ExamTitle,
    string CandidateAlias,
    DateOnly ReviewedOn,
    decimal ScorePercentage,
    bool Passed);

/// <summary>Una respuesta abierta de una corrección propia, en solo lectura.</summary>
public record EvaluatorHistoryAnswerDto(
    string QuestionText,
    string? OpenAnswer,
    int MaxPoints,
    int? AwardedPoints,
    string? ReviewerComment);

public record EvaluatorHistoryDetailDto(
    int ResultId,
    string ExamTitle,
    string CandidateAlias,
    DateOnly ReviewedOn,
    int ObtainedPoints,
    int TotalPoints,
    decimal ScorePercentage,
    bool Passed,
    List<EvaluatorHistoryAnswerDto> Answers);

/// <summary>Fin de una reserva renovada.</summary>
public record ReservationDto(DateTime ReservedUntil);
