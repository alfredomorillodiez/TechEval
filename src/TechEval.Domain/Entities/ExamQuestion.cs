namespace TechEval.Domain.Entities;

public class ExamQuestion
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public int QuestionId { get; set; }
    public int Order { get; set; }

    public Exam Exam { get; set; } = null!;
    public Question Question { get; set; } = null!;
}
