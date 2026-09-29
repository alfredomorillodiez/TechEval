namespace TechEval.Application.DTOs;

/// <summary>Un evaluador asignado a una prueba, visto por el administrador.</summary>
public record ExamEvaluatorDto(int UserId, string Name, string Email, bool IsActive, DateTime AssignedAt);
