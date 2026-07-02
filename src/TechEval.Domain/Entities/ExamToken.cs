namespace TechEval.Domain.Entities;

public class ExamToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public int ExamId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }

    public Exam Exam { get; set; } = null!;
    public ExamSession? ExamSession { get; set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => !IsUsed && !IsExpired;
}
