using TechEval.Application.DTOs;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Application.Services;

public interface IStudentPortalService
{
    Task<List<PendingExamDto>> GetPendingAsync(int userId, CancellationToken ct = default);
    Task<List<CompletedExamDto>> GetCompletedAsync(int userId, CancellationToken ct = default);
}

public class StudentPortalService : IStudentPortalService
{
    private readonly IExamTokenRepository _tokenRepo;
    private readonly IExamResultRepository _resultRepo;

    public StudentPortalService(IExamTokenRepository tokenRepo, IExamResultRepository resultRepo)
    {
        _tokenRepo = tokenRepo;
        _resultRepo = resultRepo;
    }

    public async Task<List<PendingExamDto>> GetPendingAsync(int userId, CancellationToken ct = default)
    {
        var pending = await _tokenRepo.GetPendingByUserAsync(userId, ct);
        return pending.Select(t => new PendingExamDto(t.Token, t.Exam.Title, t.ExpiresAt)).ToList();
    }

    public async Task<List<CompletedExamDto>> GetCompletedAsync(int userId, CancellationToken ct = default)
    {
        var completed = await _resultRepo.GetByUserAsync(userId, ct);
        return completed.Select(r =>
            new CompletedExamDto(r.Id, r.Exam.Title, r.ScorePercentage, r.Passed, r.CompletedAt)).ToList();
    }
}
