using TechEval.Domain.Enums;

namespace TechEval.Domain.Entities;

public class ExamResult
{
    public int Id { get; set; }
    public int ExamSessionId { get; set; }
    public int ExamId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public int TotalPoints { get; set; }
    public int ObtainedPoints { get; set; }
    public decimal ScorePercentage { get; set; }

    // Null mientras el resultado esté pendiente de corrección manual: un resultado
    // sin corregir no tiene veredicto, y presentarlo como false sería un suspenso falso.
    public bool? Passed { get; set; }

    public ExamResultStatus Status { get; set; } = ExamResultStatus.Reviewed;
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }

    // Reserva mientras alguien corrige: quién la tiene y hasta cuándo. Una reserva caducada
    // cuenta como libre. Se toma y se libera con escrituras condicionales, no leyendo y
    // escribiendo, para que dos correctores que abren a la vez no la ganen los dos.
    public int? ReservedByUserId { get; set; }
    public DateTime? ReservedUntil { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public ExamSession ExamSession { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
    public User? User { get; set; }
    public User? ReviewedByUser { get; set; }
    public User? ReservedByUser { get; set; }
}
