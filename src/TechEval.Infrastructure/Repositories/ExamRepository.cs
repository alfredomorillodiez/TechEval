using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Entities;
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
}
