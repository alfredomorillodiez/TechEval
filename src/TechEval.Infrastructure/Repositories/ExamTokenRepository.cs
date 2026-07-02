using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Entities;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Infrastructure.Data;

namespace TechEval.Infrastructure.Repositories;

public class ExamTokenRepository : BaseRepository<ExamToken>, IExamTokenRepository
{
    public ExamTokenRepository(AppDbContext context) : base(context) { }

    public async Task<ExamToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        => await Context.ExamTokens
            .FirstOrDefaultAsync(t => t.Token == token, ct);

    public async Task<ExamToken?> GetWithExamAndSessionAsync(string token, CancellationToken ct = default)
        => await Context.ExamTokens
            .Include(t => t.Exam)
                .ThenInclude(e => e.ExamQuestions)
                    .ThenInclude(eq => eq.Question)
                        .ThenInclude(q => q!.Answers)
            .Include(t => t.ExamSession)
                .ThenInclude(s => s!.UserAnswers)
            .Include(t => t.ExamSession)
                .ThenInclude(s => s!.ExamResult)
            .FirstOrDefaultAsync(t => t.Token == token, ct);
}
