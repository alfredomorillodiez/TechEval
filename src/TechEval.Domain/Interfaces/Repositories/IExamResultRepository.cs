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

    // ---- Reserva mientras alguien corrige -------------------------------------------------
    // Cada operación es una sola escritura condicional, no leer y después escribir: dos
    // correctores que abren a la vez no pueden ganar los dos.

    /// <summary>
    /// Reserva el resultado para <paramref name="userId"/> hasta <paramref name="until"/> si está
    /// pendiente y libre, caducado o ya es suyo. False si otra persona tiene una reserva vigente
    /// o si el resultado ya no está pendiente. También sirve para renovar.
    /// </summary>
    Task<bool> TryReserveAsync(int resultId, int userId, DateTime now, DateTime until, CancellationToken ct = default);

    /// <summary>
    /// Para el envío, dentro de su transacción: quita la reserva si es de <paramref name="userId"/>,
    /// si no hay ninguna o si caducó. False si otra persona tiene una vigente.
    /// </summary>
    Task<bool> TryReleaseForSubmitAsync(int resultId, int userId, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Libera sin corregir. Con <paramref name="userId"/>, solo si la reserva es suya; con null,
    /// siempre (el administrador). True si había algo que liberar.
    /// </summary>
    Task<bool> ReleaseAsync(int resultId, int? userId, CancellationToken ct = default);

    /// <summary>Libera todas las reservas de un usuario: al dejar el rol de evaluador.</summary>
    Task ReleaseAllOfAsync(int userId, CancellationToken ct = default);

    /// <summary>Quién tiene la reserva y hasta cuándo, para explicar un 409.</summary>
    Task<(int? UserId, string? UserName, DateTime? Until)> GetReservationAsync(int resultId, CancellationToken ct = default);

    // ---- Evaluador ------------------------------------------------------------------------

    /// <summary>
    /// Pendientes de las pruebas asignadas al evaluador, sin los suyos como candidato, del más
    /// antiguo al más reciente.
    /// </summary>
    Task<IReadOnlyList<ExamResult>> GetPendingForEvaluatorAsync(int evaluatorId, string evaluatorEmail, CancellationToken ct = default);

    /// <summary>
    /// El resultado, para la corrección, solo si su prueba está asignada al evaluador y no es
    /// suyo como candidato. Null en cualquier otro caso, sin distinguir cuál.
    /// </summary>
    Task<ExamResult?> GetForEvaluatorAsync(int resultId, int evaluatorId, string evaluatorEmail, CancellationToken ct = default);

    /// <summary>Resultados que corrigió el usuario, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<ExamResult>> GetReviewedByAsync(int userId, CancellationToken ct = default);
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
