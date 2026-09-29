using TechEval.Domain.Enums;

namespace TechEval.Application.DTOs;

/// <summary>
/// Señal que envía la interfaz del candidato. No lleva hora: la fija el servidor. Tampoco
/// tiene sitio para el texto de un pegado, a propósito: del pegado solo interesa la
/// longitud, y el portapapeles puede contener cualquier dato personal ajeno a la prueba.
/// </summary>
public record IntegrityEventInputDto(
    IntegrityEventType Type,
    int? QuestionId,
    int? AwaySeconds,
    int? PastedChars);

public enum IntegrityAvailability
{
    /// <summary>La sesión es anterior al registro de señales: no hay datos, ni a favor ni en contra.</summary>
    NotRecorded = 1,
    Recorded = 2
}

/// <summary>
/// `QuestionNumber` es la posición de la pregunta en el examen, que es el orden en que el
/// corrector ve las respuestas. El candidato la vio en otra posición.
/// </summary>
/// <param name="OccurredAt">
/// Hora UTC de la señal. Solo para el administrador: al evaluador le llega nula, porque la hora
/// a la que alguien hizo la prueba ayuda a saber quién la hizo.
/// </param>
/// <param name="ElapsedSeconds">Segundos desde el inicio de la sesión. Llega a los dos.</param>
public record IntegrityEventDto(
    IntegrityEventType Type,
    DateTime? OccurredAt,
    int? QuestionId,
    int? QuestionNumber,
    int? AwaySeconds,
    int? PastedChars,
    int ElapsedSeconds = 0);

public record PastesByQuestionDto(
    int QuestionId,
    int? QuestionNumber,
    int Count,
    int TotalChars);

/// <summary>
/// Señales de la sesión de un resultado, para el corrector. Son información para decidir:
/// nada en el sistema las usa para puntuar. Las duraciones de ausencia las mide el
/// navegador del candidato.
/// </summary>
public record IntegrityReportDto(
    IntegrityAvailability Availability,
    bool LimitReached,
    int PageLeftCount,
    int TotalAwaySeconds,
    int PasteCount,
    int TotalPastedChars,
    List<PastesByQuestionDto> PastesByQuestion,
    List<IntegrityEventDto> Events);
