using TechEval.Application.DTOs;
using TechEval.Domain.Enums;
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
        // Los pendientes de corrección viajan sin nota ni veredicto: la puntuación parcial
        // no es la nota del alumno y mostrarla sería comunicarle un resultado falso.
        return completed.Select(r => r.Status == ExamResultStatus.Reviewed
            ? new CompletedExamDto(r.Id, r.Exam.Title, r.ScorePercentage, r.Passed, r.Status, r.CompletedAt)
            : new CompletedExamDto(r.Id, r.Exam.Title, null, null, r.Status, r.CompletedAt)).ToList();
    }
}
