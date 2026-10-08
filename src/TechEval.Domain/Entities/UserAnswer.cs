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

    // Copia de lo que se le preguntó al candidato, congelada en el envío por el mismo
    // motivo que AwardedPoints: la pregunta del banco se puede editar después, y las
    // fichas de resultados leían de ella. Editar una errata reescribía hacia atrás lo
    // que constaba que se preguntó en cada examen ya cerrado.
    // Opcionales porque las respuestas anteriores al cambio no las tienen hasta que el
    // guion de esquema las rellena.
    public string? QuestionTextSnapshot { get; set; }
    public string? SelectedAnswerTextSnapshot { get; set; }
    public string? CorrectAnswerTextSnapshot { get; set; }
    public int? QuestionPointsSnapshot { get; set; }

    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    public ExamSession ExamSession { get; set; } = null!;
    public Question Question { get; set; } = null!;
    public Answer? SelectedAnswer { get; set; }
}
