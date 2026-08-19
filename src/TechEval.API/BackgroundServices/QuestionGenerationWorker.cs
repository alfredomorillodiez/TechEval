using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.API.BackgroundServices;

public class QuestionGenerationWorker : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuestionGenerationWorker> _logger;

    public QuestionGenerationWorker(
        IBackgroundTaskQueue queue, IServiceScopeFactory scopeFactory, ILogger<QuestionGenerationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedJobsAsync(stoppingToken);

        await foreach (var jobId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(jobId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo procesando el job de generación de preguntas {JobId}", jobId);
            }
        }
    }

    private async Task RecoverInterruptedJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IQuestionGenerationJobRepository>();

        var runningJobs = await jobRepo.GetByStatusAsync(QuestionGenerationJobStatus.Running, ct);
        foreach (var job in runningJobs)
        {
            job.Status = QuestionGenerationJobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            foreach (var item in job.Items.Where(i => i.Status == QuestionGenerationJobItemStatus.Pending))
            {
                item.Status = QuestionGenerationJobItemStatus.Failed;
                item.ErrorMessage = "El proceso se interrumpió antes de generar esta pregunta.";
            }
            await jobRepo.UpdateAsync(job, ct);
            _logger.LogWarning("Job de generación {JobId} marcado como Failed tras un reinicio de la API.", job.Id);
        }
    }

    private async Task ProcessJobAsync(int jobId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IQuestionGenerationJobRepository>();
        var questionRepo = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();
        var categoryRepo = scope.ServiceProvider.GetRequiredService<IRepository<Category>>();
        var aiService = scope.ServiceProvider.GetRequiredService<IQuestionGenerationAiService>();

        var job = await jobRepo.GetWithItemsAsync(jobId, ct);
        if (job is null) return;

        job.Status = QuestionGenerationJobStatus.Running;
        await jobRepo.UpdateAsync(job, ct);

        var category = await categoryRepo.GetByIdAsync(job.CategoryId, ct);
        var categoryName = category?.Name ?? string.Empty;

        foreach (var item in job.Items.Where(i => i.Status == QuestionGenerationJobItemStatus.Pending).ToList())
        {
            var result = await aiService.GenerateQuestionAsync(job.Topic, categoryName, job.Difficulty, job.Type, ct);

            if (!result.Success)
            {
                item.Status = QuestionGenerationJobItemStatus.Failed;
                item.ErrorMessage = result.ErrorMessage;
            }
            else
            {
                var question = new Question
                {
                    Text = result.QuestionText!,
                    Type = job.Type,
                    Difficulty = job.Difficulty,
                    CategoryId = job.CategoryId,
                    Points = 1,
                    SampleAnswer = result.SampleAnswer,
                    QuestionReviewStatus = QuestionReviewStatus.PendingReview,
                    Answers = result.Answers
                        .Select((a, i) => new Answer { Text = a.Text, IsCorrect = a.IsCorrect, Order = i + 1 })
                        .ToList()
                };
                await questionRepo.AddAsync(question, ct);

                item.Status = QuestionGenerationJobItemStatus.Succeeded;
                item.QuestionId = question.Id;
            }

            await jobRepo.UpdateAsync(job, ct);
        }

        job.Status = QuestionGenerationJobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        await jobRepo.UpdateAsync(job, ct);
    }
}
