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

/// <summary>
/// Opción en una actualización. `Id` con valor identifica una opción existente, que se
/// actualiza en su sitio; sin valor, es una opción nueva. El emparejamiento va por
/// identificador y no por posición: reordenar las opciones haría que el emparejamiento
/// posicional asignase a una opción el texto de otra, y las respuestas ya registradas
/// pasarían a apuntar a algo que el candidato no eligió.
/// </summary>
public record UpdateAnswerDto(int? Id, string Text, bool IsCorrect, int Order);

public record UpdateQuestionDto(
    string Text,
    QuestionType Type,
    DifficultyLevel Difficulty,
    int CategoryId,
    int Points,
    bool IsActive,
    string? SampleAnswer,
    List<UpdateAnswerDto> Answers);

public record QuestionSummaryDto(
    int Id,
    string Text,
    QuestionType Type,
    DifficultyLevel Difficulty,
    string CategoryName,
    int Points,
    bool IsActive);
