namespace TechEval.Domain.Entities;

public class UserAnswer
{
    public int Id { get; set; }
    public int ExamSessionId { get; set; }
    public int QuestionId { get; set; }
    public int? SelectedAnswerId { get; set; }
    public string? OpenAnswer { get; set; }
    public bool? IsCorrect { get; set; }

    // Puntos congelados en el envío para las preguntas de test, y otorgados por el
    // corrector para las abiertas. Es la fuente de verdad de ObtainedPoints: recalcular
    // contra Question.Points daría notas distintas si la pregunta se edita más tarde.
    public int? AwardedPoints { get; set; }
    public string? ReviewerComment { get; set; }

    public DateTime AnsweredAt { get; set; } = DateTime.Now;

    public ExamSession ExamSession { get; set; } = null!;
    public Question Question { get; set; } = null!;
    public Answer? SelectedAnswer { get; set; }
}
