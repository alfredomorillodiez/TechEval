using TechEval.Domain.Enums;

namespace TechEval.Domain.Entities;

public class ExamSession
{
    public int Id { get; set; }
    public int ExamTokenId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.InProgress;

    public ExamToken ExamToken { get; set; } = null!;
    public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
    public ExamResult? ExamResult { get; set; }
}
