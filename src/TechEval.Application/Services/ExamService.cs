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
            throw new ValidationException("Algunas preguntas no existen o están inactivas.");

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
        IReadOnlyList<Question> questions;
        if (dto.DifficultyPercentages is null)
        {
            // Sin reparto, lo de siempre: un nivel único o todos, y rechazo si no alcanza.
            questions = await _questionRepo.GetRandomAsync(
                dto.QuestionCount, dto.CategoryIds, dto.Difficulty, ct);

            if (questions.Count < dto.QuestionCount)
                throw new ValidationException(
                    $"No hay suficientes preguntas disponibles. Se encontraron {questions.Count} de {dto.QuestionCount} solicitadas.");
        }
        else
        {
            if (dto.Difficulty is not null)
                throw new ValidationException("Indica un nivel único o un reparto por nivel, no los dos.");
            questions = await PickByDistributionAsync(dto, ct);
        }

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

    /// <summary>
    /// Preguntas al azar según el reparto por nivel. Si un nivel no alcanza, se rechaza: nunca
    /// se completa con otro nivel ni con otra categoría. Spec exam-management.
    /// </summary>
    private async Task<IReadOnlyList<Question>> PickByDistributionAsync(GenerateExamDto dto, CancellationToken ct)
    {
        var available = await _questionRepo.CountByDifficultyAsync(dto.CategoryIds, ct);
        var plan = DifficultyPlanner.Plan(dto.QuestionCount, dto.DifficultyPercentages!, available);
        if (!plan.CanGenerate)
            throw new ValidationException(ShortageMessage(plan.Shortages));

        var picked = new List<Question>();
        foreach (var (level, count) in plan.Requested.Where(r => r.Value > 0))
        {
            var ofLevel = await _questionRepo.GetRandomAsync(count, dto.CategoryIds, level, ct);
            // La disponibilidad se contó en otra consulta: si alguien dio de baja una pregunta
            // entretanto, este nivel ya no alcanza. Es raro, y el reintento lo resuelve.
            if (ofLevel.Count < count)
                throw new ValidationException(ShortageMessage(new[] { new LevelShortage(level, count, ofLevel.Count) }));
            picked.AddRange(ofLevel);
        }

        // Mezcladas: elegir por nivel las dejaría agrupadas en el orden de la prueba.
        var shuffled = picked.ToArray();
        Random.Shared.Shuffle(shuffled);
        return shuffled;
    }

    private static string ShortageMessage(IEnumerable<LevelShortage> shortages)
        => "No hay suficientes preguntas para el reparto: " + string.Join("; ", shortages.Select(s =>
            $"nivel {LevelName(s.Level)}, se necesitan {s.Needed} y hay {s.Available}")) + ".";

    private static string LevelName(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Basic => "básico",
        DifficultyLevel.Intermediate => "intermedio",
        _ => "avanzado"
    };

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
