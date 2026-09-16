using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
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

    // Pendiente es lo que el alumno todavía puede resolver: la invitación sin usar y en
    // plazo, o la prueba que dejó a medias. Filtrar solo por !IsUsed escondía del portal
    // la prueba en curso, que es justo la que el alumno necesita encontrar para volver.
    public async Task<IReadOnlyList<ExamToken>> GetPendingByUserAsync(int userId, CancellationToken ct = default)
        => await Context.ExamTokens
            .Include(t => t.Exam)
            .Include(t => t.ExamSession)
            .Where(t => t.UserId == userId
                && ((!t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                    || (t.ExamSession != null
                        && t.ExamSession.Status == SessionStatus.InProgress
                        && t.ExamSession.ExamResult == null)))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
}
