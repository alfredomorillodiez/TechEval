using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Infrastructure.Data;

namespace TechEval.Infrastructure.Repositories;

public class QuestionGenerationJobRepository : BaseRepository<QuestionGenerationJob>, IQuestionGenerationJobRepository
{
    public QuestionGenerationJobRepository(AppDbContext context) : base(context) { }

    public async Task<QuestionGenerationJob?> GetWithItemsAsync(int id, CancellationToken ct = default)
        => await Context.QuestionGenerationJobs
            .Include(j => j.Category)
            .Include(j => j.Items)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<IReadOnlyList<QuestionGenerationJob>> GetByStatusAsync(
        QuestionGenerationJobStatus status, CancellationToken ct = default)
        => await Context.QuestionGenerationJobs
            .Include(j => j.Items)
            .Where(j => j.Status == status)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<QuestionGenerationJobItem>> GetPendingReviewItemsAsync(CancellationToken ct = default)
        => await Context.QuestionGenerationJobItems
            .Include(i => i.Job).ThenInclude(j => j.Category)
            .Include(i => i.Question).ThenInclude(q => q!.Answers)
            .Include(i => i.Question).ThenInclude(q => q!.Category)
            .Where(i =>
                (i.Status == QuestionGenerationJobItemStatus.Succeeded
                    && i.Question!.QuestionReviewStatus == QuestionReviewStatus.PendingReview)
                || i.Status == QuestionGenerationJobItemStatus.Failed)
            .OrderByDescending(i => i.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<QuestionGenerationJob>> GetActiveJobsWithProgressAsync(CancellationToken ct = default)
        => await Context.QuestionGenerationJobs
            .Include(j => j.Category)
            .Include(j => j.Items).ThenInclude(i => i.Question)
            .Where(j => j.Items.Any(i =>
                i.Status == QuestionGenerationJobItemStatus.Pending
                || i.Status == QuestionGenerationJobItemStatus.Failed
                || (i.Status == QuestionGenerationJobItemStatus.Succeeded
                    && i.Question!.QuestionReviewStatus == QuestionReviewStatus.PendingReview)))
            .OrderByDescending(j => j.Id)
            .ToListAsync(ct);

    public async Task<QuestionGenerationJobItem?> GetItemWithJobAsync(int itemId, CancellationToken ct = default)
        => await Context.QuestionGenerationJobItems
            .Include(i => i.Job)
            .Include(i => i.Question)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);
}
