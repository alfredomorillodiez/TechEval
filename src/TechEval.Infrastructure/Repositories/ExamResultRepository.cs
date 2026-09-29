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
            .Include(r => r.ReviewedByUser)
            .Include(r => r.Exam)
                .ThenInclude(e => e!.ExamQuestions)
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
            .Include(r => r.ReservedByUser)
            .Include(r => r.ExamSession)
                .ThenInclude(s => s!.UserAnswers)
                    .ThenInclude(ua => ua.Question)
            .Where(r => r.Status == ExamResultStatus.PendingReview)
            .OrderBy(r => r.CompletedAt)
            .ToListAsync(ct);

    public async Task<ExamResult?> GetForReviewAsync(int id, CancellationToken ct = default)
        => await Context.ExamResults
            .Include(r => r.Exam)
                .ThenInclude(e => e!.ExamQuestions)
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

    // ---- Reserva ------------------------------------------------------------------------
    //
    // En SQL Server, cada operación es un UPDATE con la condición en el WHERE: la base decide
    // quién gana, y dos aperturas simultáneas no pueden ganar las dos. La base en memoria de
    // las pruebas no admite ExecuteUpdate; ahí se hace la misma comprobación sobre la entidad
    // cargada, que sirve para probar las reglas pero no la concurrencia.
    //
    // ExecuteUpdate no toca la entidad que el contexto ya tiene cargada. Si después alguien
    // la guarda entera, escribiría la reserva antigua encima; por eso SyncLocal la pone al día.

    public async Task<bool> TryReserveAsync(
        int resultId, int userId, DateTime now, DateTime until, CancellationToken ct = default)
    {
        if (Context.Database.IsRelational())
        {
            var rows = await Context.ExamResults
                .Where(r => r.Id == resultId && r.Status == ExamResultStatus.PendingReview
                    && (r.ReservedByUserId == null || r.ReservedByUserId == userId || r.ReservedUntil < now))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.ReservedByUserId, userId)
                    .SetProperty(r => r.ReservedUntil, until), ct);
            if (rows == 1) SyncLocal(resultId, userId, until);
            return rows == 1;
        }

        var result = await Context.ExamResults.FirstOrDefaultAsync(r => r.Id == resultId, ct);
        if (result is null || result.Status != ExamResultStatus.PendingReview || !IsFreeFor(result, userId, now))
            return false;
        result.ReservedByUserId = userId;
        result.ReservedUntil = until;
        await Context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TryReleaseForSubmitAsync(
        int resultId, int userId, DateTime now, CancellationToken ct = default)
    {
        if (Context.Database.IsRelational())
        {
            var rows = await Context.ExamResults
                .Where(r => r.Id == resultId
                    && (r.ReservedByUserId == null || r.ReservedByUserId == userId || r.ReservedUntil < now))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.ReservedByUserId, (int?)null)
                    .SetProperty(r => r.ReservedUntil, (DateTime?)null), ct);
            if (rows == 1) SyncLocal(resultId, null, null);
            return rows == 1;
        }

        var result = await Context.ExamResults.FirstOrDefaultAsync(r => r.Id == resultId, ct);
        if (result is null || !IsFreeFor(result, userId, now)) return false;
        result.ReservedByUserId = null;
        result.ReservedUntil = null;
        await Context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReleaseAsync(int resultId, int? userId, CancellationToken ct = default)
    {
        if (Context.Database.IsRelational())
        {
            var rows = await Context.ExamResults
                .Where(r => r.Id == resultId && r.ReservedByUserId != null
                    && (userId == null || r.ReservedByUserId == userId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.ReservedByUserId, (int?)null)
                    .SetProperty(r => r.ReservedUntil, (DateTime?)null), ct);
            if (rows == 1) SyncLocal(resultId, null, null);
            return rows == 1;
        }

        var result = await Context.ExamResults.FirstOrDefaultAsync(r => r.Id == resultId, ct);
        if (result?.ReservedByUserId is null || (userId is not null && result.ReservedByUserId != userId))
            return false;
        result.ReservedByUserId = null;
        result.ReservedUntil = null;
        await Context.SaveChangesAsync(ct);
        return true;
    }

    public async Task ReleaseAllOfAsync(int userId, CancellationToken ct = default)
    {
        if (Context.Database.IsRelational())
        {
            await Context.ExamResults
                .Where(r => r.ReservedByUserId == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.ReservedByUserId, (int?)null)
                    .SetProperty(r => r.ReservedUntil, (DateTime?)null), ct);
            foreach (var local in Context.ExamResults.Local.Where(r => r.ReservedByUserId == userId).ToList())
                SyncLocal(local.Id, null, null);
            return;
        }

        foreach (var result in await Context.ExamResults.Where(r => r.ReservedByUserId == userId).ToListAsync(ct))
        {
            result.ReservedByUserId = null;
            result.ReservedUntil = null;
        }
        await Context.SaveChangesAsync(ct);
    }

    public async Task<(int? UserId, string? UserName, DateTime? Until)> GetReservationAsync(
        int resultId, CancellationToken ct = default)
    {
        var r = await Context.ExamResults
            .AsNoTracking()
            .Where(x => x.Id == resultId)
            .Select(x => new { x.ReservedByUserId, Name = x.ReservedByUser!.Name, x.ReservedUntil })
            .FirstOrDefaultAsync(ct);
        return r is null ? (null, null, null) : (r.ReservedByUserId, r.Name, r.ReservedUntil);
    }

    private static bool IsFreeFor(ExamResult r, int userId, DateTime now)
        => r.ReservedByUserId is null || r.ReservedByUserId == userId || r.ReservedUntil < now;

    private void SyncLocal(int resultId, int? userId, DateTime? until)
    {
        var local = Context.ExamResults.Local.FirstOrDefault(r => r.Id == resultId);
        if (local is null) return;
        var entry = Context.Entry(local);
        foreach (var (name, value) in new (string, object?)[]
                 { (nameof(ExamResult.ReservedByUserId), userId), (nameof(ExamResult.ReservedUntil), until) })
        {
            entry.Property(name).CurrentValue = value;
            entry.Property(name).OriginalValue = value;
        }
    }

    // ---- Evaluador ----------------------------------------------------------------------

    // El filtro de acceso del evaluador, en un solo sitio: prueba asignada y resultado que no
    // es suyo como candidato. La cola y la corrección lo usan igual.
    private IQueryable<ExamResult> VisibleToEvaluator(int evaluatorId, string evaluatorEmail)
        => Context.ExamResults.Where(r =>
            Context.ExamEvaluators.Any(a => a.ExamId == r.ExamId && a.UserId == evaluatorId)
            && (r.UserId == null || r.UserId != evaluatorId)
            && r.CandidateEmail != evaluatorEmail);

    public async Task<IReadOnlyList<ExamResult>> GetPendingForEvaluatorAsync(
        int evaluatorId, string evaluatorEmail, CancellationToken ct = default)
        => await VisibleToEvaluator(evaluatorId, evaluatorEmail)
            .Include(r => r.Exam)
            .Include(r => r.ExamSession)
                .ThenInclude(s => s!.UserAnswers)
                    .ThenInclude(ua => ua.Question)
            .Where(r => r.Status == ExamResultStatus.PendingReview)
            .OrderBy(r => r.CompletedAt)
            .ToListAsync(ct);

    public async Task<ExamResult?> GetForEvaluatorAsync(
        int resultId, int evaluatorId, string evaluatorEmail, CancellationToken ct = default)
        => await VisibleToEvaluator(evaluatorId, evaluatorEmail)
            .Include(r => r.Exam)
                .ThenInclude(e => e!.ExamQuestions)
            .Include(r => r.ExamSession)
                .ThenInclude(s => s!.UserAnswers)
                    .ThenInclude(ua => ua.Question)
            .FirstOrDefaultAsync(r => r.Id == resultId, ct);

    public async Task<IReadOnlyList<ExamResult>> GetReviewedByAsync(int userId, CancellationToken ct = default)
        => await Context.ExamResults
            .Include(r => r.Exam)
                .ThenInclude(e => e!.ExamQuestions)
            .Include(r => r.ExamSession)
                .ThenInclude(s => s!.UserAnswers)
                    .ThenInclude(ua => ua.Question)
            .Where(r => r.ReviewedByUserId == userId && r.Status == ExamResultStatus.Reviewed)
            .OrderByDescending(r => r.ReviewedAt)
            .ToListAsync(ct);
}
