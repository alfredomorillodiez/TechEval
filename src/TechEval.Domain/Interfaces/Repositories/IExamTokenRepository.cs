using TechEval.Domain.Entities;

namespace TechEval.Domain.Interfaces.Repositories;

public interface IExamTokenRepository : IRepository<ExamToken>
{
    Task<ExamToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<ExamToken?> GetWithExamAndSessionAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<ExamToken>> GetPendingByUserAsync(int userId, CancellationToken ct = default);
}
