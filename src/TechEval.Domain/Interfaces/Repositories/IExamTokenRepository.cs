using TechEval.Domain.Entities;

namespace TechEval.Domain.Interfaces.Repositories;

public interface IExamTokenRepository : IRepository<ExamToken>
{
    Task<ExamToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<ExamToken?> GetWithExamAndSessionAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<ExamToken>> GetPendingByUserAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Token de una sesión, con su examen y su sesión cargados. De ahí salen en una sola
    /// lectura el dueño (`UserId`) y el plazo (`StartedAt` más `TimeLimitMinutes`).
    /// </summary>
    Task<ExamToken?> GetBySessionIdAsync(int sessionId, CancellationToken ct = default);

    /// <summary>
    /// Tokens con una sesión en curso, con su prueba y su resultado cargados. Son pocas filas, así que
    /// quien llama calcula el plazo en memoria. Lo usa el trabajo que marca las sesiones abandonadas.
    /// </summary>
    Task<IReadOnlyList<ExamToken>> GetInProgressAsync(CancellationToken ct = default);
}
