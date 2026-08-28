using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Application.Services;

public interface IExamTokenService
{
    Task<string> SendExamAsync(SendExamDto dto, string baseUrl, CancellationToken ct = default);
    Task<BulkSendResultDto> SendExamBulkAsync(BulkSendExamDto dto, string baseUrl, CancellationToken ct = default);
    Task<ExamTokenValidationDto> ValidateTokenAsync(string token, CancellationToken ct = default);
    Task<ExamSessionInfoDto?> StartSessionAsync(string token, CancellationToken ct = default);
    Task<ExamSubmissionReceiptDto> SubmitExamAsync(SubmitExamDto dto, CancellationToken ct = default);
    Task SaveDraftAnswerAsync(int sessionId, SubmitAnswerDto answer, CancellationToken ct = default);
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

    public ExamTokenService(
        IExamTokenRepository tokenRepo,
        IExamRepository examRepo,
        IRepository<ExamSession> sessionRepo,
        IRepository<UserAnswer> answerRepo,
        IExamResultRepository resultRepo,
        IRepository<User> userRepo,
        IEmailService emailService,
        ITokenService tokenService)
    {
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
        if (examToken.IsExpired)
            return new ExamTokenValidationDto(false, "El enlace ha expirado.", null, null, null, null);
        if (examToken.IsUsed)
            return new ExamTokenValidationDto(false, "Este examen ya ha sido completado.", null, null, null, null);

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
            PasswordHash = PasswordHasher.Hash(username),
            IsAdmin = false,
            IsActive = true
        };
        return await _userRepo.AddAsync(user, ct);
    }

    public async Task<ExamSessionInfoDto?> StartSessionAsync(string token, CancellationToken ct = default)
    {
        var examToken = await _tokenRepo.GetWithExamAndSessionAsync(token, ct);
        if (examToken is null || !examToken.IsValid) return null;

        // Si ya existe sesión en progreso, devolverla
        if (examToken.ExamSession is not null)
            return BuildSessionInfo(examToken);

        examToken.IsUsed = true;
        examToken.UsedAt = DateTime.UtcNow;

        var session = new ExamSession { ExamTokenId = examToken.Id };
        await _sessionRepo.AddAsync(session, ct);
        await _tokenRepo.UpdateAsync(examToken, ct);

        examToken.ExamSession = session;
        return BuildSessionInfo(examToken);
    }

    public async Task SaveDraftAnswerAsync(int sessionId, SubmitAnswerDto dto, CancellationToken ct = default)
    {
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

    public async Task<ExamSubmissionReceiptDto> SubmitExamAsync(SubmitExamDto dto, CancellationToken ct = default)
    {
        var sessions = await _sessionRepo.FindAsync(s => s.Id == dto.SessionId, ct);
        var session = sessions.FirstOrDefault()
            ?? throw new InvalidOperationException("Sesión no encontrada.");

        var examToken = await _tokenRepo.GetWithExamAndSessionAsync(
            (await _tokenRepo.GetByIdAsync(session.ExamTokenId, ct))!.Token, ct)!
            ?? throw new InvalidOperationException("Token no encontrado.");

        var exam = await _examRepo.GetWithQuestionsAsync(examToken.ExamId, ct)!
            ?? throw new InvalidOperationException("Examen no encontrado.");

        // El estado lo decide la composición del examen, no lo que responda el candidato:
        // así es predecible desde que se monta la prueba y un humano siempre valida
        // una prueba con abiertas, incluso si el candidato no escribió nada.
        bool hasOpenQuestions = exam.ExamQuestions
            .Any(eq => eq.Question?.Type == Domain.Enums.QuestionType.OpenEnded);

        int totalPoints = 0, obtained = 0;

        foreach (var eq in exam.ExamQuestions.OrderBy(eq => eq.Order))
        {
            var q = eq.Question!;
            totalPoints += q.Points;
            var submitted = dto.Answers.FirstOrDefault(a => a.QuestionId == q.Id);

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
                obtained += awardedPoints.Value;
            }
            else if (string.IsNullOrWhiteSpace(submitted?.OpenAnswer))
            {
                // En blanco: se pre-puntúa a 0 como valor por defecto que el corrector
                // confirmará. No exime al resultado de pasar por la cola.
                awardedPoints = 0;
            }

            var existing = await _answerRepo.FindAsync(
                a => a.ExamSessionId == session.Id && a.QuestionId == q.Id, ct);

            if (existing.Any())
            {
                var answer = existing.First();
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

        decimal pct = totalPoints > 0 ? Math.Round((decimal)obtained / totalPoints * 100, 2) : 0;
        var status = hasOpenQuestions
            ? Domain.Enums.ExamResultStatus.PendingReview
            : Domain.Enums.ExamResultStatus.Reviewed;
        bool? passed = status == Domain.Enums.ExamResultStatus.Reviewed
            ? pct >= exam.PassingScorePercentage
            : null;

        var result = new ExamResult
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

        await _resultRepo.AddAsync(result, ct);

        session.Status = Domain.Enums.SessionStatus.Completed;
        session.CompletedAt = DateTime.UtcNow;
        await _sessionRepo.UpdateAsync(session, ct);

        if (status == Domain.Enums.ExamResultStatus.PendingReview)
        {
            // Acuse sin cifras: el resultado definitivo se envía al cerrar la corrección.
            await _emailService.SendExamPendingReviewAsync(
                examToken.CandidateEmail, examToken.CandidateName, exam.Title, ct);

            return new ExamSubmissionReceiptDto(
                result.Id, exam.Title, status,
                null, null, null, null, result.CompletedAt);
        }

        await _emailService.SendExamResultAsync(
            examToken.CandidateEmail, examToken.CandidateName,
            exam.Title, pct, passed!.Value, ct);

        return new ExamSubmissionReceiptDto(
            result.Id, exam.Title, status,
            totalPoints, obtained, pct, passed, result.CompletedAt);
    }

    private static ExamSessionInfoDto BuildSessionInfo(ExamToken token) => new(
        token.ExamSession!.Id,
        token.Exam.Title,
        token.CandidateName,
        token.Exam.TimeLimitMinutes,
        token.ExamSession.StartedAt,
        token.Exam.ExamQuestions.OrderBy(eq => eq.Order).Select(eq => new SessionQuestionDto(
            eq.QuestionId,
            eq.Question!.Text,
            eq.Question.Type,
            eq.Question.Points,
            eq.Order,
            eq.Question.Answers.OrderBy(a => a.Order)
                .Select(a => new AnswerOptionDto(a.Id, a.Text, a.Order)).ToList()
        )).ToList());
}
