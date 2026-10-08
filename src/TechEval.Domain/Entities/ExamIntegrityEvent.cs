using TechEval.Domain.Enums;

namespace TechEval.Domain.Entities;

/// <summary>
/// Señal de la actividad del candidato durante la prueba: una salida de la página, una
/// vuelta o un pegado en una respuesta abierta. Es información para el corrector; nada la
/// usa para puntuar. Del pegado se guarda la longitud, nunca el texto.
/// </summary>
public class ExamIntegrityEvent
{
    public int Id { get; set; }
    public int ExamSessionId { get; set; }
    public IntegrityEventType Type { get; set; }

    // Pregunta que el candidato tenía en pantalla cuando se produjo la señal.
    public int? QuestionId { get; set; }

    // Hora de recepción en el servidor, en UTC. La del cliente no se acepta: se puede
    // falsificar y depende del reloj del equipo.
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // Solo en PageReturned. La mide el navegador, porque una señal reintentada llega con
    // la hora del reintento y restar horas del servidor daría una ausencia falsa.
    public int? AwaySeconds { get; set; }

    // Solo en Paste.
    public int? PastedChars { get; set; }

    public ExamSession ExamSession { get; set; } = null!;
}
