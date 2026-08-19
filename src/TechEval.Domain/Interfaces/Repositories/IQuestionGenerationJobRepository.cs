using TechEval.Domain.Entities;
using TechEval.Domain.Enums;

namespace TechEval.Domain.Interfaces.Repositories;

public interface IQuestionGenerationJobRepository : IRepository<QuestionGenerationJob>
{
    Task<QuestionGenerationJob?> GetWithItemsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<QuestionGenerationJob>> GetByStatusAsync(
        QuestionGenerationJobStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<QuestionGenerationJobItem>> GetPendingReviewItemsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<QuestionGenerationJob>> GetActiveJobsWithProgressAsync(CancellationToken ct = default);
    Task<QuestionGenerationJobItem?> GetItemWithJobAsync(int itemId, CancellationToken ct = default);
}
