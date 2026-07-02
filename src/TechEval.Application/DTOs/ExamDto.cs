using TechEval.Domain.Enums;

namespace TechEval.Application.DTOs;

public record ExamDto(
    int Id,
    string Title,
    string Description,
    int TimeLimitMinutes,
    int PassingScorePercentage,
    bool IsActive,
    DateTime CreatedAt,
    int QuestionCount,
    int TotalPoints,
    List<ExamQuestionDto> Questions);

public record ExamQuestionDto(
    int Id,
    int QuestionId,
    string QuestionText,
    QuestionType QuestionType,
    DifficultyLevel Difficulty,
    int Points,
    int Order,
    List<AdminAnswerOptionDto> Answers);

// Respuesta para el panel de administración (incluye la respuesta correcta)
public record AdminAnswerOptionDto(int Id, string Text, bool IsCorrect, int Order);

// Respuesta sin indicar cuál es correcta (para candidatos)
public record AnswerOptionDto(int Id, string Text, int Order);

public record ExamSummaryDto(
    int Id,
    string Title,
    int TimeLimitMinutes,
    int QuestionCount,
    int TotalPoints,
    int PassingScorePercentage,
    bool IsActive,
    DateTime CreatedAt,
    int TokensSent,
    int ResultsCount);

public record CreateExamDto(
    string Title,
    string Description,
    int TimeLimitMinutes,
    int PassingScorePercentage,
    List<int> QuestionIds);

public record GenerateExamDto(
    string Title,
    string Description,
    int TimeLimitMinutes,
    int PassingScorePercentage,
    int QuestionCount,
    List<int>? CategoryIds,
    DifficultyLevel? Difficulty);

public record SendExamDto(
    int ExamId,
    string CandidateName,
    string CandidateEmail,
    int ExpirationHours = 72);

public record UpdateExamDto(
    string Title,
    string Description,
    int TimeLimitMinutes,
    int PassingScorePercentage,
    bool IsActive);

public record BulkCandidateDto(string Name, string Email);

public record BulkSendExamDto(
    int ExamId,
    List<BulkCandidateDto> Candidates,
    int ExpirationHours = 72);

public record BulkSendItemResultDto(
    string CandidateName,
    string CandidateEmail,
    bool Success,
    string? Error);

public record BulkSendResultDto(
    int Sent,
    int Failed,
    List<BulkSendItemResultDto> Results);
