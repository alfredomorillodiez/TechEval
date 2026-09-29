using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Application.Services;

/// <summary>
/// Qué evaluadores corrigen cada prueba. Spec exam-management — Asignación de evaluadores.
/// </summary>
public interface IExamEvaluatorService
{
    Task<IReadOnlyList<ExamEvaluatorDto>> ListAsync(int examId, CancellationToken ct = default);

    /// <summary>Asigna un evaluador activo. Asignarlo otra vez no hace nada.</summary>
    Task<IReadOnlyList<ExamEvaluatorDto>> AssignAsync(int examId, int userId, int assignedByUserId, CancellationToken ct = default);

    /// <summary>Quita la asignación. Las correcciones ya hechas no cambian.</summary>
    Task<IReadOnlyList<ExamEvaluatorDto>> UnassignAsync(int examId, int userId, CancellationToken ct = default);
}

public class ExamEvaluatorService : IExamEvaluatorService
{
    private readonly IRepository<ExamEvaluator> _assignmentRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IExamRepository _examRepo;

    public ExamEvaluatorService(
        IRepository<ExamEvaluator> assignmentRepo, IRepository<User> userRepo, IExamRepository examRepo)
    {
        _assignmentRepo = assignmentRepo;
        _userRepo = userRepo;
        _examRepo = examRepo;
    }

    public async Task<IReadOnlyList<ExamEvaluatorDto>> ListAsync(int examId, CancellationToken ct = default)
    {
        await EnsureExamAsync(examId, ct);

        var assignments = await _assignmentRepo.FindAsync(a => a.ExamId == examId, ct);
        var ids = assignments.Select(a => a.UserId).ToList();
        var users = (await _userRepo.FindAsync(u => ids.Contains(u.Id), ct)).ToDictionary(u => u.Id);

        return assignments
            .Where(a => users.ContainsKey(a.UserId))
            .Select(a => new ExamEvaluatorDto(
                a.UserId, users[a.UserId].Name, users[a.UserId].Email, users[a.UserId].IsActive, a.AssignedAt))
            .OrderBy(e => e.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<ExamEvaluatorDto>> AssignAsync(
        int examId, int userId, int assignedByUserId, CancellationToken ct = default)
    {
        await EnsureExamAsync(examId, ct);

        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user is not { Role: UserRole.Evaluador, IsActive: true })
            throw new ValidationException("Solo se puede asignar un usuario activo con rol de evaluador.");

        var existing = await _assignmentRepo.FindAsync(a => a.ExamId == examId && a.UserId == userId, ct);
        if (existing.Count == 0)
            await _assignmentRepo.AddAsync(new ExamEvaluator
            {
                ExamId = examId,
                UserId = userId,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = assignedByUserId
            }, ct);

        return await ListAsync(examId, ct);
    }

    public async Task<IReadOnlyList<ExamEvaluatorDto>> UnassignAsync(int examId, int userId, CancellationToken ct = default)
    {
        await EnsureExamAsync(examId, ct);

        foreach (var assignment in await _assignmentRepo.FindAsync(a => a.ExamId == examId && a.UserId == userId, ct))
            await _assignmentRepo.DeleteAsync(assignment, ct);

        return await ListAsync(examId, ct);
    }

    private async Task EnsureExamAsync(int examId, CancellationToken ct)
    {
        if (await _examRepo.GetByIdAsync(examId, ct) is null)
            throw new NotFoundException("Prueba no encontrada.");
    }
}
