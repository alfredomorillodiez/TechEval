using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Common;
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

    public async Task<PagedResult<Question>> GetPagedAsync(
        QuestionFilter filter, PageQuery query, CancellationToken ct = default)
    {
        IQueryable<Question> q = Context.Questions.AsNoTracking();

        if (filter.CategoryId is { } categoryId) q = q.Where(x => x.CategoryId == categoryId);
        if (filter.Difficulty is { } difficulty) q = q.Where(x => x.Difficulty == difficulty);
        if (filter.Type is { } type) q = q.Where(x => x.Type == type);
        if (filter.Active is { } active) q = q.Where(x => x.IsActive == active);
        if (query.Text is { } text)
        {
            var t = PagedQuery.Lower(text);
            q = q.Where(x => x.Text.ToLower().Contains(t));
        }

        var total = await q.CountAsync(ct);

        // Sin las opciones: la tabla del banco no las muestra y son cuatro filas por pregunta.
        var items = await Order(q.Include(x => x.Category), query)
            .Skip(query.Skip).Take(query.EffectivePageSize)
            .ToListAsync(ct);

        return PagedResult<Question>.Of(items, total, query);
    }

    private static IOrderedQueryable<Question> Order(IQueryable<Question> q, PageQuery p)
    {
        var desc = p.Desc;
        var ordered = p.SortKey switch
        {
            "text" => PagedQuery.By(q, x => x.Text.ToLower(), desc),
            "category" => PagedQuery.By(q, x => x.Category.Name.ToLower(), desc),
            "difficulty" => PagedQuery.By(q, x => x.Difficulty, desc),
            "type" => PagedQuery.By(q, x => x.Type, desc),
            "points" => PagedQuery.By(q, x => x.Points, desc),
            "status" => PagedQuery.By(q, x => x.IsActive, desc),
            _ => q.OrderBy(x => x.Category.Name).ThenBy(x => x.Difficulty)
        };
        return ordered.ThenBy(x => x.Id);
    }

    public async Task<IReadOnlyList<Question>> GetRandomAsync(
        int count, List<int>? categoryIds, DifficultyLevel? difficulty, CancellationToken ct = default)
    {
        // Pregunta y categoría activas: las preguntas de una categoría dada de baja no entran en
        // la generación, aunque ellas mismas sigan marcadas como activas.
        var query = Context.Questions
            .Include(q => q.Answers)
            .Where(q => q.IsActive && q.Category!.IsActive);

        if (categoryIds is { Count: > 0 }) query = query.Where(q => categoryIds.Contains(q.CategoryId));
        if (difficulty.HasValue) query = query.Where(q => q.Difficulty == difficulty.Value);

        // ORDER BY NEWID() en SQL Server via EF
        return await query.OrderBy(_ => Guid.NewGuid()).Take(count).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<DifficultyLevel, int>> CountByDifficultyAsync(
        List<int>? categoryIds, CancellationToken ct = default)
    {
        // La misma regla que la generación: la disponibilidad que se anuncia es la que se puede usar.
        var query = Context.Questions.Where(q => q.IsActive && q.Category!.IsActive);
        if (categoryIds is { Count: > 0 }) query = query.Where(q => categoryIds.Contains(q.CategoryId));

        // Un GROUP BY en la base; los niveles sin filas no salen del GROUP BY, así que se
        // completan con cero aquí.
        var counts = await query
            .GroupBy(q => q.Difficulty)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Enum.GetValues<DifficultyLevel>()
            .ToDictionary(l => l, l => counts.FirstOrDefault(c => c.Level == l)?.Count ?? 0);
    }

    public async Task<IReadOnlyList<int>> GetReferencedAnswerIdsAsync(
        int questionId, CancellationToken ct = default)
        => await Context.UserAnswers
            .Where(ua => ua.QuestionId == questionId && ua.SelectedAnswerId != null)
            .Select(ua => ua.SelectedAnswerId!.Value)
            .Distinct()
            .ToListAsync(ct);
}
