using TechEval.Domain.Enums;

namespace TechEval.Application.DTOs;

public record CreateQuestionGenerationJobDto(
    int CategoryId,
    DifficultyLevel Difficulty,
    QuestionType Type,
    string Topic,
    int Count);

public record QuestionGenerationJobDto(
    int Id,
    int CategoryId,
    string CategoryName,
    DifficultyLevel Difficulty,
    QuestionType Type,
    string Topic,
    int RequestedCount,
    QuestionGenerationJobStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record QuestionGenerationJobItemDto(
    int Id,
    int JobId,
    string JobTopic,
    QuestionGenerationJobItemStatus Status,
    string? ErrorMessage,
    QuestionDto? Question);

public record QuestionGenerationJobProgressDto(
    int JobId,
    string Topic,
    int CategoryId,
    string CategoryName,
    DifficultyLevel Difficulty,
    QuestionType Type,
    QuestionGenerationJobStatus Status,
    int RequestedCount,
    int PendingCount,
    int SucceededCount,
    int FailedCount,
    DateTime CreatedAt);
