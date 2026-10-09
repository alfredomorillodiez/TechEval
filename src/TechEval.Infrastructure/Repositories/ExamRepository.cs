using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Common;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Infrastructure.Data;

namespace TechEval.Infrastructure.Repositories;

public class ExamRepository : BaseRepository<Exam>, IExamRepository
{
    public ExamRepository(AppDbContext context) : base(context) { }

    public async Task<Exam?> GetWithQuestionsAsync(int id, CancellationToken ct = default)
        => await Context.Exams
            .Include(e => e.ExamQuestions)
                .ThenInclude(eq => eq.Question)
                    .ThenInclude(q => q!.Answers)
            .Include(e => e.ExamQuestions)
                .ThenInclude(eq => eq.Question)
                    .ThenInclude(q => q!.Category)
            .Include(e => e.CreatedByUser)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<PagedResult<ExamSummaryRow>> GetPagedSummariesAsync(
        ExamFilter filter, PageQuery query, CancellationToken ct = default)
    {
        IQueryable<Exam> q = Context.Exams.AsNoTracking();

        if (filter.Active is { } active) q = q.Where(e => e.IsActive == active);
        if (query.Text is { } text)
        {
            var t = PagedQuery.Lower(text);
            q = q.Where(e => e.Title.ToLower().Contains(t));
        }

        var total = await q.CountAsync(ct);

        // Se ordena sobre la entidad y se proyecta después: los recuentos son subconsultas, y
        // no se carga el árbol de preguntas, invitaciones, sesiones y resultados de cada prueba.
        var desc = query.Desc;
        var ordered = query.SortKey switch
        {
            "title" => PagedQuery.By(q, e => e.Title.ToLower(), desc),
            "created" => PagedQuery.By(q, e => e.CreatedAt, desc),
            "questions" => PagedQuery.By(q, e => e.ExamQuestions.Count(), desc),
            "time" => PagedQuery.By(q, e => e.TimeLimitMinutes, desc),
            "invitations" => PagedQuery.By(q, e => e.ExamTokens.Count(), desc),
            "results" => PagedQuery.By(q, e => e.ExamTokens.Count(t => t.ExamSession != null && t.ExamSession.ExamResult != null), desc),
            _ => q.OrderByDescending(e => e.CreatedAt)
        };

        var items = await ordered.ThenByDescending(e => e.Id)
            .Skip(query.Skip).Take(query.EffectivePageSize)
            .Select(e => new ExamSummaryRow(
                e.Id, e.Title, e.TimeLimitMinutes, e.PassingScorePercentage, e.IsActive, e.CreatedAt,
                e.ExamQuestions.Count(),
                e.ExamQuestions.Sum(eq => eq.Question.Points),
                e.ExamTokens.Count(),
                e.ExamTokens.Count(t => t.ExamSession != null && t.ExamSession.ExamResult != null),
                e.ExamTokens.Count(t => t.ExamSession != null && t.ExamSession.Status == SessionStatus.Abandoned)))
            .ToListAsync(ct);

        return PagedResult<ExamSummaryRow>.Of(items, total, query);
    }

    public async Task<IReadOnlyList<ExamOption>> GetOptionsAsync(CancellationToken ct = default)
        => await Context.Exams.AsNoTracking()
            .OrderBy(e => e.Title.ToLower()).ThenBy(e => e.Id)
            .Select(e => new ExamOption(e.Id, e.Title))
            .ToListAsync(ct);

    public async Task<int> CountActiveAsync(CancellationToken ct = default)
        => await Context.Exams.CountAsync(e => e.IsActive, ct);

    public async Task<IReadOnlyList<Exam>> GetActiveWithoutEvaluatorAsync(CancellationToken ct = default)
        => await Context.Exams
            .Where(e => e.IsActive
                && e.ExamQuestions.Any(eq => eq.Question.Type == QuestionType.OpenEnded)
                && !Context.ExamEvaluators.Any(a => a.ExamId == e.Id))
            .OrderBy(e => e.Title)
            .Select(e => new Exam { Id = e.Id, Title = e.Title })
            .ToListAsync(ct);
}
