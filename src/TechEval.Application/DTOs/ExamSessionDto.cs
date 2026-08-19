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
