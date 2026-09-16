using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Application.Services;

/// <summary>
/// Se lanza cuando la sesión no pertenece a quien llama: la API la traduce a 403.
/// Los identificadores de sesión son secuenciales, así que el número por sí solo nunca
/// puede valer como prueba de propiedad.
/// </summary>
public class SessionAccessDeniedException : Exception
{
    public SessionAccessDeniedException(string message) : base(message) { }
}

/// <summary>
/// Se lanza cuando el plazo del examen ya venció: la API la traduce a 409.
/// </summary>
public class ExamTimeExpiredException : Exception
{
    public ExamTimeExpiredException(string message) : base(message) { }
}

public interface IExamTokenService
{
    Task<string> SendExamAsync(SendExamDto dto, string baseUrl, CancellationToken ct = default);
    Task<BulkSendResultDto> SendExamBulkAsync(BulkSendExamDto dto, string baseUrl, CancellationToken ct = default);
    Task<ExamTokenValidationDto> ValidateTokenAsync(string token, CancellationToken ct = default);
    Task<ExamSessionInfoDto?> StartSessionAsync(string token, CancellationToken ct = default);
    Task<ExamSubmissionReceiptDto> SubmitExamAsync(SubmitExamDto dto, int userId, CancellationToken ct = default);
    Task SaveDraftAnswerAsync(int sessionId, int userId, SubmitAnswerDto answer, CancellationToken ct = default);
}

public class ExamTokenService : IExamTokenService
{
    private readonly IExamTokenRepository _tokenRepo;
    private readonly IExamRepository _examRepo;
    private readonly IRepository<ExamSession> _sessionRepo;
    private readonly IRepository<UserAnswer> _answerRepo;
    private readonly IExamResultRepository _resultRepo;
    private readonly IRepository<User> _userRepo;
    private readonly IEmailService _emailService;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;

    public ExamTokenService(
        IExamTokenRepository tokenRepo,
        IExamRepository examRepo,
        IRepository<ExamSession> sessionRepo,
        IRepository<UserAnswer> answerRepo,
        IExamResultRepository resultRepo,
        IRepository<User> userRepo,
        IEmailService emailService,
        ITokenService tokenService,
        IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _tokenRepo = tokenRepo;
        _examRepo = examRepo;
        _sessionRepo = sessionRepo;
        _answerRepo = answerRepo;
        _resultRepo = resultRepo;
        _userRepo = userRepo;
        _emailService = emailService;
        _tokenService = tokenService;
    }

    public async Task<string> SendExamAsync(SendExamDto dto, string baseUrl, CancellationToken ct = default)
    {
        var exam = await _examRepo.GetByIdAsync(dto.ExamId, ct)
            ?? throw new InvalidOperationException("Examen no encontrado.");

        var secureToken = _tokenService.GenerateSecureToken();
        var expiresAt = DateTime.UtcNow.AddHours(dto.ExpirationHours);

        var examToken = new ExamToken
        {
            Token = secureToken,
            ExamId = dto.ExamId,
            CandidateName = dto.CandidateName,
            CandidateEmail = dto.CandidateEmail,
            ExpiresAt = expiresAt
        };

        await _tokenRepo.AddAsync(examToken, ct);

        var examLink = $"{baseUrl}/prueba/{secureToken}";
        await _emailService.SendExamInvitationAsync(
            dto.CandidateEmail, dto.CandidateName,
            exam.Title, examLink, expiresAt, ct);

        return secureToken;
    }

    public async Task<BulkSendResultDto> SendExamBulkAsync(BulkSendExamDto dto, string baseUrl, CancellationToken ct = default)
    {
        var exam = await _examRepo.GetByIdAsync(dto.ExamId, ct)
            ?? throw new InvalidOperationException("Examen no encontrado.");

        var results = new List<BulkSendItemResultDto>();

        foreach (var candidate in dto.Candidates)
        {
            try
            {
                var secureToken = _tokenService.GenerateSecureToken();
                var expiresAt = DateTime.UtcNow.AddHours(dto.ExpirationHours);

                var examToken = new ExamToken
                {
                    Token = secureToken,
                    ExamId = dto.ExamId,
                    CandidateName = candidate.Name,
                    CandidateEmail = candidate.Email,
                    ExpiresAt = expiresAt
                };

                await _tokenRepo.AddAsync(examToken, ct);

                var examLink = $"{baseUrl}/prueba/{secureToken}";
                await _emailService.SendExamInvitationAsync(
                    candidate.Email, candidate.Name,
                    exam.Title, examLink, expiresAt, ct);

                results.Add(new BulkSendItemResultDto(candidate.Name, candidate.Email, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new BulkSendItemResultDto(candidate.Name, candidate.Email, false, ex.Message));
            }
        }

        return new BulkSendResultDto(
            results.Count(r => r.Success),
            results.Count(r => !r.Success),
            results);
    }

    public async Task<ExamTokenValidationDto> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        var examToken = await _tokenRepo.GetWithExamAndSessionAsync(token, ct);
        if (examToken is null)
            return new ExamTokenValidationDto(false, "Token no válido.", null, null, null, null);

        // El estado de la sesión manda sobre el del token. Un token usado con la sesión
        // todavía abierta es el caso normal de quien recarga la página, y debe poder volver.
        if (IsSessionFinished(examToken))
            return new ExamTokenValidationDto(false, "Este examen ya ha sido completado.", null, null, null, null);

        // ExpiresAt es el plazo para EMPEZAR, no para terminar: una prueba ya abierta la
        // gobierna su tiempo límite, así que la expiración solo cierra la puerta de entrada.
        if (!HasOpenSession(examToken) && examToken.IsExpired)
            return new ExamTokenValidationDto(false, "El enlace ha expirado.", null, null, null, null);

        var user = await GetOrCreateStudentAsync(examToken.CandidateEmail, examToken.CandidateName, ct);
        if (examToken.UserId != user.Id)
        {
            examToken.UserId = user.Id;
            await _tokenRepo.UpdateAsync(examToken, ct);
        }

        var authToken = _tokenService.GenerateJwtToken(user.Id, user.Email, isAdmin: false);

        return new ExamTokenValidationDto(
            true, null,
            examToken.ExamSession?.Id,
            examToken.Exam.Title,
            examToken.CandidateName,
            authToken);
    }

    private async Task<User> GetOrCreateStudentAsync(string email, string name, CancellationToken ct)
    {
        var existing = await _userRepo.FindAsync(u => u.Email == email, ct);
        if (existing.Count > 0) return existing[0];

        var username = email.Split('@')[0];
        var user = new User
        {
            Email = email,
            Username = username,
            Name = name,
            // Sin contraseña utilizable a propósito. Derivarla del email convertía un dato
            // que circula en cualquier proceso de selección en la llave del portal del
            // candidato. Su acceso es el enlace de la invitación, que ya lo autentica.
            PasswordHash = string.Empty,
            IsAdmin = false,
            IsActive = true
        };
        return await _userRepo.AddAsync(user, ct);
    }

    public async Task<ExamSessionInfoDto?> StartSessionAsync(string token, CancellationToken ct = default)
    {
        var examToken = await _tokenRepo.GetWithExamAndSessionAsync(token, ct);
        if (examToken is null) return null;

        // Reanudación: la sesión abierta se devuelve tal cual, sin tocar StartedAt ni las
        // respuestas ya guardadas. Va antes que CanStart porque un token con sesión está
        // usado por definición, y CanStart lo rechazaría.
        if (HasOpenSession(examToken))
            return BuildSessionInfo(examToken);

        if (!examToken.CanStart) return null;

        examToken.IsUsed = true;
        examToken.UsedAt = DateTime.UtcNow;

        var session = new ExamSession { ExamTokenId = examToken.Id };
        await _sessionRepo.AddAsync(session, ct);
        await _tokenRepo.UpdateAsync(examToken, ct);

        examToken.ExamSession = session;
        return BuildSessionInfo(examToken);
    }

    public async Task SaveDraftAnswerAsync(
        int sessionId, int userId, SubmitAnswerDto dto, CancellationToken ct = default)
    {
        var token = await _tokenRepo.GetBySessionIdAsync(sessionId, ct)
            ?? throw new SessionAccessDeniedException("Sesión no encontrada.");

        EnsureOwnedBy(token, userId);

        // Guardar fuera de plazo es lo que hace posible congelar el temporizador del
        // navegador y seguir trabajando. El envío tardío sí se acepta, pero vacío.
        if (IsPastDeadline(token))
            throw new ExamTimeExpiredException("El tiempo de la prueba ha terminado.");

        var existing = await _answerRepo.FindAsync(
            a => a.ExamSessionId == sessionId && a.QuestionId == dto.QuestionId, ct);

        if (existing.Any())
        {
            var answer = existing.First();
            answer.SelectedAnswerId = dto.SelectedAnswerId;
            answer.OpenAnswer = dto.OpenAnswer;
            answer.AnsweredAt = DateTime.Now;
            await _answerRepo.UpdateAsync(answer, ct);
        }
        else
        {
            await _answerRepo.AddAsync(new UserAnswer
            {
                ExamSessionId = sessionId,
                QuestionId = dto.QuestionId,
                SelectedAnswerId = dto.SelectedAnswerId,
                OpenAnswer = dto.OpenAnswer
            }, ct);
        }
    }

    public async Task<ExamSubmissionReceiptDto> SubmitExamAsync(
        SubmitExamDto dto, int userId, CancellationToken ct = default)
    {
        var sessions = await _sessionRepo.FindAsync(s => s.Id == dto.SessionId, ct);
        var session = sessions.FirstOrDefault()
            ?? throw new InvalidOperationException("Sesión no encontrada.");

        var examToken = await _tokenRepo.GetWithExamAndSessionAsync(
            (await _tokenRepo.GetByIdAsync(session.ExamTokenId, ct))!.Token, ct)!
            ?? throw new InvalidOperationException("Token no encontrado.");

        var exam = await _examRepo.GetWithQuestionsAsync(examToken.ExamId, ct)!
            ?? throw new InvalidOperationException("Examen no encontrado.");

        EnsureOwnedBy(examToken, userId);

        // Fuera de plazo el envío se acepta, pero vacío: la prueba se cierra y se puntúa con
        // lo que ya estuviera guardado. Rechazarlo castigaría al candidato al que se le cayó
        // la conexión, que perdería el examen entero por un corte de red. Aceptar su
        // contenido dejaría entrar por aquí lo que `answer` ya no deja escribir.
        bool fueraDePlazo = IsPastDeadline(examToken, exam.TimeLimitMinutes);

        // El envío es idempotente. El temporizador y un clic del candidato pueden coincidir,
        // y ExamResult es uno a uno con ExamSession: el segundo INSERT reventaría. Se mira el
        // resultado y no el estado de la sesión, para cubrir también el caso en que el envío
        // anterior se interrumpió entre escribir el resultado y marcar la sesión cerrada.
        var yaEnviado = await _resultRepo.FindAsync(r => r.ExamSessionId == session.Id, ct);
        if (yaEnviado.Count > 0)
            return BuildReceipt(yaEnviado[0], exam.Title);

        // El estado lo decide la composición del examen, no lo que responda el candidato:
        // así es predecible desde que se monta la prueba y un humano siempre valida
        // una prueba con abiertas, incluso si el candidato no escribió nada.
        bool hasOpenQuestions = exam.ExamQuestions
            .Any(eq => eq.Question?.Type == Domain.Enums.QuestionType.OpenEnded);

        int totalPoints = exam.ExamQuestions.Sum(eq => eq.Question?.Points ?? 0);
        int obtained = 0;

        decimal pct = 0;
        var status = hasOpenQuestions
            ? Domain.Enums.ExamResultStatus.PendingReview
            : Domain.Enums.ExamResultStatus.Reviewed;
        bool? passed = null;
        ExamResult result = null!;

        // Respuestas, resultado y cierre de la sesión en una sola transacción. Sin ella, el
        // repositorio confirma en cada operación y un fallo a mitad dejaba una sesión con
        // resultado y todavía marcada InProgress: la ventana que los arreglos de la
        // reanudación y del envío repetido tuvieron que tolerar.
        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            obtained = await WriteAnswersAsync(dto, exam, session, fueraDePlazo, token);

            pct = totalPoints > 0 ? Math.Round((decimal)obtained / totalPoints * 100, 2) : 0;
            passed = status == Domain.Enums.ExamResultStatus.Reviewed
                ? pct >= exam.PassingScorePercentage
                : null;

            result = new ExamResult
            {
                ExamSessionId = session.Id,
                ExamId = exam.Id,
                CandidateName = examToken.CandidateName,
                CandidateEmail = examToken.CandidateEmail,
                UserId = examToken.UserId,
                TotalPoints = totalPoints,
                ObtainedPoints = obtained,
                ScorePercentage = pct,
                Passed = passed,
                Status = status
            };

            await _resultRepo.AddAsync(result, token);

            session.Status = Domain.Enums.SessionStatus.Completed;
            session.CompletedAt = DateTime.UtcNow;
            await _sessionRepo.UpdateAsync(session, token);
        }, ct);

        // El correo va después de confirmar, y fuera de la transacción: mantenerla abierta
        // mientras se espera al SMTP bloquearía filas durante segundos.
        if (status == Domain.Enums.ExamResultStatus.PendingReview)
        {
            // Acuse sin cifras: el resultado definitivo se envía al cerrar la corrección.
            await _emailService.SendExamPendingReviewAsync(
                examToken.CandidateEmail, examToken.CandidateName, exam.Title, ct);

            return BuildReceipt(result, exam.Title);
        }

        await _emailService.SendExamResultAsync(
            examToken.CandidateEmail, examToken.CandidateName,
            exam.Title, pct, passed!.Value, ct);

        return BuildReceipt(result, exam.Title);
    }

    /// <summary>
    /// Escribe la respuesta de cada pregunta del examen y acumula los puntos de las de test.
    /// </summary>
    private async Task<int> WriteAnswersAsync(
        SubmitExamDto dto, Exam exam, ExamSession session, bool fueraDePlazo, CancellationToken ct)
    {
        int acumulado = 0;

        foreach (var eq in exam.ExamQuestions.OrderBy(eq => eq.Order))
        {
            var q = eq.Question!;

            var existing = await _answerRepo.FindAsync(
                a => a.ExamSessionId == session.Id && a.QuestionId == q.Id, ct);
            var guardada = existing.FirstOrDefault();

            // Fuera de plazo se puntúa lo que ya estuviera guardado y se descarta lo que
            // traiga el envío. El contenido guardado se conserva tal cual: sustituirlo por
            // el del envío dejaría entrar por aquí lo que `answer` ya no deja escribir, y
            // sustituirlo por nada destruiría el trabajo legítimo del candidato.
            var submitted = fueraDePlazo
                ? (guardada is null
                    ? null
                    : new SubmitAnswerDto(q.Id, guardada.SelectedAnswerId, guardada.OpenAnswer))
                : dto.Answers.FirstOrDefault(a => a.QuestionId == q.Id);

            bool? isCorrect = null;
            int? awardedPoints = null;

            if (q.Type == Domain.Enums.QuestionType.MultipleChoice)
            {
                var selectedAnswer = submitted?.SelectedAnswerId is int sel
                    ? q.Answers.FirstOrDefault(a => a.Id == sel)
                    : null;
                isCorrect = selectedAnswer?.IsCorrect ?? false;
                // Congelado aquí: recalcular más tarde contra Question.Points daría otra
                // nota si alguien edita la pregunta entre el envío y la corrección.
                awardedPoints = isCorrect == true ? q.Points : 0;
                acumulado += awardedPoints.Value;
            }
            else if (string.IsNullOrWhiteSpace(submitted?.OpenAnswer))
            {
                // En blanco: se pre-puntúa a 0 como valor por defecto que el corrector
                // confirmará. No exime al resultado de pasar por la cola.
                awardedPoints = 0;
            }

            if (guardada is not null)
            {
                var answer = guardada;
                answer.SelectedAnswerId = submitted?.SelectedAnswerId;
                answer.OpenAnswer = submitted?.OpenAnswer;
                answer.IsCorrect = isCorrect;
                answer.AwardedPoints = awardedPoints;
                answer.AnsweredAt = DateTime.UtcNow;
                await _answerRepo.UpdateAsync(answer, ct);
            }
            else
            {
                await _answerRepo.AddAsync(new UserAnswer
                {
                    ExamSessionId = session.Id,
                    QuestionId = q.Id,
                    SelectedAnswerId = submitted?.SelectedAnswerId,
                    OpenAnswer = submitted?.OpenAnswer,
                    IsCorrect = isCorrect,
                    AwardedPoints = awardedPoints,
                    AnsweredAt = DateTime.UtcNow
                }, ct);
            }
        }

        return acumulado;
    }

    /// <summary>
    /// Acuse de un resultado ya guardado. Un resultado pendiente de corrección viaja sin
    /// cifras: adelantar la puntuación parcial sería comunicarle al candidato una nota falsa.
    /// </summary>
    private static ExamSubmissionReceiptDto BuildReceipt(ExamResult result, string examTitle)
    {
        // SQL Server devuelve DateTime sin zona, así que el acuse de un resultado releído
        // perdería la marca UTC que sí lleva el del primer envío. El cliente vería dos
        // horas distintas para el mismo hecho. Sobre un valor ya UTC no cambia nada.
        var completedAt = DateTime.SpecifyKind(result.CompletedAt, DateTimeKind.Utc);

        return result.Status == Domain.Enums.ExamResultStatus.PendingReview
            ? new ExamSubmissionReceiptDto(
                result.Id, examTitle, result.Status,
                null, null, null, null, completedAt)
            : new ExamSubmissionReceiptDto(
                result.Id, examTitle, result.Status,
                result.TotalPoints, result.ObtainedPoints,
                result.ScorePercentage, result.Passed, completedAt);
    }

    /// <summary>
    /// La sesión existe y el candidato todavía puede volver a ella.
    /// El resultado se comprueba además del estado porque SubmitExamAsync los escribe en dos
    /// confirmaciones distintas: si el proceso cae entre ambas, queda una sesión con
    /// ExamResult y todavía marcada InProgress. Sin esta comprobación esa sesión se daría
    /// por reanudable, y el segundo envío chocaría con la relación uno a uno del resultado.
    /// </summary>
    private static bool HasOpenSession(ExamToken token)
        => token.ExamSession is not null
        && token.ExamSession.Status == Domain.Enums.SessionStatus.InProgress
        && token.ExamSession.ExamResult is null;

    /// <summary>La prueba se envió: ya no se entra, ni con el enlace ni desde el portal.</summary>
    private static bool IsSessionFinished(ExamToken token)
        => token.ExamSession is not null && !HasOpenSession(token);

    /// <summary>
    /// Margen que absorbe la latencia de la red y el auto-envío del temporizador, que
    /// dispara en el cero exacto del cliente. Holgado para cualquier latencia real y
    /// despreciable como ventana de fraude.
    /// </summary>
    private static readonly TimeSpan GraciaDePlazo = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Comprueba que la sesión es de quien llama. Un `UserId` nulo se trata como ajeno:
    /// dueño desconocido es dueño distinto, y un fallo de autorización debe cerrar.
    /// </summary>
    private static void EnsureOwnedBy(ExamToken token, int userId)
    {
        if (token.UserId is null || token.UserId != userId)
            throw new SessionAccessDeniedException("Esta sesión de examen no te pertenece.");
    }

    /// <summary>El plazo del examen, medido por el reloj del servidor y con su margen.</summary>
    private static bool IsPastDeadline(ExamToken token, int? timeLimitMinutes = null)
    {
        var minutos = timeLimitMinutes ?? token.Exam?.TimeLimitMinutes ?? 0;
        if (token.ExamSession is null || minutos <= 0) return false;

        var limite = token.ExamSession.StartedAt.AddMinutes(minutos).Add(GraciaDePlazo);
        return DateTime.UtcNow > limite;
    }

    private static ExamSessionInfoDto BuildSessionInfo(ExamToken token) => new(
        token.ExamSession!.Id,
        token.Exam.Title,
        token.CandidateName,
        token.Exam.TimeLimitMinutes,
        token.ExamSession.StartedAt,
        RemainingSecondsOf(token),
        token.Exam.ExamQuestions.OrderBy(eq => eq.Order).Select(eq => new SessionQuestionDto(
            eq.QuestionId,
            eq.Question!.Text,
            eq.Question.Type,
            eq.Question.Points,
            eq.Order,
            eq.Question.Answers.OrderBy(a => a.Order)
                .Select(a => new AnswerOptionDto(a.Id, a.Text, a.Order)).ToList()
        )).ToList(),
        SavedAnswersOf(token));

    /// <summary>
    /// Tiempo que le queda al candidato, medido por el reloj del servidor. Nunca negativo:
    /// quien vuelve fuera de plazo recibe cero y el cliente envía de inmediato.
    /// </summary>
    private static int RemainingSecondsOf(ExamToken token)
    {
        var deadline = token.ExamSession!.StartedAt.AddMinutes(token.Exam.TimeLimitMinutes);
        var remaining = (deadline - DateTime.UtcNow).TotalSeconds;
        return remaining <= 0 ? 0 : (int)Math.Floor(remaining);
    }

    /// <summary>
    /// Lo que el candidato ya tenía guardado. Sin esto, un envío posterior a la recarga
    /// mandaría borradores vacíos y borraría su trabajo, porque SubmitExamAsync escribe
    /// la respuesta de cada pregunta del examen, esté o no rellena.
    /// </summary>
    private static List<SubmitAnswerDto> SavedAnswersOf(ExamToken token)
        => token.ExamSession!.UserAnswers
            .OrderBy(ua => ua.QuestionId)
            .Select(ua => new SubmitAnswerDto(ua.QuestionId, ua.SelectedAnswerId, ua.OpenAnswer))
            .ToList();
}
