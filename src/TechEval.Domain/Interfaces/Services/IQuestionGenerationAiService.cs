using TechEval.Domain.Enums;

namespace TechEval.Domain.Interfaces.Services;

public interface IQuestionGenerationAiService
{
    Task<GeneratedQuestionResult> GenerateQuestionAsync(
        string topic,
        string categoryName,
        DifficultyLevel difficulty,
        QuestionType type,
        CancellationToken ct = default);
}

public class GeneratedQuestionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? QuestionText { get; init; }
    public string? SampleAnswer { get; init; }
    public List<GeneratedAnswer> Answers { get; init; } = new();

    public static GeneratedQuestionResult Fail(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };

    public static GeneratedQuestionResult Ok(string questionText, string? sampleAnswer, List<GeneratedAnswer> answers) => new()
    {
        Success = true,
        QuestionText = questionText,
        SampleAnswer = sampleAnswer,
        Answers = answers
    };
}

public class GeneratedAnswer
{
    public string Text { get; init; } = string.Empty;
    public bool IsCorrect { get; init; }
}
