using TechEval.Domain.Common;
using TechEval.Domain.Enums;

namespace TechEval.Domain.Entities;

public class QuestionGenerationJob : AuditableEntity
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public DifficultyLevel Difficulty { get; set; }
    public QuestionType Type { get; set; }
    public string Topic { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public QuestionGenerationJobStatus Status { get; set; } = QuestionGenerationJobStatus.Queued;
    public int CreatedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Category Category { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<QuestionGenerationJobItem> Items { get; set; } = new List<QuestionGenerationJobItem>();
}
