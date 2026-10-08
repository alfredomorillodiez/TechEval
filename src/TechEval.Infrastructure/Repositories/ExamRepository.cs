using Microsoft.EntityFrameworkCore;
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

    public async Task<IReadOnlyList<Exam>> GetWithStatsAsync(CancellationToken ct = default)
        => await Context.Exams
            .Include(e => e.ExamQuestions)
                .ThenInclude(eq => eq.Question)
            .Include(e => e.ExamTokens)
                .ThenInclude(t => t.ExamSession)
                    .ThenInclude(s => s!.ExamResult)
            .OrderByDescending(e => e.CreatedAt)
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
