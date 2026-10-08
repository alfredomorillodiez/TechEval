namespace TechEval.Domain.Entities;

public class ExamToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public int ExamId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }

    public Exam Exam { get; set; } = null!;
    public User? User { get; set; }
    public ExamSession? ExamSession { get; set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    // Responde a "¿se puede EMPEZAR esta prueba?", no a "¿se puede volver a ella?".
    // La vuelta depende del estado de ExamSession, que esta propiedad no puede consultar
    // sin depender de que la navegación esté cargada: esa decisión vive en ExamTokenService.
    public bool CanStart => !IsUsed && !IsExpired;
}
