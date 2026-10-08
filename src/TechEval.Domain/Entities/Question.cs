using TechEval.Domain.Common;
using TechEval.Domain.Enums;

namespace TechEval.Domain.Entities;

public class Question : AuditableEntity
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public DifficultyLevel Difficulty { get; set; }
    public int CategoryId { get; set; }
    public int Points { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    // Para preguntas abiertas: respuesta de referencia para el corrector
    public string? SampleAnswer { get; set; }

    public Category Category { get; set; } = null!;
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    public ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
}
