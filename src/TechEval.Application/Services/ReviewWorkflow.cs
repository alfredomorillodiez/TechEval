using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Application.Services;

/// <summary>
/// Otra persona tiene el resultado reservado: la API la traduce a 409.
/// </summary>
public class ReservationConflictException : ConflictException
{
    public DateTime? ReservedUntil { get; }

    public ReservationConflictException(string message, DateTime? reservedUntil) : base(message)
        => ReservedUntil = reservedUntil;
}

/// <summary>Lo que queda del resultado tras una corrección aceptada.</summary>
public record ReviewOutcome(int ObtainedPoints, int TotalPoints, decimal ScorePercentage, bool Passed);

/// <summary>
/// La corrección de las preguntas abiertas, igual para el administrador y para el evaluador:
/// qué se corrige, la reserva, la validación, la escritura en una transacción y el correo.
/// </summary>
/// <remarks>
/// Los dos servicios que la usan solo se diferencian en cómo obtienen el resultado (el
/// evaluador, filtrado por su asignación) y en qué devuelven (el evaluador, sin identidad).
/// Tener la regla en un solo sitio evita que un día se valide distinto según quién corrige.
/// </remarks>
public sealed class ReviewWorkflow
{
    public const int ReservationMinutes = 30;

    private readonly IExamResultRepository _resultRepo;
    private readonly IExamRepository _examRepo;
    private readonly IRepository<UserAnswer> _answerRepo;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewWorkflow(
        IExamResultRepository resultRepo,
        IExamRepository examRepo,
        IRepository<UserAnswer> answerRepo,
        IEmailService emailService,
        IUnitOfWork unitOfWork)
    {
        _resultRepo = resultRepo;
        _examRepo = examRepo;
        _answerRepo = answerRepo;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Todas las respuestas a preguntas abiertas del resultado, tengan contenido o estén
    /// en blanco. Las vacías llegan pre-puntuadas a 0 pero siguen requiriendo confirmación,
    /// así que no pueden filtrarse por AwardedPoints == null.
    /// </summary>
    public static List<UserAnswer> OpenAnswersOf(ExamResult result)
        => result.ExamSession?.UserAnswers
            .Where(ua => ua.Question?.Type == QuestionType.OpenEnded)
            .ToList() ?? new List<UserAnswer>();

    /// <summary>Las respuestas abiertas, en el orden del examen, tal como se corrigen.</summary>
    public static List<ReviewableAnswerDto> BuildReviewableAnswers(ExamResult result)
        => SessionOrder.ByExamOrder(OpenAnswersOf(result), result.Exam)
            .Select(ua => new ReviewableAnswerDto(
                ua.Id,
                ua.QuestionId,
                // El enunciado y los puntos, tal como se le formularon: juzgar la respuesta
                // contra una pregunta editada después sería juzgarla contra otra pregunta.
                // SampleAnswer sí sale del banco, porque es guía del corrector y no algo
                // que el candidato llegara a ver.
                ua.QuestionTextSnapshot ?? ua.Question?.Text ?? "",
                ua.OpenAnswer,
                MaxPointsOf(ua),
                ua.AwardedPoints,
                ua.ReviewerComment,
                ua.Question?.SampleAnswer))
            .ToList();

    /// <summary>
    /// Reserva el resultado para quien lo corrige, o renueva su reserva. Devuelve el fin.
    /// </summary>
    /// <param name="revealHolder">
    /// Si el mensaje del 409 dice quién tiene la reserva. Sí para el administrador; no para el
    /// evaluador, que solo necesita saber que está ocupado.
    /// </param>
    public async Task<DateTime> ReserveAsync(
        ExamResult result, int userId, bool revealHolder, CancellationToken ct = default)
    {
        if (result.Status == ExamResultStatus.Reviewed)
            throw new AlreadyReviewedException("Este resultado ya ha sido corregido.");

        var now = DateTime.UtcNow;
        var until = now.AddMinutes(ReservationMinutes);
        if (await _resultRepo.TryReserveAsync(result.Id, userId, now, until, ct))
            return until;

        throw await ConflictAsync(result.Id, revealHolder, ct);
    }

    /// <summary>
    /// Valida y aplica la corrección completa. Todo en una transacción: la liberación de la
    /// reserva, las puntuaciones y el cierre. El correo va después y no la revierte.
    /// </summary>
    public async Task<ReviewOutcome> ApplyAsync(
        ExamResult result, SubmitReviewDto dto, int reviewerId, bool revealHolder, CancellationToken ct = default)
    {
        if (result.Status == ExamResultStatus.Reviewed)
            throw new AlreadyReviewedException("Este resultado ya ha sido corregido.");

        var openAnswers = OpenAnswersOf(result);
        var submitted = dto.Answers ?? new List<ReviewAnswerInputDto>();

        // Validar TODO antes de escribir nada: la corrección es atómica, no hay
        // estado intermedio de "a medio corregir".
        ValidateReview(openAnswers, submitted);

        var exam = await _examRepo.GetByIdAsync(result.ExamId, ct);
        int passingScore = exam?.PassingScorePercentage ?? 0;

        decimal pct = 0;
        bool passed = false;
        int obtained = 0;

        // Todas las escrituras en una transacción. Sin ella, el repositorio confirma en cada
        // operación: una corrección de tres abiertas son cuatro confirmaciones sueltas, y un
        // fallo a mitad dejaba puntuaciones escritas con el resultado sin cerrar. El spec
        // promete que o se corrige todo o no se modifica nada; esto lo hace cierto.
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            // Primero la reserva: si otra persona la tiene vigente, no se escribe nada.
            if (!await _resultRepo.TryReleaseForSubmitAsync(result.Id, reviewerId, DateTime.UtcNow, token))
                throw await ConflictAsync(result.Id, revealHolder, token);
            result.ReservedByUserId = null;
            result.ReservedUntil = null;

            var byId = openAnswers.ToDictionary(ua => ua.Id);
            foreach (var input in submitted)
            {
                var answer = byId[input.UserAnswerId];
                answer.AwardedPoints = input.AwardedPoints;
                answer.ReviewerComment = input.ReviewerComment;
                answer.IsCorrect = input.AwardedPoints >= MaxPointsOf(answer)
                    && MaxPointsOf(answer) > 0;
                await _answerRepo.UpdateAsync(answer, token);
            }

            // Suma sobre AwardedPoints, no sobre Question.Points: los puntos de test quedaron
            // congelados en el envío y no deben moverse si la pregunta se editó entretanto.
            var allAnswers = result.ExamSession?.UserAnswers ?? new List<UserAnswer>();
            obtained = allAnswers.Sum(ua => ua.AwardedPoints ?? 0);

            pct = result.TotalPoints > 0
                ? Math.Round((decimal)obtained / result.TotalPoints * 100, 2)
                : 0;

            passed = pct >= passingScore;

            result.ObtainedPoints = obtained;
            result.ScorePercentage = pct;
            result.Passed = passed;
            result.Status = ExamResultStatus.Reviewed;
            result.ReviewedAt = DateTime.UtcNow;
            result.ReviewedByUserId = reviewerId;
            await _resultRepo.UpdateAsync(result, token);
        }, ct);

        // Después del cierre y sin revertir: una corrección válida no debe perderse
        // porque el correo esté caído. GraphEmailService ya registra el fallo en el log
        // antes de relanzar, así que aquí solo hay que evitar que tumbe la operación.
        try
        {
            await _emailService.SendExamResultAsync(
                result.CandidateEmail, result.CandidateName,
                result.Exam?.Title ?? "", pct, passed, ct);
        }
        catch
        {
            // Silenciado a propósito: la corrección ya está persistida y es la que manda.
        }

        return new ReviewOutcome(obtained, result.TotalPoints, pct, passed);
    }

    private async Task<ReservationConflictException> ConflictAsync(int resultId, bool revealHolder, CancellationToken ct)
    {
        var (_, name, until) = await _resultRepo.GetReservationAsync(resultId, ct);
        var hasta = until is null ? "" : $" hasta las {until:HH:mm} (UTC)";
        var quien = revealHolder && name is not null ? name : "Otra persona";
        return new ReservationConflictException($"{quien} está corrigiendo este resultado{hasta}.", until);
    }

    /// <summary>
    /// Los puntos máximos que se le mostraron al corrector. No los de la pregunta actual:
    /// puede haberse editado después del envío, y entonces la pantalla enseñaría un máximo
    /// y la validación exigiría otro.
    /// </summary>
    public static int MaxPointsOf(UserAnswer ua)
        => ua.QuestionPointsSnapshot ?? ua.Question?.Points ?? 0;

    private static void ValidateReview(
        IReadOnlyList<UserAnswer> openAnswers, IReadOnlyList<ReviewAnswerInputDto> submitted)
    {
        var expectedIds = openAnswers.Select(a => a.Id).ToHashSet();
        var submittedIds = submitted.Select(a => a.UserAnswerId).ToList();

        if (submittedIds.Count != submittedIds.Distinct().Count())
            throw new InvalidReviewException("La corrección contiene respuestas duplicadas.");

        var foreignIds = submittedIds.Where(id => !expectedIds.Contains(id)).ToList();
        if (foreignIds.Count > 0)
            throw new InvalidReviewException(
                $"La corrección incluye respuestas que no pertenecen a este resultado: {string.Join(", ", foreignIds)}.");

        var missingIds = expectedIds.Where(id => !submittedIds.Contains(id)).ToList();
        if (missingIds.Count > 0)
            throw new InvalidReviewException(
                $"La corrección debe puntuar todas las respuestas abiertas. Faltan {missingIds.Count}.");

        var byId = openAnswers.ToDictionary(a => a.Id);
        foreach (var input in submitted)
        {
            int max = MaxPointsOf(byId[input.UserAnswerId]);
            if (input.AwardedPoints < 0 || input.AwardedPoints > max)
                throw new InvalidReviewException(
                    $"Los puntos otorgados deben estar entre 0 y {max} (recibido: {input.AwardedPoints}).");
        }
    }
}
