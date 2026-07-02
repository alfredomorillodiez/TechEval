using TechEval.Domain.Entities;

namespace TechEval.Domain.Interfaces.Repositories;

public interface IExamRepository : IRepository<Exam>
{
    Task<Exam?> GetWithQuestionsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Exam>> GetWithStatsAsync(CancellationToken ct = default);
}
