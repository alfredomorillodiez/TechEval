using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TechEval.Application;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;
using TechEval.Infrastructure.Data;

namespace TechEval.Tests.Services;

/// <summary>
/// Tres defectos del envío de la prueba: el correo caído que devolvía 500 con la prueba ya
/// guardada (D11), el autoguardado sobre una sesión ya enviada (S8) y las dos filas para la
/// misma pregunta cuando dos autoguardados coincidían (D12).
/// </summary>
public class SubmissionHardeningTests
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

    public SubmissionHardeningTests()
    {
        _answerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAnswer>());
        _resultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExamResult>());
        _resultRepo.Setup(r => r.AddAsync(It.IsAny<ExamResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExamResult r, CancellationToken _) => { r.Id = 99; return r; });
        _tokenRepo.Setup(r => r.GetByIdAsync(1, default))
            .ReturnsAsync(new ExamToken { Id = 1, Token = "tok" });

        _sut = new ExamTokenService(
            _tokenRepo.Object, _examRepo.Object, _sessionRepo.Object, _answerRepo.Object,
            _resultRepo.Object, _userRepo.Object, _email.Object, _tokens.Object, _uow);
    }

    /// <summary>Prueba de 60 minutos empezada hace diez. Con o sin una pregunta abierta.</summary>
    private ExamSession ConfigurarSesion(bool conAbierta, SessionStatus estado = SessionStatus.InProgress,
        int minutosDesdeElInicio = 10, int duenno = Duenno)
    {
        var pregunta = new Question
        {
            Id = 2, Text = "Pregunta", Points = 5,
            Type = conAbierta ? QuestionType.OpenEnded : QuestionType.MultipleChoice,
            Answers = conAbierta
                ? new List<Answer>()
                : new List<Answer> { new() { Id = 21, Text = "Sí", IsCorrect = true }, new() { Id = 22, Text = "No" } }
        };
        var exam = new Exam
        {
            Id = 7, Title = "Prueba", TimeLimitMinutes = 60, PassingScorePercentage = 50,
            ExamQuestions = new List<ExamQuestion> { new() { QuestionId = 2, Order = 1, Question = pregunta } }
        };
        var session = new ExamSession
        {
            Id = 1, ExamTokenId = 1, Status = estado,
            StartedAt = DateTime.UtcNow.AddMinutes(-minutosDesdeElInicio)
        };
        var token = new ExamToken
        {
            Id = 1, Token = "tok", ExamId = 7, Exam = exam, UserId = duenno,
            CandidateName = "Ana", CandidateEmail = "ana@test.com", ExamSession = session
        };

        _sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamSession, bool>>>(), default))
            .ReturnsAsync(new List<ExamSession> { session });
        _tokenRepo.Setup(r => r.GetBySessionIdAsync(1, default)).ReturnsAsync(token);
        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", default)).ReturnsAsync(token);
        _examRepo.Setup(r => r.GetWithQuestionsAsync(7, default)).ReturnsAsync(exam);
        return session;
    }

    private void ExisteResultado()
        => _resultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExamResult> { new() { Id = 99, ExamSessionId = 1 } });

    private static SubmitAnswerDto Test(int opcion = 21) => new(2, opcion, null);

    private static SubmitExamDto Envio(params SubmitAnswerDto[] respuestas)
        => new(1, respuestas.ToList());

    // ---------- D11: el correo caído no cambia la respuesta ----------

    [Fact]
    public async Task Enviar_PruebaDeTestConElCorreoCaido_DevuelveElAcuseConCifras()
    {
        var session = ConfigurarSesion(conAbierta: false);
        _email.Setup(e => e.SendExamResultAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Graph caído"));

        var acuse = await _sut.SubmitExamAsync(Envio(Test()), Duenno);

        acuse.Status.Should().Be(ExamResultStatus.Reviewed);
        acuse.ScorePercentage.Should().Be(100);
        acuse.Passed.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Completed, "el cierre ya estaba confirmado");
        _resultRepo.Verify(r => r.AddAsync(It.IsAny<ExamResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Enviar_PruebaConAbiertasConElCorreoCaido_DevuelveElAcusePendienteSinCifras()
    {
        var session = ConfigurarSesion(conAbierta: true);
        _email.Setup(e => e.SendExamPendingReviewAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Graph caído"));

        var acuse = await _sut.SubmitExamAsync(Envio(new SubmitAnswerDto(2, null, "texto")), Duenno);

        acuse.Status.Should().Be(ExamResultStatus.PendingReview);
        acuse.ScorePercentage.Should().BeNull("el resultado pendiente viaja sin cifras");
        session.Status.Should().Be(SessionStatus.Completed);
    }

    [Fact]
    public async Task Enviar_SiElCorreoSeCancela_LaCancelacionLlegaAQuienLlama()
    {
        ConfigurarSesion(conAbierta: false);
        _email.Setup(e => e.SendExamResultAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.SubmitExamAsync(Envio(Test()), Duenno);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---------- S8: una prueba enviada no admite más respuestas ----------

    [Fact]
    public async Task Guardar_SesionConResultado_SeRechazaSinEscribirNada()
    {
        ConfigurarSesion(conAbierta: true);
        ExisteResultado();

        var act = () => _sut.SaveDraftAnswerAsync(1, Duenno, new SubmitAnswerDto(2, null, "cambio tardío"));

        (await act.Should().ThrowAsync<ExamAlreadySubmittedException>())
            .Which.Message.Should().Contain("ya se ha enviado");
        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()), Times.Never);
        _answerRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Guardar_SesionCompletadaSinResultadoAUnaVista_SeRechazaPorSuEstado()
    {
        ConfigurarSesion(conAbierta: true, estado: SessionStatus.Completed);

        var act = () => _sut.SaveDraftAnswerAsync(1, Duenno, new SubmitAnswerDto(2, null, "cambio tardío"));

        await act.Should().ThrowAsync<ExamAlreadySubmittedException>();
        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Guardar_SesionAjenaYEnviada_ManaLaPropiedad()
    {
        ConfigurarSesion(conAbierta: true, duenno: 99);
        ExisteResultado();

        var act = () => _sut.SaveDraftAnswerAsync(1, Duenno, new SubmitAnswerDto(2, null, "x"));

        await act.Should().ThrowAsync<SessionAccessDeniedException>();
    }

    [Fact]
    public async Task Guardar_SesionEnviadaFueraDePlazoYConTextoLargo_MandaElEstado()
    {
        ConfigurarSesion(conAbierta: true, minutosDesdeElInicio: 90);
        ExisteResultado();

        var act = () => _sut.SaveDraftAnswerAsync(1, Duenno,
            new SubmitAnswerDto(2, null, new string('x', UserAnswer.OpenAnswerMaxLength + 1)));

        await act.Should().ThrowAsync<ExamAlreadySubmittedException>();
    }

    [Fact]
    public async Task Guardar_SesionEnCurso_GuardaComoAntes()
    {
        ConfigurarSesion(conAbierta: true);
        UserAnswer? escrita = null;
        _answerRepo.Setup(r => r.AddAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAnswer a, CancellationToken _) => { escrita = a; return a; });

        await _sut.SaveDraftAnswerAsync(1, Duenno, new SubmitAnswerDto(2, null, "borrador"));

        escrita.Should().NotBeNull();
        escrita!.OpenAnswer.Should().Be("borrador");
    }

    // ---------- D12: una sola respuesta por pregunta y sesión ----------

    [Fact]
    public async Task Guardar_SiLaInsercionPierdeLaCarrera_ActualizaLaFilaDelGanador()
    {
        ConfigurarSesion(conAbierta: true);
        var ganadora = new UserAnswer { Id = 7, ExamSessionId = 1, QuestionId = 2, OpenAnswer = "la primera" };

        // Primera lectura: no hay fila. La inserción falla porque otra petición insertó antes.
        // Segunda lectura: la fila del ganador ya está.
        _answerRepo.SetupSequence(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAnswer>())
            .ReturnsAsync(new List<UserAnswer> { ganadora });
        _answerRepo.Setup(r => r.AddAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("índice único"));

        await _sut.SaveDraftAnswerAsync(1, Duenno, new SubmitAnswerDto(2, null, "la segunda"));

        ganadora.OpenAnswer.Should().Be("la segunda", "esta petición pasa a ser una corrección");
        _answerRepo.Verify(r => r.UpdateAsync(ganadora, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Guardar_SiLaInsercionFallaYNoHayFila_SeRelanzaElFalloOriginal()
    {
        ConfigurarSesion(conAbierta: true);
        _answerRepo.Setup(r => r.AddAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("base caída"));

        var act = () => _sut.SaveDraftAnswerAsync(1, Duenno, new SubmitAnswerDto(2, null, "x"));

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be("base caída");
        _answerRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Guardar_SiLaInsercionSeCancela_NoSeTrataComoCarrera()
    {
        ConfigurarSesion(conAbierta: true);
        _answerRepo.Setup(r => r.AddAsync(It.IsAny<UserAnswer>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => _sut.SaveDraftAnswerAsync(1, Duenno, new SubmitAnswerDto(2, null, "x"));

        await act.Should().ThrowAsync<OperationCanceledException>();
        // Solo la lectura inicial: no se relee tras una cancelación.
        _answerRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ElModelo_ImponeUnaRespuestaPorPreguntaYSesion()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=no-se-conecta;Database=TechEvalDb;")
            .Options;
        using var ctx = new AppDbContext(opciones);
        var respuestas = ctx.Model.FindEntityType(typeof(UserAnswer))!;

        var unico = respuestas.GetIndexes().Single(i => i.IsUnique);
        unico.Properties.Select(p => p.Name).Should().Equal(nameof(UserAnswer.ExamSessionId), nameof(UserAnswer.QuestionId));
        unico.GetDatabaseName().Should().Be("IX_UserAnswers_ExamSessionId_QuestionId");

        respuestas.GetIndexes().Should().Contain(i =>
            !i.IsUnique && i.Properties.Single().Name == nameof(UserAnswer.QuestionId)
            && i.GetDatabaseName() == "IX_UserAnswers_QuestionId");
    }

    [Fact]
    public void ElGuion_CreaLosMismosIndicesQueElModelo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TechEval.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull("la raíz del repositorio contiene TechEval.sln");

        var scripts = Path.Combine(dir!.FullName, "scripts");
        var completo = File.ReadAllText(Path.Combine(scripts, "create_database.sql"));
        var aditivo = File.ReadAllText(Path.Combine(scripts, "add_useranswers_unique_index.sql"));

        foreach (var guion in new[] { completo, aditivo })
        {
            guion.Should().Contain("CREATE UNIQUE INDEX IX_UserAnswers_ExamSessionId_QuestionId");
            guion.Should().Contain("CREATE INDEX IX_UserAnswers_QuestionId");
        }
    }
}
