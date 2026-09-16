using TechEval.Domain.Entities;

namespace TechEval.Domain.Interfaces.Repositories;

public interface IExamResultRepository : IRepository<ExamResult>
{
    Task<IReadOnlyList<ExamResult>> GetByExamAsync(int examId, CancellationToken ct = default);
    Task<IReadOnlyList<ExamResult>> GetByCandidateEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<ExamResult>> GetByUserAsync(int userId, CancellationToken ct = default);
    Task<ExamResult?> GetWithDetailsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ExamResult>> GetAllWithDetailsAsync(CancellationToken ct = default);

    /// <summary>Cola de correcciones pendientes, del más antiguo al más reciente</summary>
    Task<IReadOnlyList<ExamResult>> GetPendingReviewAsync(CancellationToken ct = default);

    /// <summary>Resultado con sus respuestas y preguntas, para la pantalla de corrección</summary>
    Task<ExamResult?> GetForReviewAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Cifras de un periodo, agregadas en la base de datos. El dashboard las necesita y
    /// antes las sacaba trayéndose el histórico entero para contarlo en memoria.
    /// </summary>
    /// <param name="desde">Inclusive.</param>
    /// <param name="hasta">Exclusive. Un rango usa el índice de <c>CompletedAt</c>; comparar año y mes, no.</param>
    Task<PeriodResultStats> GetPeriodStatsAsync(
        DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>Cuántos resultados esperan corrección, sin traerlos.</summary>
    Task<int> CountPendingReviewAsync(CancellationToken ct = default);

    /// <summary>Los más recientes, ordenados y limitados en la base de datos.</summary>
    Task<IReadOnlyList<ExamResult>> GetRecentAsync(int count, CancellationToken ct = default);
}

/// <summary>
/// Cifras crudas de un periodo. Se devuelven la suma y la cuenta, no el promedio: con un
/// conjunto vacío <c>AVG</c> devuelve nulo, y el redondeo debe hacerse una sola vez, al
/// final. La división es aritmética y no pertenece a la base de datos.
/// </summary>
/// <param name="Total">Resultados completados en el periodo, corregidos o no.</param>
/// <param name="Scored">De esos, los que ya están corregidos.</param>
/// <param name="ScoreSum">Suma de las puntuaciones de los corregidos.</param>
/// <param name="Passed">De los corregidos, los que aprobaron.</param>
public record PeriodResultStats(int Total, int Scored, decimal ScoreSum, int Passed);
