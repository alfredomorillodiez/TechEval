using TechEval.Domain.Common;

namespace TechEval.Domain.Entities;

public class Exam : AuditableEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; } = 60;
    public int PassingScorePercentage { get; set; } = 70;
    public bool IsActive { get; set; } = true;
    public int CreatedByUserId { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
    public ICollection<ExamToken> ExamTokens { get; set; } = new List<ExamToken>();

    public int TotalPoints => ExamQuestions.Sum(eq => eq.Question?.Points ?? 0);
}
