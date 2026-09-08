using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Infrastructure.Data;

namespace TechEval.Infrastructure.Repositories;

public class QuestionRepository : BaseRepository<Question>, IQuestionRepository
{
    public QuestionRepository(AppDbContext context) : base(context) { }

    public async Task<Question?> GetWithAnswersAsync(int id, CancellationToken ct = default)
        => await Context.Questions
            .Include(q => q.Category)
            .Include(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<IReadOnlyList<Question>> GetByCategoryAsync(int categoryId, CancellationToken ct = default)
        => await Context.Questions
            .Include(q => q.Category)
            .Include(q => q.Answers)
            .Where(q => q.CategoryId == categoryId && q.IsActive)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Question>> GetFilteredAsync(
        int? categoryId, DifficultyLevel? difficulty, QuestionType? type,
        bool onlyActive = true, CancellationToken ct = default)
    {
        var query = Context.Questions
            .Include(q => q.Category)
            .Include(q => q.Answers)
            .AsQueryable();

        if (onlyActive)
            query = query.Where(q => q.IsActive);
        if (categoryId.HasValue) query = query.Where(q => q.CategoryId == categoryId.Value);
        if (difficulty.HasValue) query = query.Where(q => q.Difficulty == difficulty.Value);
        if (type.HasValue) query = query.Where(q => q.Type == type.Value);

        return await query.OrderBy(q => q.Category!.Name).ThenBy(q => q.Difficulty).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Question>> GetRandomAsync(
        int count, List<int>? categoryIds, DifficultyLevel? difficulty, CancellationToken ct = default)
    {
        var query = Context.Questions
            .Include(q => q.Answers)
            .Where(q => q.IsActive);

        if (categoryIds is { Count: > 0 }) query = query.Where(q => categoryIds.Contains(q.CategoryId));
        if (difficulty.HasValue) query = query.Where(q => q.Difficulty == difficulty.Value);

        // ORDER BY NEWID() en SQL Server via EF
        return await query.OrderBy(_ => Guid.NewGuid()).Take(count).ToListAsync(ct);
    }
}
