using TechEval.Domain.Enums;

namespace TechEval.Domain.Entities;

public class QuestionGenerationJobItem
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public QuestionGenerationJobItemStatus Status { get; set; } = QuestionGenerationJobItemStatus.Pending;
    public int? QuestionId { get; set; }
    public string? ErrorMessage { get; set; }

    public QuestionGenerationJob Job { get; set; } = null!;
    public Question? Question { get; set; }
}
