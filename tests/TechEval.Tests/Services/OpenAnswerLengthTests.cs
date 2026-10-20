using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Xunit;
using TechEval.Application;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Tests.Services;

/// <summary>
/// Longitud de la respuesta abierta. La columna admite 4000 caracteres y antes nada lo
/// comprobaba: una respuesta más larga llegaba a la base de datos y salía como un 500, en el
/// autoguardado sin aviso y en el envío revirtiendo la prueba entera.
/// </summary>
public class OpenAnswerLengthTests
{
    private const int Duenno = 5;

    private readonly Mock<IExamTokenRepository> _tokenRepo = new();
    private readonly Mock<IExamRepository> _examRepo = new();
    private readonly Mock<IRepository<ExamSession>> _sessionRepo = new();
    private readonly Mock<IRepository<UserAnswer>> _answerRepo = new();
    private readonly Mock<IExamResultRepository> _resultRepo = new();
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly FakeUnitOfWork _uow = new();

    private readonly ExamTokenService _sut;

    public OpenAnswerLengthTests()
    {
        _answerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAnswer>());
        _resultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), default))
            .ReturnsAsync(new List<ExamResult>());
        _resultRepo.Setup(r => r.AddAsync(It.IsAny<ExamResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExamResult r, CancellationToken _) => { r.Id = 99; return r; });
        _tokenRepo.Setup(r => r.GetByIdAsync(1, default))
            .ReturnsAsync(new ExamToken { Id = 1, Token = "tok" });

        _sut = new ExamTokenService(
            _tokenRepo.Object, _examRepo.Object, _sessionRepo.Object, _answerRepo.Object,
            _resultRepo.Object, _userRepo.Object, _email.Object, _tokens.Object, _uow);
    }

    /// <summary>Prueba de 60 minutos con una pregunta abierta, empezada hace `minutosDesdeElInicio`.</summary>
    private ExamSession ConfigurarSesion(int minutosDesdeElInicio = 10)
    {
        var exam = new Exam
        {
            Id = 7, Title = "Prueba", TimeLimitMinutes = 60, PassingScorePercentage = 50,
            ExamQuestions = new List<ExamQuestion>
            {
                new()
                {
                    QuestionId = 2, Order = 1,
                    Question = new Question
                    {
                        Id = 2, Text = "Explica.", Points = 5, Type = QuestionType.OpenEnded,
                        Answers = new List<Answer>()
                    }
                }
            }
        };

        var session = new ExamSession
        {
            Id = 1, ExamTokenId = 1, Status = SessionStatus.InProgress,
            StartedAt = DateTime.UtcNow.AddMinutes(-minutosDesdeElInicio)
        };

        var token = new ExamToken
        {
            Id = 1, Token = "tok", ExamId = 7, Exam = exam, UserId = Duenno,
            CandidateName = "Ana", CandidateEmail = "ana@test.com", ExamSession = session
        };

        _sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamSession, bool>>>(), default))
            .ReturnsAsync(new List<ExamSession> { session });
        _tokenRepo.Setup(r => r.GetBySessionIdAsync(1, default)).ReturnsAsync(token);
        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", default)).ReturnsAsync(token);
        _examRepo.Setup(r => r.GetWithQuestionsAsync(7, default)).ReturnsAsync(exam);
        return session;
    }

    private UserAnswer GuardarAntes(string texto)
    {
        var guardada = new UserAnswer { Id = 1, ExamSessionId = 1, QuestionId = 2, OpenAnswer = texto };
        _answerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAnswer> { guardada });
        return guardada;
    }

    private static SubmitAnswerDto Abierta(int caracteres) => new(2, null, new string('x', caracteres));

    // ---------- Autoguardado ----------

    [Fact]
    public async Task Guardar_JustoEnElMaximo_GuardaLaRespuestaEntera()
    {
        ConfigurarSesion();
        UserAnswer? escrita = null;
        _answerRepo.Setup(r => r.AddAsync(It.IsAny<UserAnswer>(), default))
            .ReturnsAsync((UserAnswer a, CancellationToken _) => { escrita = a; return a; });

        await _sut.SaveDraftAnswerAsync(1, Duenno, Abierta(UserAnswer.OpenAnswerMaxLength));

        escrita.Should().NotBeNull();
        escrita!.OpenAnswer.Should().HaveLength(UserAnswer.OpenAnswerMaxLength, "no se recorta nada");
    }

    [Fact]
    public async Task Guardar_PorEncimaDelMaximo_SeRechazaSinTocarLoGuardado()
    {
        ConfigurarSesion();
        var guardada = GuardarAntes("respuesta anterior");

        var act = () => _sut.SaveDraftAnswerAsync(1, Duenno, Abierta(UserAnswer.OpenAnswerMaxLength + 1));

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Message.Should().Contain("4000 caracteres");
        guardada.OpenAnswer.Should().Be("respuesta anterior");
        _answerRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()), Times.Never);
        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Guardar_FueraDePlazoYPorEncimaDelMaximo_MandaElPlazo()
    {
        // El orden de los rechazos no cambia: primero el dueño, luego el plazo y luego el
        // contenido. Quien llega tarde sabe que llega tarde, no que su texto es largo.
        ConfigurarSesion(minutosDesdeElInicio: 90);

        var act = () => _sut.SaveDraftAnswerAsync(1, Duenno, Abierta(UserAnswer.OpenAnswerMaxLength + 1));

        await act.Should().ThrowAsync<ExamTimeExpiredException>();
    }

    // ---------- Envío ----------

    [Fact]
    public async Task Enviar_DentroDePlazoConUnaRespuestaDemasiadoLarga_SeRechazaSinEscribirNada()
    {
        var session = ConfigurarSesion(minutosDesdeElInicio: 30);
        var guardada = GuardarAntes("respuesta anterior");

        var act = () => _sut.SubmitExamAsync(
            new SubmitExamDto(1, new List<SubmitAnswerDto> { Abierta(UserAnswer.OpenAnswerMaxLength + 1) }), Duenno);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Message.Should().Contain("4000 caracteres");

        // Ni siquiera se abre la transacción: no hay nada a medias que revertir.
        _uow.Executed.Should().BeFalse();
        _resultRepo.Verify(r => r.AddAsync(It.IsAny<ExamResult>(), It.IsAny<CancellationToken>()), Times.Never);
        _answerRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()), Times.Never);
        guardada.OpenAnswer.Should().Be("respuesta anterior");

        // La sesión sigue abierta: el candidato puede acortar el texto y volver a enviar.
        session.Status.Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public async Task Enviar_FueraDePlazoConUnaRespuestaDemasiadoLarga_CierraConLoGuardado()
    {
        // Fuera de plazo el cuerpo se descarta. Rechazarlo por su longitud dejaría la prueba
        // sin cerrar a quien ya no puede escribir nada.
        var session = ConfigurarSesion(minutosDesdeElInicio: 90);
        var guardada = GuardarAntes("respuesta en plazo");

        var receipt = await _sut.SubmitExamAsync(
            new SubmitExamDto(1, new List<SubmitAnswerDto> { Abierta(UserAnswer.OpenAnswerMaxLength + 1) }), Duenno);

        receipt.Status.Should().Be(ExamResultStatus.PendingReview);
        guardada.OpenAnswer.Should().Be("respuesta en plazo", "se corrige lo guardado en plazo, no el cuerpo");
        session.Status.Should().Be(SessionStatus.Completed);
    }
}
