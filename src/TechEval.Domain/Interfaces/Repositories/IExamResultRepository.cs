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
}
