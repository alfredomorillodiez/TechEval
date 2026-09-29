using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Application.Services;

public interface IExamIntegrityService
{
    Task RecordAsync(int sessionId, int userId, IntegrityEventInputDto dto, CancellationToken ct = default);
    Task<IntegrityReportDto> GetReportAsync(int resultId, CancellationToken ct = default);

    /// <summary>
    /// El mismo informe sin la hora del reloj: cada señal lleva solo el tiempo desde el inicio.
    /// Para el evaluador, que corrige a ciegas. La comprobación de acceso la hace quien llama.
    /// </summary>
    Task<IntegrityReportDto> GetEvaluatorReportAsync(int resultId, CancellationToken ct = default);
}

/// <summary>
/// Registro y lectura de las señales de integridad de una sesión: salidas de la página,
/// vueltas y pegados en respuestas abiertas.
///
/// Las señales no deciden nada. No bloquean la prueba, no puntúan y no llegan al candidato.
/// Tampoco prueban nada por su ausencia: quien sepa usar las herramientas de desarrollo
/// puede impedir que se envíen.
/// </summary>
public class ExamIntegrityService : IExamIntegrityService
{
    /// <summary>
    /// Tope por sesión. Un candidato normal produce unas decenas; el tope existe para que un
    /// bucle, torpe o malintencionado, no llene la tabla.
    /// </summary>
    public const int MaxEventsPerSession = 500;

    /// <summary>Misma longitud máxima que una respuesta abierta: un pegado más largo no cabe.</summary>
    public const int MaxPastedChars = 4000;

    private readonly IExamTokenRepository _tokenRepo;
    private readonly IExamRepository _examRepo;
    private readonly IRepository<ExamSession> _sessionRepo;
    private readonly IRepository<ExamIntegrityEvent> _eventRepo;
    private readonly IExamResultRepository _resultRepo;

    public ExamIntegrityService(
        IExamTokenRepository tokenRepo,
        IExamRepository examRepo,
        IRepository<ExamSession> sessionRepo,
        IRepository<ExamIntegrityEvent> eventRepo,
        IExamResultRepository resultRepo)
    {
        _tokenRepo = tokenRepo;
        _examRepo = examRepo;
        _sessionRepo = sessionRepo;
        _eventRepo = eventRepo;
        _resultRepo = resultRepo;
    }

    public async Task RecordAsync(
        int sessionId, int userId, IntegrityEventInputDto dto, CancellationToken ct = default)
    {
        var token = await _tokenRepo.GetBySessionIdAsync(sessionId, ct)
            ?? throw new SessionAccessDeniedException("Sesión no encontrada.");

        ExamTokenService.EnsureOwnedBy(token, userId);

        var session = token.ExamSession!;

        // El 409 depende del estado de la sesión, no del reloj: una señal que llega en los
        // segundos de gracia, o justo antes del envío automático, sigue siendo útil.
        // El resultado se consulta aparte porque la carga del token no lo trae, y una sesión
        // con resultado está enviada aunque siga marcada InProgress.
        if (session.Status != SessionStatus.InProgress
            || await _resultRepo.CountAsync(r => r.ExamSessionId == sessionId, ct) > 0)
            throw new ConflictException("La prueba ya se ha enviado.");

        if (!Enum.IsDefined(dto.Type))
            throw new ValidationException("Tipo de señal desconocido.");

        if (dto.Type == IntegrityEventType.Paste)
            await EnsureOpenQuestionOfExamAsync(token.ExamId, dto.QuestionId, ct);

        // Comprobar y añadir no es atómico: dos señales simultáneas pueden pasar el tope por
        // una o dos. Es un tope contra el volumen, no una cifra contractual.
        if (await _eventRepo.CountAsync(e => e.ExamSessionId == sessionId, ct) >= MaxEventsPerSession)
        {
            if (!session.IntegrityLimitReached)
            {
                session.IntegrityLimitReached = true;
                await _sessionRepo.UpdateAsync(session, ct);
            }
            return;
        }

        var maxAwaySeconds = Math.Max(0, token.Exam.TimeLimitMinutes) * 60;

        await _eventRepo.AddAsync(new ExamIntegrityEvent
        {
            ExamSessionId = sessionId,
            Type = dto.Type,
            QuestionId = dto.QuestionId,
            OccurredAt = DateTime.UtcNow,
            AwaySeconds = dto.Type == IntegrityEventType.PageReturned
                ? Math.Clamp(dto.AwaySeconds ?? 0, 0, maxAwaySeconds)
                : null,
            PastedChars = dto.Type == IntegrityEventType.Paste
                ? Math.Clamp(dto.PastedChars ?? 0, 0, MaxPastedChars)
                : null
        }, ct);
    }

    private async Task EnsureOpenQuestionOfExamAsync(int examId, int? questionId, CancellationToken ct)
    {
        var exam = await _examRepo.GetWithQuestionsAsync(examId, ct);
        var question = exam?.ExamQuestions.FirstOrDefault(eq => eq.QuestionId == questionId)?.Question;

        if (question is null || question.Type != QuestionType.OpenEnded)
            throw new ValidationException("Un pegado solo se registra sobre una pregunta abierta de la prueba.");
    }

    public Task<IntegrityReportDto> GetReportAsync(int resultId, CancellationToken ct = default)
        => BuildReportAsync(resultId, withClockTime: true, ct);

    public Task<IntegrityReportDto> GetEvaluatorReportAsync(int resultId, CancellationToken ct = default)
        => BuildReportAsync(resultId, withClockTime: false, ct);

    private async Task<IntegrityReportDto> BuildReportAsync(int resultId, bool withClockTime, CancellationToken ct)
    {
        var result = await _resultRepo.GetByIdAsync(resultId, ct)
            ?? throw new NotFoundException("Resultado no encontrado.");

        var session = await _sessionRepo.GetByIdAsync(result.ExamSessionId, ct)
            ?? throw new NotFoundException("Sesión no encontrada.");

        // Toda sesión nueva recibe semilla. Sin ella, la sesión es anterior al registro, y
        // decir "sin señales" la presentaría como limpia cuando solo es desconocida.
        if (session.ShuffleSeed is null)
            return new IntegrityReportDto(
                IntegrityAvailability.NotRecorded, false, 0, 0, 0, 0, new(), new());

        var exam = await _examRepo.GetWithQuestionsAsync(result.ExamId, ct);
        var numberOf = exam?.ExamQuestions.ToDictionary(eq => eq.QuestionId, eq => eq.Order)
            ?? new Dictionary<int, int>();
        int? NumberOf(int? questionId)
            => questionId is int id && numberOf.TryGetValue(id, out var n) ? n : null;

        var events = (await _eventRepo.FindAsync(e => e.ExamSessionId == session.Id, ct))
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.Id)
            .ToList();

        var pastes = events.Where(e => e.Type == IntegrityEventType.Paste).ToList();

        return new IntegrityReportDto(
            IntegrityAvailability.Recorded,
            session.IntegrityLimitReached,
            PageLeftCount: events.Count(e => e.Type == IntegrityEventType.PageLeft),
            TotalAwaySeconds: events.Where(e => e.Type == IntegrityEventType.PageReturned)
                .Sum(e => e.AwaySeconds ?? 0),
            PasteCount: pastes.Count,
            TotalPastedChars: pastes.Sum(e => e.PastedChars ?? 0),
            PastesByQuestion: pastes
                .Where(e => e.QuestionId is not null)
                .GroupBy(e => e.QuestionId!.Value)
                .Select(g => new PastesByQuestionDto(
                    g.Key, NumberOf(g.Key), g.Count(), g.Sum(e => e.PastedChars ?? 0)))
                .OrderBy(p => p.QuestionNumber ?? int.MaxValue)
                .ToList(),
            Events: events
                .Select(e => new IntegrityEventDto(
                    e.Type, withClockTime ? e.OccurredAt : null, e.QuestionId, NumberOf(e.QuestionId),
                    e.AwaySeconds, e.PastedChars,
                    Math.Max(0, (int)(e.OccurredAt - session.StartedAt).TotalSeconds)))
                .ToList());
    }
}
