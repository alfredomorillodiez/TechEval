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

    public async Task<PeriodResultStats> GetPeriodStatsAsync(
        DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        // Las cuatro cifras salen del mismo filtro, así que van en un solo recorrido.
        // El promedio no se pide aquí: sobre un conjunto vacío AVG devuelve nulo, y el
        // redondeo debe hacerse una sola vez, al final.
        var agregado = await Context.ExamResults
            .Where(r => r.CompletedAt >= desde && r.CompletedAt < hasta)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Scored = g.Count(r => r.Status == ExamResultStatus.Reviewed),
                ScoreSum = g.Sum(r => r.Status == ExamResultStatus.Reviewed ? r.ScorePercentage : 0m),
                Passed = g.Count(r => r.Status == ExamResultStatus.Reviewed && r.Passed == true)
            })
            .FirstOrDefaultAsync(ct);

        // Sin filas en el periodo no hay grupo, y eso es un periodo vacío, no un error.
        return agregado is null
            ? new PeriodResultStats(0, 0, 0m, 0)
            : new PeriodResultStats(agregado.Total, agregado.Scored, agregado.ScoreSum, agregado.Passed);
    }

    public async Task<int> CountPendingReviewAsync(CancellationToken ct = default)
        => await Context.ExamResults
            .CountAsync(r => r.Status == ExamResultStatus.PendingReview, ct);

    public async Task<IReadOnlyList<ExamResult>> GetRecentAsync(
        int count, CancellationToken ct = default)
        => await Context.ExamResults
            .Include(r => r.Exam)
            .OrderByDescending(r => r.CompletedAt)
            .Take(count)
            .ToListAsync(ct);
}
