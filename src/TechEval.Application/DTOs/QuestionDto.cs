using TechEval.Domain.Enums;

namespace TechEval.Application.DTOs;

public record AnswerDto(int Id, string Text, bool IsCorrect, int Order);

public record QuestionDto(
    int Id,
    string Text,
    QuestionType Type,
    DifficultyLevel Difficulty,
    int CategoryId,
    string CategoryName,
    int Points,
    bool IsActive,
    string? SampleAnswer,
    List<AnswerDto> Answers,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateQuestionDto(
    string Text,
    QuestionType Type,
    DifficultyLevel Difficulty,
    int CategoryId,
    int Points,
    string? SampleAnswer,
    List<CreateAnswerDto> Answers);

public record CreateAnswerDto(string Text, bool IsCorrect, int Order);

public record UpdateQuestionDto(
    string Text,
    QuestionType Type,
    DifficultyLevel Difficulty,
    int CategoryId,
    int Points,
    bool IsActive,
    string? SampleAnswer,
    List<CreateAnswerDto> Answers);

public record QuestionSummaryDto(
    int Id,
    string Text,
    QuestionType Type,
    DifficultyLevel Difficulty,
    string CategoryName,
    int Points,
    bool IsActive);
