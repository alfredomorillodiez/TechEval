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
/// Las condiciones de una sesión (tiempo límite y nota de corte) se congelan al empezar, y las
/// sesiones que nadie envió se marcan abandonadas. Antes el plazo y el veredicto salían del valor
/// ACTUAL de la prueba: editarla con candidatos en curso les cambiaba el plazo.
/// </summary>
public class SessionTermsTests
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

    public SessionTermsTests()
    {
        _answerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAnswer>());
        _resultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExamResult>());
        _resultRepo.Setup(r => r.AddAsync(It.IsAny<ExamResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExamResult r, CancellationToken _) => { r.Id = 99; return r; });
        _tokenRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new ExamToken { Id = 1, Token = "tok" });

        _sut = new ExamTokenService(
            _tokenRepo.Object, _examRepo.Object, _sessionRepo.Object, _answerRepo.Object,
            _resultRepo.Object, _userRepo.Object, _email.Object, _tokens.Object, _uow);
    }

    /// <summary>
    /// Prueba de test con dos preguntas (3 y 1 puntos). `examMinutes` y `examPassing` son los valores
    /// ACTUALES de la prueba; la sesión lleva los suyos (`sessionMinutes`, `sessionPassing`), que
    /// pueden faltar (sesión anterior al cambio).
    /// </summary>
    private (ExamToken Token, ExamSession Session, Exam Exam) Montar(
        int examMinutes, int examPassing, int? sessionMinutes, int? sessionPassing,
        int minutosDesdeElInicio, SessionStatus estado = SessionStatus.InProgress)
    {
        Question Pregunta(int id, int puntos) => new()
        {
            Id = id, Text = $"P{id}", Points = puntos, Type = QuestionType.MultipleChoice,
            Answers = new List<Answer>
            {
                new() { Id = id * 10 + 1, Text = "Sí", IsCorrect = true, Order = 1 },
                new() { Id = id * 10 + 2, Text = "No", IsCorrect = false, Order = 2 }
            }
        };

        var exam = new Exam
        {
            Id = 7, Title = "Prueba", TimeLimitMinutes = examMinutes, PassingScorePercentage = examPassing,
            ExamQuestions = new List<ExamQuestion>
            {
                new() { QuestionId = 1, Order = 1, Question = Pregunta(1, 3) },
                new() { QuestionId = 2, Order = 2, Question = Pregunta(2, 1) }
            }
        };
        var session = new ExamSession
        {
            Id = 1, ExamTokenId = 1, Status = estado, ShuffleSeed = 1,
            StartedAt = DateTime.UtcNow.AddMinutes(-minutosDesdeElInicio),
            TimeLimitMinutesSnapshot = sessionMinutes, PassingScoreSnapshot = sessionPassing
        };
        var token = new ExamToken
        {
            Id = 1, Token = "tok", ExamId = 7, Exam = exam, UserId = Duenno, IsUsed = true,
            CandidateName = "Ana", CandidateEmail = "ana@test.com", ExamSession = session
        };

        _sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamSession, bool>>>(), default))
            .ReturnsAsync(new List<ExamSession> { session });
        _tokenRepo.Setup(r => r.GetBySessionIdAsync(1, default)).ReturnsAsync(token);
        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", default)).ReturnsAsync(token);
        _examRepo.Setup(r => r.GetWithQuestionsAsync(7, default)).ReturnsAsync(exam);
        return (token, session, exam);
    }

    private static SubmitExamDto EnvioConLaPrimeraAcertada()
        => new(1, new List<SubmitAnswerDto> { new(1, 11, null), new(2, 22, null) });   // 3 de 4 puntos = 75 %

    // ---------- Se guardan al empezar ----------

    [Fact]
    public async Task Empezar_GuardaElTiempoYLaNotaDeLaPrueba()
    {
        var examen = new Exam { Id = 7, Title = "P", TimeLimitMinutes = 45, PassingScorePercentage = 65, ExamQuestions = new List<ExamQuestion>() };
        var token = new ExamToken { Id = 1, Token = "tok", ExamId = 7, Exam = examen, ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", default)).ReturnsAsync(token);
        ExamSession? creada = null;
        _sessionRepo.Setup(r => r.AddAsync(It.IsAny<ExamSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExamSession s, CancellationToken _) => { s.Id = 1; creada = s; return s; });

        await _sut.StartSessionAsync("tok");

        creada.Should().NotBeNull();
        creada!.TimeLimitMinutesSnapshot.Should().Be(45);
        creada.PassingScoreSnapshot.Should().Be(65);
    }

    [Fact]
    public async Task Reanudar_NoVuelveACopiarLosValoresDeLaPrueba()
    {
        var (_, session, exam) = Montar(examMinutes: 30, examPassing: 80, sessionMinutes: 60, sessionPassing: 70, minutosDesdeElInicio: 10);

        await _sut.StartSessionAsync("tok");

        session.TimeLimitMinutesSnapshot.Should().Be(60);
        session.PassingScoreSnapshot.Should().Be(70);
        exam.TimeLimitMinutes.Should().Be(30, "la prueba sí cambió");
    }

    // ---------- Tiempo ----------

    [Fact]
    public async Task AcortarElTiempo_NoCambiaElPlazoDeUnaSesionEnCurso()
    {
        // Empezó hace 20 minutos con 60. Después la prueba pasa a 30: con el valor actual le
        // quedarían 10 minutos; con el suyo, 40.
        Montar(examMinutes: 30, examPassing: 70, sessionMinutes: 60, sessionPassing: 70, minutosDesdeElInicio: 20);

        var info = await _sut.StartSessionAsync("tok");

        info!.TimeLimitMinutes.Should().Be(60);
        info.RemainingSeconds.Should().BeInRange(39 * 60, 40 * 60);
    }

    [Fact]
    public async Task AlargarElTiempo_NoRegalaMinutosAUnaSesionEnCurso()
    {
        Montar(examMinutes: 120, examPassing: 70, sessionMinutes: 60, sessionPassing: 70, minutosDesdeElInicio: 50);

        var info = await _sut.StartSessionAsync("tok");

        info!.RemainingSeconds.Should().BeInRange(9 * 60, 10 * 60);
    }

    [Fact]
    public async Task SesionAnteriorAlCambio_UsaElTiempoActualDeLaPrueba()
    {
        Montar(examMinutes: 30, examPassing: 70, sessionMinutes: null, sessionPassing: null, minutosDesdeElInicio: 20);

        var info = await _sut.StartSessionAsync("tok");

        info!.TimeLimitMinutes.Should().Be(30);
        info.RemainingSeconds.Should().BeInRange(9 * 60, 10 * 60);
    }

    [Fact]
    public async Task EnviarEnElPlazoDeLaSesion_AunqueLaPruebaSeHayaAcortado_PuntuaLasRespuestas()
    {
        // Empezó hace 40 minutos con 60 (en plazo). La prueba ahora dura 30: con el valor actual el
        // envío saldría «fuera de plazo» y se puntuaría en blanco.
        var (_, session, _) = Montar(examMinutes: 30, examPassing: 70, sessionMinutes: 60, sessionPassing: 70, minutosDesdeElInicio: 40);

        var acuse = await _sut.SubmitExamAsync(EnvioConLaPrimeraAcertada(), Duenno);

        acuse.ScorePercentage.Should().Be(75, "el envío entró en el plazo de la sesión y se puntuó");
        session.Status.Should().Be(SessionStatus.Completed);
    }

    // ---------- Nota de corte ----------

    [Fact]
    public async Task SubirLaNota_NoCambiaElVeredictoDeQuienEmpezoConLaAnterior()
    {
        // 75 %. Empezó con 70 (aprueba). Ahora la prueba pide 80: con el valor actual suspendería.
        Montar(examMinutes: 60, examPassing: 80, sessionMinutes: 60, sessionPassing: 70, minutosDesdeElInicio: 10);

        var acuse = await _sut.SubmitExamAsync(EnvioConLaPrimeraAcertada(), Duenno);

        acuse.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task BajarLaNota_NoCambiaElVeredictoDeQuienEmpezoConLaAnterior()
    {
        // 75 %. Empezó con 80 (suspende). Ahora la prueba pide 70: con el valor actual aprobaría.
        Montar(examMinutes: 60, examPassing: 70, sessionMinutes: 60, sessionPassing: 80, minutosDesdeElInicio: 10);

        var acuse = await _sut.SubmitExamAsync(EnvioConLaPrimeraAcertada(), Duenno);

        acuse.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task SesionSinNotaGuardada_UsaLaNotaActualDeLaPrueba()
    {
        Montar(examMinutes: 60, examPassing: 80, sessionMinutes: null, sessionPassing: null, minutosDesdeElInicio: 10);

        var acuse = await _sut.SubmitExamAsync(EnvioConLaPrimeraAcertada(), Duenno);

        acuse.Passed.Should().BeFalse("75 % no llega al 80 % de la prueba");
    }

    [Fact]
    public async Task LaNotaCambiadaDuranteLaCorreccion_NoCambiaElVeredicto()
    {
        // Resultado de 10 pts con una abierta de 5; el corrector da 2 → 7 de 10 = 70 %.
        var resultRepo = new Mock<IExamResultRepository>();
        var examRepo = new Mock<IExamRepository>();
        // La prueba ahora pide 80; la sesión empezó con 70.
        examRepo.Setup(r => r.GetByIdAsync(7, default)).ReturnsAsync(new Exam { Id = 7, PassingScorePercentage = 80 });
        resultRepo.Setup(r => r.TryReserveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        resultRepo.Setup(r => r.TryReleaseForSubmitAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = new ExamResult
        {
            Id = 1, ExamId = 7, Status = ExamResultStatus.PendingReview, CandidateName = "Ana", CandidateEmail = "ana@test.com",
            Exam = new Exam { Id = 7, Title = "Prueba", PassingScorePercentage = 80 },
            TotalPoints = 10, ObtainedPoints = 5,
            ExamSession = new ExamSession
            {
                Id = 1, PassingScoreSnapshot = 70,
                UserAnswers = new List<UserAnswer>
                {
                    new() { Id = 100, QuestionId = 1, IsCorrect = true, AwardedPoints = 5,
                            Question = new Question { Id = 1, Points = 5, Type = QuestionType.MultipleChoice } },
                    new() { Id = 200, QuestionId = 2, OpenAnswer = "x",
                            Question = new Question { Id = 2, Points = 5, Type = QuestionType.OpenEnded } }
                }
            }
        };
        resultRepo.Setup(r => r.GetForReviewAsync(1, default)).ReturnsAsync(result);
        var resultService = new Mock<IResultService>();
        resultService.Setup(r => r.GetDetailAsync(It.IsAny<int>(), default)).ReturnsAsync(new ExamResultDto(
            1, "Ana", "ana@test.com", "Prueba", 10, 7, 70m, true, ExamResultStatus.Reviewed, DateTime.UtcNow, new List<AnswerReviewDto>()));
        var review = new OpenQuestionReviewService(
            resultRepo.Object, examRepo.Object, new Mock<IRepository<UserAnswer>>().Object,
            new Mock<IEmailService>().Object, resultService.Object, new FakeUnitOfWork());

        await review.SubmitReviewAsync(1,
            new SubmitReviewDto(new List<ReviewAnswerInputDto> { new(200, 2, null) }), reviewedByUserId: 42);

        result.ScorePercentage.Should().Be(70m);
        result.Passed.Should().BeTrue("la sesión empezó con 70; la prueba pida ahora 80 no le afecta");
    }

    [Fact]
    public void LosMetodosDeAcceso_PrefierenElValorGuardado()
    {
        var examen = new Exam { TimeLimitMinutes = 30, PassingScorePercentage = 80 };

        new ExamSession { TimeLimitMinutesSnapshot = 60, PassingScoreSnapshot = 70 }.TimeLimitFor(examen).Should().Be(60);
        new ExamSession { TimeLimitMinutesSnapshot = 60, PassingScoreSnapshot = 70 }.PassingScoreFor(examen).Should().Be(70);
        new ExamSession().TimeLimitFor(examen).Should().Be(30);
        new ExamSession().PassingScoreFor(examen).Should().Be(80);
    }

    // ---------- Sesiones abandonadas: el servicio ----------

    private static readonly TimeSpan Gracia = TimeSpan.FromHours(24);

    private (SessionMaintenanceService Sut, Mock<IRepository<ExamSession>> Sessions) Mantenimiento(params ExamToken[] tokens)
    {
        var tokenRepo = new Mock<IExamTokenRepository>();
        tokenRepo.Setup(r => r.GetInProgressAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tokens.ToList());
        var sessions = new Mock<IRepository<ExamSession>>();
        return (new SessionMaintenanceService(tokenRepo.Object, sessions.Object), sessions);
    }

    private static ExamToken ConSesion(
        int minutosDesdeElInicio, int examMinutes = 60, int? sessionMinutes = null, ExamResult? result = null)
    {
        var exam = new Exam { Id = 7, TimeLimitMinutes = examMinutes };
        var session = new ExamSession
        {
            Id = 1, Status = SessionStatus.InProgress, StartedAt = DateTime.UtcNow.AddMinutes(-minutosDesdeElInicio),
            TimeLimitMinutesSnapshot = sessionMinutes, ExamResult = result
        };
        return new ExamToken { Id = 1, Exam = exam, ExamSession = session };
    }

    [Fact]
    public async Task Vencida_HaceMasDeLaGracia_SeMarcaAbandonadaSinTocarSusRespuestas()
    {
        // Plazo de 60 minutos; empezó hace 60 + 25 horas.
        var token = ConSesion(minutosDesdeElInicio: 60 + 25 * 60);
        token.ExamSession!.UserAnswers.Add(new UserAnswer { Id = 1, OpenAnswer = "lo que escribió" });
        var (sut, sessions) = Mantenimiento(token);

        var marcadas = await sut.MarkAbandonedAsync(DateTime.UtcNow, Gracia);

        marcadas.Should().Be(1);
        token.ExamSession.Status.Should().Be(SessionStatus.Abandoned);
        token.ExamSession.CompletedAt.Should().BeNull("no se completó");
        token.ExamSession.UserAnswers.Single().OpenAnswer.Should().Be("lo que escribió");
        sessions.Verify(r => r.UpdateAsync(token.ExamSession, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Vencida_HaceMenosDeLaGracia_SigueEnCurso()
    {
        var token = ConSesion(minutosDesdeElInicio: 60 + 2 * 60);
        var (sut, sessions) = Mantenimiento(token);

        (await sut.MarkAbandonedAsync(DateTime.UtcNow, Gracia)).Should().Be(0);

        token.ExamSession!.Status.Should().Be(SessionStatus.InProgress);
        sessions.Verify(r => r.UpdateAsync(It.IsAny<ExamSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnPlazo_NoSeToca()
    {
        var token = ConSesion(minutosDesdeElInicio: 10);
        var (sut, _) = Mantenimiento(token);

        (await sut.MarkAbandonedAsync(DateTime.UtcNow, Gracia)).Should().Be(0);
        token.ExamSession!.Status.Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public async Task ElTiempoGuardadoMandaSobreElDeLaPrueba()
    {
        // Empezó hace 30 horas con 60 minutos guardados: vencida hace casi 30 horas → abandonada.
        // La prueba se editó después a 600 minutos (10 horas): con ese valor seguiría en plazo.
        var token = ConSesion(minutosDesdeElInicio: 30 * 60, examMinutes: 600, sessionMinutes: 60);
        var (sut, _) = Mantenimiento(token);

        (await sut.MarkAbandonedAsync(DateTime.UtcNow, Gracia)).Should().Be(1);
    }

    [Fact]
    public async Task ConResultado_NoSeMarca()
    {
        // Un envío interrumpido entre el resultado y el cierre deja resultado e InProgress.
        var token = ConSesion(minutosDesdeElInicio: 7 * 24 * 60, result: new ExamResult { Id = 9 });
        var (sut, _) = Mantenimiento(token);

        (await sut.MarkAbandonedAsync(DateTime.UtcNow, Gracia)).Should().Be(0);
        token.ExamSession!.Status.Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public async Task ElFalloDeUnaSesion_NoImpideMarcarLasDemas()
    {
        var mala = ConSesion(minutosDesdeElInicio: 5 * 24 * 60);
        mala.ExamSession!.Id = 1;
        var buena = ConSesion(minutosDesdeElInicio: 5 * 24 * 60);
        buena.ExamSession!.Id = 2;
        var (sut, sessions) = Mantenimiento(mala, buena);
        sessions.Setup(r => r.UpdateAsync(It.Is<ExamSession>(s => s.Id == 1), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("base caída"));

        var marcadas = await sut.MarkAbandonedAsync(DateTime.UtcNow, Gracia);

        marcadas.Should().Be(1);
        buena.ExamSession.Status.Should().Be(SessionStatus.Abandoned);
        mala.ExamSession.Status.Should().Be(SessionStatus.InProgress, "la que falló vuelve a su estado");
    }

    [Fact]
    public async Task LaCancelacionSePropaga()
    {
        var (sut, _) = Mantenimiento(ConSesion(minutosDesdeElInicio: 5 * 24 * 60));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.MarkAbandonedAsync(DateTime.UtcNow, Gracia, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(-3, 15, 24, 15)]
    [InlineData(24, 0, 24, 15)]
    [InlineData(24, -1, 24, 15)]
    [InlineData(0, 30, 0, 30)]
    [InlineData(48, 5, 48, 5)]
    public void Opciones_SustituyenLosValoresInvalidos(int horas, int minutos, int horasEsperadas, int minutosEsperados)
    {
        var o = new SessionSweepOptions { AbandonAfterHours = horas, SweepMinutes = minutos }.Normalize();

        (o.AbandonAfterHours, o.SweepMinutes).Should().Be((horasEsperadas, minutosEsperados));
    }

    [Fact]
    public void Opciones_PorDefecto_24HorasYQuinceMinutos()
    {
        var o = new SessionSweepOptions();

        (o.AbandonAfterHours, o.SweepMinutes).Should().Be((24, 15));
    }

    // ---------- Lo que ve el administrador ----------

    [Fact]
    public async Task ElListadoDePruebas_CuentaLasSesionesAbandonadas()
    {
        var examRepo = new Mock<IExamRepository>();
        ExamToken Con(SessionStatus? estado) => new()
        {
            ExamSession = estado is null ? null : new ExamSession { Status = estado.Value }
        };
        examRepo.Setup(r => r.GetWithStatsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Exam>
        {
            new() { Id = 1, Title = "Con abandonos", ExamTokens = new List<ExamToken>
                { Con(SessionStatus.Abandoned), Con(SessionStatus.Abandoned), Con(SessionStatus.Abandoned), Con(SessionStatus.InProgress), Con(null) } },
            new() { Id = 2, Title = "Sin abandonos", ExamTokens = new List<ExamToken> { Con(SessionStatus.Completed) } }
        });
        var servicio = new ExamService(examRepo.Object, new Mock<IQuestionRepository>().Object);

        var lista = await servicio.GetAllAsync();

        lista.Single(x => x.Id == 1).AbandonedCount.Should().Be(3);
        lista.Single(x => x.Id == 2).AbandonedCount.Should().Be(0);
    }

    // ---------- Una abandonada no se reanuda ni se escribe ----------

    [Fact]
    public async Task Validar_UnaAbandonada_DiceQueSeCerroPorInactividad()
    {
        Montar(60, 70, 60, 70, minutosDesdeElInicio: 5000, estado: SessionStatus.Abandoned);

        var validacion = await _sut.ValidateTokenAsync("tok");

        validacion.IsValid.Should().BeFalse();
        validacion.Error.Should().Contain("inactividad").And.Contain("invitación nueva");
        validacion.Error.Should().NotContain("completada");
    }

    [Fact]
    public async Task Validar_UnaCompletada_SigueDiciendoCompletada()
    {
        Montar(60, 70, 60, 70, minutosDesdeElInicio: 5, estado: SessionStatus.Completed);

        var validacion = await _sut.ValidateTokenAsync("tok");

        validacion.Error.Should().Contain("completada");
    }

    [Fact]
    public async Task Autoguardar_UnaAbandonada_Da409ConSuMensajeYNoEscribe()
    {
        Montar(60, 70, 60, 70, minutosDesdeElInicio: 5000, estado: SessionStatus.Abandoned);

        var act = () => _sut.SaveDraftAnswerAsync(1, Duenno, new SubmitAnswerDto(1, 11, null));

        (await act.Should().ThrowAsync<ExamAbandonedException>()).Which.Message.Should().Contain("inactividad");
        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Autoguardar_UnaAbandonadaAjena_MandaLaPropiedad()
    {
        Montar(60, 70, 60, 70, minutosDesdeElInicio: 5000, estado: SessionStatus.Abandoned);

        var act = () => _sut.SaveDraftAnswerAsync(1, 99, new SubmitAnswerDto(1, 11, null));

        await act.Should().ThrowAsync<SessionAccessDeniedException>();
    }

    [Fact]
    public async Task Enviar_UnaAbandonada_Da409YNoCreaResultado()
    {
        Montar(60, 70, 60, 70, minutosDesdeElInicio: 5000, estado: SessionStatus.Abandoned);

        var act = () => _sut.SubmitExamAsync(EnvioConLaPrimeraAcertada(), Duenno);

        (await act.Should().ThrowAsync<ExamAbandonedException>()).Which.Message.Should().Contain("inactividad");
        _resultRepo.Verify(r => r.AddAsync(It.IsAny<ExamResult>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Executed.Should().BeFalse();
    }

    [Fact]
    public async Task Enviar_UnaSesionConResultadoSigueSiendoIdempotente()
    {
        Montar(60, 70, 60, 70, minutosDesdeElInicio: 5, estado: SessionStatus.InProgress);
        _resultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExamResult>
            {
                new() { Id = 99, ExamSessionId = 1, Status = ExamResultStatus.Reviewed, Passed = true, ScorePercentage = 90 }
            });

        var acuse = await _sut.SubmitExamAsync(EnvioConLaPrimeraAcertada(), Duenno);

        acuse.ResultId.Should().Be(99);
    }
}
