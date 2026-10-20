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

    // Las condiciones con las que EMPEZÓ el candidato: el tiempo y la nota de corte de la prueba en
    // ese momento. Editar la prueba después cambia lo que verán las sesiones nuevas, no esta. Antes
    // el plazo y el veredicto se calculaban con el valor actual de la prueba: acortar el tiempo con
    // candidatos en curso les cambiaba el plazo, y subir la nota daba veredictos distintos a dos
    // candidatos con la misma nota. Nulos en las sesiones anteriores al cambio, que usan los de la prueba.
    public int? TimeLimitMinutesSnapshot { get; set; }
    public int? PassingScoreSnapshot { get; set; }

    /// <summary>El tiempo límite de ESTA sesión: el guardado al empezar, o el de la prueba si no hay.</summary>
    public int TimeLimitFor(Exam exam) => TimeLimitMinutesSnapshot ?? exam.TimeLimitMinutes;

    /// <summary>La nota de corte de ESTA sesión: la guardada al empezar, o la de la prueba si no hay.</summary>
    public int PassingScoreFor(Exam exam) => PassingScoreSnapshot ?? exam.PassingScorePercentage;

    public ExamToken ExamToken { get; set; } = null!;
    public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
    public ICollection<ExamIntegrityEvent> IntegrityEvents { get; set; } = new List<ExamIntegrityEvent>();
    public ExamResult? ExamResult { get; set; }
}
