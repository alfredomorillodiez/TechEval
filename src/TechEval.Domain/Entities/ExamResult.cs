namespace TechEval.Domain.Entities;

public class ExamResult
{
    public int Id { get; set; }
    public int ExamSessionId { get; set; }
    public int ExamId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public int TotalPoints { get; set; }
    public int ObtainedPoints { get; set; }
    public decimal ScorePercentage { get; set; }
    public bool Passed { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public ExamSession ExamSession { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
    public User? User { get; set; }
}
