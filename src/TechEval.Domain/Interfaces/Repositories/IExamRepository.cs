using TechEval.Domain.Entities;

namespace TechEval.Domain.Interfaces.Repositories;

public interface IExamRepository : IRepository<Exam>
{
    Task<Exam?> GetWithQuestionsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Exam>> GetWithStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Cuántos exámenes están activos. Sin sus preguntas, invitaciones, sesiones ni
    /// resultados: el dashboard solo necesita el número, y esas inclusiones se resuelven
    /// con uniones que multiplican filas.
    /// </summary>
    Task<int> CountActiveAsync(CancellationToken ct = default);
}
