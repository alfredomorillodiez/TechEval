using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Application.Services;

/// <summary>Se lanza cuando el resultado ya fue corregido: la API la traduce a 409.</summary>
public class AlreadyReviewedException : ConflictException
{
    public AlreadyReviewedException(string message) : base(message) { }
}

/// <summary>Se lanza cuando la corrección no es válida: la API la traduce a 400.</summary>
public class InvalidReviewException : ValidationException
{
    public InvalidReviewException(string message) : base(message) { }
}

public interface IOpenQuestionReviewService
{
    Task<IReadOnlyList<PendingReviewSummaryDto>> GetPendingAsync(CancellationToken ct = default);
    Task<PendingReviewDetailDto?> GetDetailAsync(int resultId, CancellationToken ct = default);
    Task<ExamResultDto> SubmitReviewAsync(
        int resultId, SubmitReviewDto dto, int reviewedByUserId, CancellationToken ct = default);
}

public class OpenQuestionReviewService : IOpenQuestionReviewService
{
    private readonly IExamResultRepository _resultRepo;
    private readonly IExamRepository _examRepo;
    private readonly IRepository<UserAnswer> _answerRepo;
    private readonly IEmailService _emailService;
    private readonly IResultService _resultService;
    private readonly IUnitOfWork _unitOfWork;

    public OpenQuestionReviewService(
        IExamResultRepository resultRepo,
        IExamRepository examRepo,
        IRepository<UserAnswer> answerRepo,
        IEmailService emailService,
        IResultService resultService,
        IUnitOfWork unitOfWork)
    {
        _resultRepo = resultRepo;
        _examRepo = examRepo;
        _answerRepo = answerRepo;
        _emailService = emailService;
        _resultService = resultService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<PendingReviewSummaryDto>> GetPendingAsync(CancellationToken ct = default)
    {
        var pending = await _resultRepo.GetPendingReviewAsync(ct);
        var now = DateTime.UtcNow;

        return pending.Select(r => new PendingReviewSummaryDto(
            r.Id,
            r.ExamId,
            r.Exam?.Title ?? "",
            r.CandidateName,
            r.CandidateEmail,
            r.CompletedAt,
            Math.Max(0, (int)(now - r.CompletedAt).TotalDays),
            OpenAnswersOf(r).Count)).ToList();
    }

    public async Task<PendingReviewDetailDto?> GetDetailAsync(int resultId, CancellationToken ct = default)
    {
        var result = await _resultRepo.GetForReviewAsync(resultId, ct);
        if (result is null) return null;

        if (result.Status == ExamResultStatus.Reviewed)
            throw new AlreadyReviewedException("Este resultado ya ha sido corregido.");

        var openAnswers = OpenAnswersOf(result);

        var answers = openAnswers
            .OrderBy(ua => ua.Id)
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

        return new PendingReviewDetailDto(
            result.Id,
            result.Exam?.Title ?? "",
            result.CandidateName,
            result.CandidateEmail,
            result.CompletedAt,
            result.TotalPoints,
            result.ObtainedPoints,
            answers);
    }

    public async Task<ExamResultDto> SubmitReviewAsync(
        int resultId, SubmitReviewDto dto, int reviewedByUserId, CancellationToken ct = default)
    {
        var result = await _resultRepo.GetForReviewAsync(resultId, ct)
            ?? throw new InvalidReviewException("Resultado no encontrado.");

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

        // Todas las escrituras en una transacción. Sin ella, el repositorio confirma en cada
        // operación: una corrección de tres abiertas son cuatro confirmaciones sueltas, y un
        // fallo a mitad dejaba puntuaciones escritas con el resultado sin cerrar. El spec
        // promete que o se corrige todo o no se modifica nada; esto lo hace cierto.
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
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
            int obtained = allAnswers.Sum(ua => ua.AwardedPoints ?? 0);

            pct = result.TotalPoints > 0
                ? Math.Round((decimal)obtained / result.TotalPoints * 100, 2)
                : 0;

            passed = pct >= passingScore;

            result.ObtainedPoints = obtained;
            result.ScorePercentage = pct;
            result.Passed = passed;
            result.Status = ExamResultStatus.Reviewed;
            result.ReviewedAt = DateTime.UtcNow;
            result.ReviewedByUserId = reviewedByUserId;
            await _resultRepo.UpdateAsync(result, token);
        }, ct);

        // Después del cierre y sin revertir: una corrección válida no debe perderse
        // porque el SMTP esté caído. SmtpEmailService ya registra el fallo en el log
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

        return await _resultService.GetDetailAsync(result.Id, ct)
            ?? throw new InvalidOperationException("No se pudo recuperar el resultado corregido.");
    }

    /// <summary>
    /// Los puntos máximos que se le mostraron al corrector. No los de la pregunta actual:
    /// puede haberse editado después del envío, y entonces la pantalla enseñaría un máximo
    /// y la validación exigiría otro.
    /// </summary>
    private static int MaxPointsOf(UserAnswer ua)
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

    /// <summary>
    /// Todas las respuestas a preguntas abiertas del resultado, tengan contenido o estén
    /// en blanco. Las vacías llegan pre-puntuadas a 0 pero siguen requiriendo confirmación,
    /// así que no pueden filtrarse por AwardedPoints == null.
    /// </summary>
    private static List<UserAnswer> OpenAnswersOf(ExamResult result)
        => result.ExamSession?.UserAnswers
            .Where(ua => ua.Question?.Type == QuestionType.OpenEnded)
            .ToList() ?? new List<UserAnswer>();
}
