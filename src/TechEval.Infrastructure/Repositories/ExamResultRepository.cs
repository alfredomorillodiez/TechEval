using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Infrastructure.Data;

namespace TechEval.Infrastructure.Repositories;

public class ExamResultRepository : BaseRepository<ExamResult>, IExamResultRepository
{
    public ExamResultRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<ExamResult>> GetByExamAsync(int examId, CancellationToken ct = default)
        => await Context.ExamResults
            .Include(r => r.Exam)
            .Where(r => r.ExamId == examId)
            .OrderByDescending(r => r.CompletedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ExamResult>> GetByCandidateEmailAsync(string email, CancellationToken ct = default)
        => await Context.ExamResults
            .Include(r => r.Exam)
            .Where(r => r.CandidateEmail == email)
            .OrderByDescending(r => r.CompletedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ExamResult>> GetByUserAsync(int userId, CancellationToken ct = default)
        => await Context.ExamResults
            .Include(r => r.Exam)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CompletedAt)
            .ToListAsync(ct);

    public async Task<ExamResult?> GetWithDetailsAsync(int id, CancellationToken ct = default)
        => await Context.ExamResults
            .Include(r => r.Exam)
            .Include(r => r.ExamSession)
                .ThenInclude(s => s!.UserAnswers)
                    .ThenInclude(ua => ua.Question)
                        .ThenInclude(q => q!.Answers)
            .Include(r => r.ExamSession)
                .ThenInclude(s => s!.UserAnswers)
                    .ThenInclude(ua => ua.SelectedAnswer)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<ExamResult>> GetAllWithDetailsAsync(CancellationToken ct = default)
        => await Context.ExamResults
            .Include(r => r.Exam)
            .OrderByDescending(r => r.CompletedAt)
            .ToListAsync(ct);

    // Orden ascendente a propósito: la espera del candidato marca la prioridad.
    public async Task<IReadOnlyList<ExamResult>> GetPendingReviewAsync(CancellationToken ct = default)
        => await Context.ExamResults
            .Include(r => r.Exam)
            .Include(r => r.ExamSession)
                .ThenInclude(s => s!.UserAnswers)
                    .ThenInclude(ua => ua.Question)
            .Where(r => r.Status == ExamResultStatus.PendingReview)
            .OrderBy(r => r.CompletedAt)
            .ToListAsync(ct);

    public async Task<ExamResult?> GetForReviewAsync(int id, CancellationToken ct = default)
        => await Context.ExamResults
            .Include(r => r.Exam)
            .Include(r => r.ExamSession)
                .ThenInclude(s => s!.UserAnswers)
                    .ThenInclude(ua => ua.Question)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
}
