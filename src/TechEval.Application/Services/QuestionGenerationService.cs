using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Application.Services;

public interface IQuestionGenerationService
{
    Task<QuestionGenerationJobDto> CreateJobAsync(
        CreateQuestionGenerationJobDto dto, int createdByUserId, CancellationToken ct = default);
    Task<IReadOnlyList<QuestionGenerationJobItemDto>> GetPendingReviewItemsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<QuestionGenerationJobProgressDto>> GetActiveJobsProgressAsync(CancellationToken ct = default);
    Task<QuestionDto> ApproveAsync(int itemId, CancellationToken ct = default);
    Task<bool> RejectAsync(int itemId, CancellationToken ct = default);
}

public class QuestionGenerationService : IQuestionGenerationService
{
    private const int MaxCount = 20;

    private readonly IQuestionGenerationJobRepository _jobRepo;
    private readonly IQuestionRepository _questionRepo;
    private readonly IRepository<Category> _categoryRepo;
    private readonly IBackgroundTaskQueue _queue;

    public QuestionGenerationService(
        IQuestionGenerationJobRepository jobRepo,
        IQuestionRepository questionRepo,
        IRepository<Category> categoryRepo,
        IBackgroundTaskQueue queue)
    {
        _jobRepo = jobRepo;
        _questionRepo = questionRepo;
        _categoryRepo = categoryRepo;
        _queue = queue;
    }

    public async Task<QuestionGenerationJobDto> CreateJobAsync(
        CreateQuestionGenerationJobDto dto, int createdByUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Topic))
            throw new InvalidOperationException("El tema es obligatorio.");
        if (dto.Count < 1 || dto.Count > MaxCount)
            throw new InvalidOperationException($"La cantidad debe estar entre 1 y {MaxCount}.");

        var category = await _categoryRepo.GetByIdAsync(dto.CategoryId, ct)
            ?? throw new InvalidOperationException("La categoría seleccionada no existe.");
        if (!category.AllowsAiGeneration)
            throw new InvalidOperationException("Esta categoría no está habilitada para generación de preguntas por IA.");

        var job = new QuestionGenerationJob
        {
            CategoryId = dto.CategoryId,
            Difficulty = dto.Difficulty,
            Type = dto.Type,
            Topic = dto.Topic.Trim(),
            RequestedCount = dto.Count,
            CreatedByUserId = createdByUserId,
            Status = QuestionGenerationJobStatus.Queued,
            Items = Enumerable.Range(0, dto.Count)
                .Select(_ => new QuestionGenerationJobItem { Status = QuestionGenerationJobItemStatus.Pending })
                .ToList()
        };

        await _jobRepo.AddAsync(job, ct);
        _queue.EnqueueJob(job.Id);

        return MapJobToDto(job, category.Name);
    }

    public async Task<IReadOnlyList<QuestionGenerationJobItemDto>> GetPendingReviewItemsAsync(CancellationToken ct = default)
    {
        var items = await _jobRepo.GetPendingReviewItemsAsync(ct);
        return items.Select(i => new QuestionGenerationJobItemDto(
            i.Id, i.JobId, i.Job.Topic, i.Status, i.ErrorMessage,
            i.Question is null ? null : MapQuestionToDto(i.Question))).ToList();
    }

    public async Task<IReadOnlyList<QuestionGenerationJobProgressDto>> GetActiveJobsProgressAsync(CancellationToken ct = default)
    {
        var jobs = await _jobRepo.GetActiveJobsWithProgressAsync(ct);
        return jobs.Select(j => new QuestionGenerationJobProgressDto(
            j.Id, j.Topic, j.CategoryId, j.Category?.Name ?? "", j.Difficulty, j.Type, j.Status,
            j.RequestedCount,
            j.Items.Count(i => i.Status == QuestionGenerationJobItemStatus.Pending),
            j.Items.Count(i => i.Status == QuestionGenerationJobItemStatus.Succeeded),
            j.Items.Count(i => i.Status == QuestionGenerationJobItemStatus.Failed),
            j.CreatedAt)).ToList();
    }

    public async Task<QuestionDto> ApproveAsync(int itemId, CancellationToken ct = default)
    {
        var item = await _jobRepo.GetItemWithJobAsync(itemId, ct)
            ?? throw new KeyNotFoundException("El ítem de generación no existe.");
        if (item.QuestionId is null || item.Status != QuestionGenerationJobItemStatus.Succeeded)
            throw new InvalidOperationException("Este ítem no tiene una pregunta generada para aprobar.");

        var question = await _questionRepo.GetWithAnswersAsync(item.QuestionId.Value, ct)
            ?? throw new KeyNotFoundException("La pregunta generada ya no existe.");

        question.QuestionReviewStatus = QuestionReviewStatus.Approved;
        question.UpdatedAt = DateTime.UtcNow;
        await _questionRepo.UpdateAsync(question, ct);

        return MapQuestionToDto(question);
    }

    public async Task<bool> RejectAsync(int itemId, CancellationToken ct = default)
    {
        var item = await _jobRepo.GetItemWithJobAsync(itemId, ct)
            ?? throw new KeyNotFoundException("El ítem de generación no existe.");
        if (item.QuestionId is null || item.Status != QuestionGenerationJobItemStatus.Succeeded)
            throw new InvalidOperationException("Este ítem no tiene una pregunta generada para rechazar.");

        var question = await _questionRepo.GetByIdAsync(item.QuestionId.Value, ct);
        if (question is null) return false;

        question.QuestionReviewStatus = QuestionReviewStatus.Rejected;
        question.UpdatedAt = DateTime.UtcNow;
        await _questionRepo.UpdateAsync(question, ct);
        return true;
    }

    private static QuestionGenerationJobDto MapJobToDto(QuestionGenerationJob job, string categoryName) => new(
        job.Id, job.CategoryId, categoryName, job.Difficulty, job.Type,
        job.Topic, job.RequestedCount, job.Status, job.CreatedAt, job.CompletedAt);

    private static QuestionDto MapQuestionToDto(Question q) => new(
        q.Id, q.Text, q.Type, q.Difficulty,
        q.CategoryId, q.Category?.Name ?? "",
        q.Points, q.IsActive, q.SampleAnswer,
        q.Answers.OrderBy(a => a.Order)
            .Select(a => new AnswerDto(a.Id, a.Text, a.IsCorrect, a.Order))
            .ToList(),
        q.CreatedAt, q.UpdatedAt);
}
