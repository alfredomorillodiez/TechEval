using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Application.Services;

public interface IQuestionService
{
    Task<IReadOnlyList<QuestionSummaryDto>> GetAllAsync(int? categoryId, DifficultyLevel? difficulty, QuestionType? type, CancellationToken ct = default);
    Task<QuestionDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<QuestionDto> CreateAsync(CreateQuestionDto dto, CancellationToken ct = default);
    Task<QuestionDto?> UpdateAsync(int id, UpdateQuestionDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class QuestionService : IQuestionService
{
    private readonly IQuestionRepository _repo;

    public QuestionService(IQuestionRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<QuestionSummaryDto>> GetAllAsync(
        int? categoryId, DifficultyLevel? difficulty, QuestionType? type, CancellationToken ct = default)
    {
        var questions = await _repo.GetFilteredAsync(categoryId, difficulty, type, true, ct);
        return questions.Select(q => new QuestionSummaryDto(
            q.Id, q.Text, q.Type, q.Difficulty,
            q.Category?.Name ?? "", q.Points, q.IsActive)).ToList();
    }

    public async Task<QuestionDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var q = await _repo.GetWithAnswersAsync(id, ct);
        return q is null ? null : MapToDto(q);
    }

    public async Task<QuestionDto> CreateAsync(CreateQuestionDto dto, CancellationToken ct = default)
    {
        ValidateAnswers(dto.Type, dto.Answers);

        var question = new Question
        {
            Text = dto.Text,
            Type = dto.Type,
            Difficulty = dto.Difficulty,
            CategoryId = dto.CategoryId,
            Points = dto.Points,
            SampleAnswer = dto.SampleAnswer,
            Answers = dto.Answers.Select((a, i) => new Answer
            {
                Text = a.Text,
                IsCorrect = a.IsCorrect,
                Order = a.Order > 0 ? a.Order : i + 1
            }).ToList()
        };

        await _repo.AddAsync(question, ct);
        return MapToDto(question);
    }

    public async Task<QuestionDto?> UpdateAsync(int id, UpdateQuestionDto dto, CancellationToken ct = default)
    {
        var question = await _repo.GetWithAnswersAsync(id, ct);
        if (question is null) return null;

        ValidateAnswers(dto.Type, dto.Answers);

        question.Text = dto.Text;
        question.Type = dto.Type;
        question.Difficulty = dto.Difficulty;
        question.CategoryId = dto.CategoryId;
        question.Points = dto.Points;
        question.IsActive = dto.IsActive;
        question.SampleAnswer = dto.SampleAnswer;
        question.UpdatedAt = DateTime.UtcNow;
        question.Answers.Clear();
        foreach (var (a, i) in dto.Answers.Select((a, i) => (a, i)))
        {
            question.Answers.Add(new Answer
            {
                Text = a.Text,
                IsCorrect = a.IsCorrect,
                Order = a.Order > 0 ? a.Order : i + 1,
                QuestionId = question.Id
            });
        }

        await _repo.UpdateAsync(question, ct);
        return MapToDto(question);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var question = await _repo.GetByIdAsync(id, ct);
        if (question is null) return false;
        question.IsActive = false;
        question.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(question, ct);
        return true;
    }

    private static void ValidateAnswers(QuestionType type, List<CreateAnswerDto> answers)
    {
        if (type == QuestionType.MultipleChoice)
        {
            if (answers.Count != 4)
                throw new InvalidOperationException("Las preguntas tipo test deben tener exactamente 4 respuestas.");
            if (answers.Count(a => a.IsCorrect) != 1)
                throw new InvalidOperationException("Las preguntas tipo test deben tener exactamente 1 respuesta correcta.");
        }
    }

    private static QuestionDto MapToDto(Question q) => new(
        q.Id, q.Text, q.Type, q.Difficulty,
        q.CategoryId, q.Category?.Name ?? "",
        q.Points, q.IsActive, q.SampleAnswer,
        q.Answers.OrderBy(a => a.Order)
            .Select(a => new AnswerDto(a.Id, a.Text, a.IsCorrect, a.Order))
            .ToList());
}
