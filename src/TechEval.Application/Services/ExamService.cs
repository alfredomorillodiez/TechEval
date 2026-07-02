using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Application.Services;

public interface IExamService
{
    Task<IReadOnlyList<ExamSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<ExamDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ExamDto> CreateAsync(CreateExamDto dto, int createdByUserId, CancellationToken ct = default);
    Task<ExamDto> GenerateAsync(GenerateExamDto dto, int createdByUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<ExamDto?> UpdateAsync(int id, UpdateExamDto dto, CancellationToken ct = default);
}

public class ExamService : IExamService
{
    private readonly IExamRepository _examRepo;
    private readonly IQuestionRepository _questionRepo;

    public ExamService(IExamRepository examRepo, IQuestionRepository questionRepo)
    {
        _examRepo = examRepo;
        _questionRepo = questionRepo;
    }

    public async Task<IReadOnlyList<ExamSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var exams = await _examRepo.GetWithStatsAsync(ct);
        return exams.Select(e => new ExamSummaryDto(
            e.Id, e.Title, e.TimeLimitMinutes,
            e.ExamQuestions.Count,
            e.ExamQuestions.Sum(eq => eq.Question?.Points ?? 0),
            e.PassingScorePercentage,
            e.IsActive, e.CreatedAt,
            e.ExamTokens.Count,
            e.ExamTokens.Count(t => t.ExamSession?.ExamResult != null))).ToList();
    }

    public async Task<ExamDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var exam = await _examRepo.GetWithQuestionsAsync(id, ct);
        return exam is null ? null : MapToDto(exam);
    }

    public async Task<ExamDto> CreateAsync(CreateExamDto dto, int createdByUserId, CancellationToken ct = default)
    {
        var questions = await _questionRepo.FindAsync(q => dto.QuestionIds.Contains(q.Id) && q.IsActive, ct);
        if (questions.Count != dto.QuestionIds.Count)
            throw new InvalidOperationException("Algunas preguntas no existen o están inactivas.");

        var exam = new Exam
        {
            Title = dto.Title,
            Description = dto.Description,
            TimeLimitMinutes = dto.TimeLimitMinutes,
            PassingScorePercentage = dto.PassingScorePercentage,
            CreatedByUserId = createdByUserId,
            ExamQuestions = dto.QuestionIds.Select((qid, idx) => new ExamQuestion
            {
                QuestionId = qid,
                Order = idx + 1
            }).ToList()
        };

        await _examRepo.AddAsync(exam, ct);
        var created = await _examRepo.GetWithQuestionsAsync(exam.Id, ct);
        return MapToDto(created!);
    }

    public async Task<ExamDto> GenerateAsync(GenerateExamDto dto, int createdByUserId, CancellationToken ct = default)
    {
        var questions = await _questionRepo.GetRandomAsync(
            dto.QuestionCount, dto.CategoryIds, dto.Difficulty, ct);

        if (questions.Count < dto.QuestionCount)
            throw new InvalidOperationException(
                $"No hay suficientes preguntas disponibles. Se encontraron {questions.Count} de {dto.QuestionCount} solicitadas.");

        var exam = new Exam
        {
            Title = dto.Title,
            Description = dto.Description,
            TimeLimitMinutes = dto.TimeLimitMinutes,
            PassingScorePercentage = dto.PassingScorePercentage,
            CreatedByUserId = createdByUserId,
            ExamQuestions = questions.Select((q, idx) => new ExamQuestion
            {
                QuestionId = q.Id,
                Order = idx + 1
            }).ToList()
        };

        await _examRepo.AddAsync(exam, ct);
        var created = await _examRepo.GetWithQuestionsAsync(exam.Id, ct);
        return MapToDto(created!);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var exam = await _examRepo.GetByIdAsync(id, ct);
        if (exam is null) return false;
        exam.IsActive = false;
        exam.UpdatedAt = DateTime.UtcNow;
        await _examRepo.UpdateAsync(exam, ct);
        return true;
    }

    public async Task<ExamDto?> UpdateAsync(int id, UpdateExamDto dto, CancellationToken ct = default)
    {
        var exam = await _examRepo.GetByIdAsync(id, ct);
        if (exam is null) return null;
        exam.Title = dto.Title;
        exam.Description = dto.Description;
        exam.TimeLimitMinutes = dto.TimeLimitMinutes;
        exam.PassingScorePercentage = dto.PassingScorePercentage;
        exam.IsActive = dto.IsActive;
        exam.UpdatedAt = DateTime.UtcNow;
        await _examRepo.UpdateAsync(exam, ct);
        var updated = await _examRepo.GetWithQuestionsAsync(exam.Id, ct);
        return updated is null ? null : MapToDto(updated);
    }

    private static ExamDto MapToDto(Exam e) => new(
        e.Id, e.Title, e.Description, e.TimeLimitMinutes,
        e.PassingScorePercentage, e.IsActive, e.CreatedAt,
        e.ExamQuestions.Count,
        e.ExamQuestions.Sum(eq => eq.Question?.Points ?? 0),
        e.ExamQuestions.OrderBy(eq => eq.Order).Select(eq => new ExamQuestionDto(
            eq.Id, eq.QuestionId,
            eq.Question?.Text ?? "",
            eq.Question?.Type ?? QuestionType.MultipleChoice,
            eq.Question?.Difficulty ?? DifficultyLevel.Basic,
            eq.Question?.Points ?? 0,
            eq.Order,
            eq.Question?.Answers.OrderBy(a => a.Order)
                .Select(a => new AdminAnswerOptionDto(a.Id, a.Text, a.IsCorrect, a.Order)).ToList()
            ?? new List<AdminAnswerOptionDto>())).ToList());
}
