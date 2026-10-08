using TechEval.Domain.Enums;

namespace TechEval.Domain.Entities;

public class ExamSession
{
    public int Id { get; set; }
    public int ExamTokenId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.InProgress;

    // Semilla del orden de preguntas y opciones que ve esta sesión. Toda sesión nueva la
    // recibe al crearse; una semilla nula marca una sesión anterior al cambio, que conserva
    // el orden del examen y no tiene registro de señales.
    public int? ShuffleSeed { get; set; }

    // La sesión llegó al tope de señales y las siguientes se descartaron.
    public bool IntegrityLimitReached { get; set; }

    public ExamToken ExamToken { get; set; } = null!;
    public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
    public ICollection<ExamIntegrityEvent> IntegrityEvents { get; set; } = new List<ExamIntegrityEvent>();
    public ExamResult? ExamResult { get; set; }
}
