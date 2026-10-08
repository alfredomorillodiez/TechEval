using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Xunit;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Tests.Services;

/// <summary>
/// Propiedad de la sesión y vigencia del plazo. Los identificadores de sesión son
/// secuenciales, así que el número por sí solo nunca puede valer como prueba de propiedad.
/// Y el reloj del cliente nunca puede ser la autoridad sobre el tiempo transcurrido.
/// </summary>
public class SessionAccessTests
{
    private const int Duenno = 5;
    private const int Intruso = 6;

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

    public SessionAccessTests()
    {
        _answerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), default))
            .ReturnsAsync(new List<UserAnswer>());
        _resultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), default))
            .ReturnsAsync(new List<ExamResult>());
        _resultRepo.Setup(r => r.AddAsync(It.IsAny<ExamResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExamResult r, CancellationToken _) => { r.Id = 99; return r; });
        _sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamSession, bool>>>(), default))
            .ReturnsAsync(new List<ExamSession> { new() { Id = 1, ExamTokenId = 1 } });
        _tokenRepo.Setup(r => r.GetByIdAsync(1, default))
            .ReturnsAsync(new ExamToken { Id = 1, Token = "tok" });

        _sut = new ExamTokenService(
            _tokenRepo.Object, _examRepo.Object, _sessionRepo.Object, _answerRepo.Object,
            _resultRepo.Object, _userRepo.Object, _email.Object, _tokens.Object, _uow);
    }

    /// <summary>Prueba de 60 minutos empezada hace `minutosDesdeElInicio`.</summary>
    private ExamToken ConfigurarSesion(int? duenno = Duenno, int minutosDesdeElInicio = 10)
    {
        var exam = new Exam
        {
            Id = 7, Title = "Prueba", TimeLimitMinutes = 60, PassingScorePercentage = 70,
            ExamQuestions = new List<ExamQuestion>
            {
                new()
                {
                    QuestionId = 1, Order = 1,
                    Question = new Question
                    {
                        Id = 1, Points = 5, Type = QuestionType.MultipleChoice,
                        Answers = new List<Answer> { new() { Id = 11, IsCorrect = true } }
                    }
                }
            }
        };

        var token = new ExamToken
        {
            Id = 1, Token = "tok", ExamId = 7, Exam = exam, UserId = duenno,
            CandidateName = "Ana", CandidateEmail = "ana@test.com",
            ExamSession = new ExamSession
            {
                Id = 1, ExamTokenId = 1, Status = SessionStatus.InProgress,
                StartedAt = DateTime.UtcNow.AddMinutes(-minutosDesdeElInicio)
            }
        };

        _tokenRepo.Setup(r => r.GetBySessionIdAsync(1, default)).ReturnsAsync(token);
        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", default)).ReturnsAsync(token);
        _examRepo.Setup(r => r.GetWithQuestionsAsync(7, default)).ReturnsAsync(exam);
        return token;
    }

    private static SubmitAnswerDto Respuesta() => new(1, 11, null);
    private static SubmitExamDto Envio() => new(1, new List<SubmitAnswerDto> { Respuesta() });

    // ---------- Propiedad ----------

    [Fact]
    public async Task Guardar_EnLaPropiaSesion_Funciona()
    {
        ConfigurarSesion();

        await _sut.SaveDraftAnswerAsync(1, Duenno, Respuesta());

        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), default), Times.Once);
    }

    [Fact]
    public async Task Guardar_EnLaSesionDeOtro_SeRechazaSinEscribir()
    {
        ConfigurarSesion();

        var act = () => _sut.SaveDraftAnswerAsync(1, Intruso, Respuesta());

        await act.Should().ThrowAsync<SessionAccessDeniedException>();
        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), default), Times.Never);
        _answerRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAnswer>(), default), Times.Never);
    }

    [Fact]
    public async Task Guardar_EnUnaSesionSinDuenno_SeRechaza()
    {
        // Dueño desconocido es dueño distinto: un fallo de autorización debe cerrar.
        ConfigurarSesion(duenno: null);

        var act = () => _sut.SaveDraftAnswerAsync(1, Duenno, Respuesta());

        await act.Should().ThrowAsync<SessionAccessDeniedException>();
        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), default), Times.Never);
    }

    [Fact]
    public async Task Guardar_EnUnaSesionInexistente_SeRechaza()
    {
        _tokenRepo.Setup(r => r.GetBySessionIdAsync(999, default)).ReturnsAsync((ExamToken?)null);

        var act = () => _sut.SaveDraftAnswerAsync(999, Duenno, Respuesta());

        await act.Should().ThrowAsync<SessionAccessDeniedException>();
    }

    [Fact]
    public async Task Enviar_LaSesionDeOtro_SeRechazaSinCrearResultado()
    {
        ConfigurarSesion();

        var act = () => _sut.SubmitExamAsync(Envio(), Intruso);

        await act.Should().ThrowAsync<SessionAccessDeniedException>();
        _resultRepo.Verify(r => r.AddAsync(It.IsAny<ExamResult>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessionRepo.Verify(r => r.UpdateAsync(It.IsAny<ExamSession>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Executed.Should().BeFalse();
    }

    [Fact]
    public async Task Enviar_UnaSesionSinDuenno_SeRechaza()
    {
        ConfigurarSesion(duenno: null);

        var act = () => _sut.SubmitExamAsync(Envio(), Duenno);

        await act.Should().ThrowAsync<SessionAccessDeniedException>();
    }

    // ---------- Plazo ----------

    [Fact]
    public async Task Guardar_DentroDePlazo_Escribe()
    {
        ConfigurarSesion(minutosDesdeElInicio: 30);

        await _sut.SaveDraftAnswerAsync(1, Duenno, Respuesta());

        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), default), Times.Once);
    }

    [Fact]
    public async Task Guardar_FueraDePlazo_SeRechazaSinEscribir()
    {
        // 90 minutos en una prueba de 60: pasado el límite y su margen.
        ConfigurarSesion(minutosDesdeElInicio: 90);

        var act = () => _sut.SaveDraftAnswerAsync(1, Duenno, Respuesta());

        await act.Should().ThrowAsync<ExamTimeExpiredException>();
        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), default), Times.Never);
    }

    [Fact]
    public async Task Guardar_DentroDelMargenDeGracia_Escribe()
    {
        // 60 minutos justos: el límite acaba de vencer, pero el margen aún cubre.
        ConfigurarSesion(minutosDesdeElInicio: 60);

        await _sut.SaveDraftAnswerAsync(1, Duenno, Respuesta());

        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), default), Times.Once);
    }

    [Fact]
    public async Task Enviar_FueraDePlazo_CierraConLoYaGuardadoEIgnoraElCuerpo()
    {
        ConfigurarSesion(minutosDesdeElInicio: 90);

        // Lo que ya estaba guardado: acertó la pregunta.
        var guardada = new UserAnswer { Id = 1, ExamSessionId = 1, QuestionId = 1, SelectedAnswerId = 11 };
        _answerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAnswer> { guardada });

        // Y ahora manda otra cosa, fuera de plazo.
        var receipt = await _sut.SubmitExamAsync(
            new SubmitExamDto(1, new List<SubmitAnswerDto> { new(1, 999, null) }), Duenno);

        receipt.ObtainedPoints.Should().Be(5, "se puntúa la respuesta guardada, no la que trae el envío");
        guardada.SelectedAnswerId.Should().Be(11, "el contenido guardado no se sustituye");
    }

    [Fact]
    public async Task Enviar_DentroDePlazo_EscribeLoQueTraeElEnvio()
    {
        ConfigurarSesion(minutosDesdeElInicio: 30);

        UserAnswer? escrita = null;
        _answerRepo.Setup(r => r.AddAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAnswer a, CancellationToken _) => { escrita = a; return a; });

        var receipt = await _sut.SubmitExamAsync(Envio(), Duenno);

        escrita.Should().NotBeNull();
        escrita!.SelectedAnswerId.Should().Be(11);
        receipt.ObtainedPoints.Should().Be(5);
    }
}
