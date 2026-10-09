using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Common;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Infrastructure.Data;

namespace TechEval.Infrastructure.Repositories;

public class ExamResultRepository : BaseRepository<ExamResult>, IExamResultRepository
{
    public ExamResultRepository(AppDbContext context) : base(context) { }

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

    public async Task<PagedResult<ExamResult>> GetPagedAsync(
        ResultFilter filter, PageQuery query, CancellationToken ct = default)
    {
        var filtered = ApplyFilter(Context.ExamResults.AsNoTracking(), filter, query.Text);
        var total = await filtered.CountAsync(ct);

        var items = await Order(filtered.Include(r => r.Exam), query)
            .Skip(query.Skip).Take(query.EffectivePageSize)
            .ToListAsync(ct);

        return PagedResult<ExamResult>.Of(items, total, query);
    }

    private static IQueryable<ExamResult> ApplyFilter(
        IQueryable<ExamResult> q, ResultFilter f, string? text)
    {
        if (f.ExamId is { } examId) q = q.Where(r => r.ExamId == examId);

        if (text is not null)
        {
            var t = PagedQuery.Lower(text);
            q = q.Where(r => r.CandidateName.ToLower().Contains(t) || r.CandidateEmail.ToLower().Contains(t));
        }

        // Los mismos tres valores que entendía el filtro del cliente. Otro valor no filtra.
        q = f.Outcome?.Trim().ToLowerInvariant() switch
        {
            "pending" => q.Where(r => r.Status == ExamResultStatus.PendingReview),
            "passed" => q.Where(r => r.Status == ExamResultStatus.Reviewed && r.Passed == true),
            "failed" => q.Where(r => r.Status == ExamResultStatus.Reviewed && r.Passed == false),
            _ => q
        };

        // Un pendiente lleva una puntuación parcial: no tiene nota que comparar.
        if (f.ScoreMin is { } min)
            q = q.Where(r => r.Status == ExamResultStatus.Reviewed && r.ScorePercentage >= min);
        if (f.ScoreMax is { } max)
            q = q.Where(r => r.Status == ExamResultStatus.Reviewed && r.ScorePercentage <= max);

        if (f.From is { } from) q = q.Where(r => r.CompletedAt >= from);
        if (f.To is { } to) q = q.Where(r => r.CompletedAt < to);
        return q;
    }

    // Lista blanca de columnas: una clave desconocida no llega a SQL, cae en el orden natural.
    // Todas desempatan por Id, para que dos páginas seguidas no repitan ni pierdan filas.
    private static IOrderedQueryable<ExamResult> Order(IQueryable<ExamResult> q, PageQuery p)
    {
        var desc = p.Desc;
        var ordered = p.SortKey switch
        {
            "candidate" => PagedQuery.By(q, r => r.CandidateName.ToLower(), desc),
            "exam" => PagedQuery.By(q, r => r.Exam.Title.ToLower(), desc),
            // Los pendientes, sin nota definitiva, al final en cualquier dirección.
            "score" => PagedQuery.ThenBy(
                q.OrderBy(r => r.Status == ExamResultStatus.Reviewed ? 0 : 1), r => r.ScorePercentage, desc),
            "outcome" => PagedQuery.By(q, r => r.Status == ExamResultStatus.PendingReview ? 2 : r.Passed == true ? 0 : 1, desc),
            "date" => PagedQuery.By(q, r => r.CompletedAt, desc),
            _ => q.OrderByDescending(r => r.CompletedAt)
        };
        return ordered.ThenByDescending(r => r.Id);
    }

    public async Task<ResultStats> GetStatsAsync(int? examId, CancellationToken ct = default)
    {
        var all = Context.ExamResults.AsNoTracking();
        if (examId is { } id) all = all.Where(r => r.ExamId == id);

        var total = await all.CountAsync(ct);
        var reviewed = all.Where(r => r.Status == ExamResultStatus.Reviewed);

        var agg = await reviewed
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Passed = g.Count(r => r.Passed == true),
                Avg = g.Average(r => r.ScorePercentage),
                Max = g.Max(r => r.ScorePercentage),
                Min = g.Min(r => r.ScorePercentage)
            })
            .FirstOrDefaultAsync(ct);

        // Tramos por umbral y no por entero: una nota de 49,5 cae en el primero. Con rangos
        // cerrados de enteros (0–49, 50–59...) las notas con decimales entre dos tramos se perdían.
        var buckets = await reviewed
            .GroupBy(r => r.ScorePercentage < 50 ? 0
                : r.ScorePercentage < 60 ? 1
                : r.ScorePercentage < 70 ? 2
                : r.ScorePercentage < 80 ? 3
                : r.ScorePercentage < 90 ? 4 : 5)
            .Select(g => new { Bucket = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var distribution = new int[6];
        foreach (var b in buckets) distribution[b.Bucket] = b.Count;

        var reviewedCount = agg?.Count ?? 0;
        return new ResultStats(
            total, reviewedCount, total - reviewedCount, agg?.Passed ?? 0,
            agg is null ? 0 : Math.Round(agg.Avg, 1),
            agg?.Max ?? 0, agg?.Min ?? 0, distribution);
    }

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
