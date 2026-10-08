using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;
using TechEval.API.Authorization;
using TechEval.API.Controllers;
using TechEval.Application;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// Señales de integridad: quién puede registrarlas, cuándo, con qué límites, y qué ve el
/// corrector después.
/// </summary>
public class ExamIntegrityServiceTests
{
    private readonly Mock<IExamTokenRepository> _tokenRepo = new();
    private readonly Mock<IExamRepository> _examRepo = new();
    private readonly Mock<IRepository<ExamSession>> _sessionRepo = new();
    private readonly Mock<IRepository<ExamIntegrityEvent>> _eventRepo = new();
    private readonly Mock<IExamResultRepository> _resultRepo = new();

    private readonly List<ExamIntegrityEvent> _guardadas = new();
    private int _senalesPrevias;
    private int _resultadosDeLaSesion;

    private readonly ExamIntegrityService _sut;

    public ExamIntegrityServiceTests()
    {
        _eventRepo.Setup(r => r.AddAsync(It.IsAny<ExamIntegrityEvent>(), default))
            .ReturnsAsync((ExamIntegrityEvent e, CancellationToken _) => { _guardadas.Add(e); return e; });
        _eventRepo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<ExamIntegrityEvent, bool>>>(), default))
            .ReturnsAsync(() => _senalesPrevias);
        _resultRepo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), default))
            .ReturnsAsync(() => _resultadosDeLaSesion);
        _examRepo.Setup(r => r.GetWithQuestionsAsync(7, default)).ReturnsAsync(Examen());

        _sut = new ExamIntegrityService(
            _tokenRepo.Object, _examRepo.Object, _sessionRepo.Object, _eventRepo.Object, _resultRepo.Object);
    }

    /// <summary>Examen de 60 minutos: la 1 es de test, la 4 es abierta.</summary>
    private static Exam Examen() => new()
    {
        Id = 7,
        Title = "Prueba",
        TimeLimitMinutes = 60,
        ExamQuestions = new List<ExamQuestion>
        {
            new() { QuestionId = 1, Order = 1, Question = new Question { Id = 1, Type = QuestionType.MultipleChoice } },
            new() { QuestionId = 4, Order = 2, Question = new Question { Id = 4, Type = QuestionType.OpenEnded } }
        }
    };

    private ExamSession ConfigurarSesion(int dueno = 5, SessionStatus estado = SessionStatus.InProgress)
    {
        var sesion = new ExamSession { Id = 42, ExamTokenId = 1, Status = estado, ShuffleSeed = 1 };
        _tokenRepo.Setup(r => r.GetBySessionIdAsync(42, default)).ReturnsAsync(new ExamToken
        {
            Id = 1, ExamId = 7, Exam = Examen(), UserId = dueno, ExamSession = sesion
        });
        return sesion;
    }

    // ---------- Registro ----------

    [Fact]
    public async Task Salida_EnSesionPropiaEnCurso_SeGuardaConHoraDelServidor()
    {
        ConfigurarSesion();
        var antes = DateTime.UtcNow;

        await _sut.RecordAsync(42, 5, new IntegrityEventInputDto(IntegrityEventType.PageLeft, 1, 999, 999));

        var e = _guardadas.Single();
        e.ExamSessionId.Should().Be(42);
        e.Type.Should().Be(IntegrityEventType.PageLeft);
        e.QuestionId.Should().Be(1);
        e.OccurredAt.Should().BeOnOrAfter(antes).And.BeOnOrBefore(DateTime.UtcNow);
        // Los datos de otros tipos no se guardan en una salida.
        e.AwaySeconds.Should().BeNull();
        e.PastedChars.Should().BeNull();
    }

    [Fact]
    public async Task Vuelta_GuardaLaDuracion()
    {
        ConfigurarSesion();

        await _sut.RecordAsync(42, 5, new IntegrityEventInputDto(IntegrityEventType.PageReturned, 1, 150, null));

        _guardadas.Single().AwaySeconds.Should().Be(150);
    }

    [Theory]
    [InlineData(-30, 0)]
    [InlineData(5 * 3600, 3600)]
    public async Task Vuelta_DuracionFueraDeRango_SeAcota(int informada, int guardada)
    {
        ConfigurarSesion();

        await _sut.RecordAsync(42, 5, new IntegrityEventInputDto(IntegrityEventType.PageReturned, 1, informada, null));

        _guardadas.Single().AwaySeconds.Should().Be(guardada);
    }

    [Fact]
    public async Task Pegado_EnAbierta_GuardaLaLongitud()
    {
        ConfigurarSesion();

        await _sut.RecordAsync(42, 5, new IntegrityEventInputDto(IntegrityEventType.Paste, 4, null, 820));

        var e = _guardadas.Single();
        e.QuestionId.Should().Be(4);
        e.PastedChars.Should().Be(820);
    }

    [Fact]
    public async Task Pegado_MasLargoQueUnaRespuesta_SeAcota()
    {
        ConfigurarSesion();

        await _sut.RecordAsync(42, 5, new IntegrityEventInputDto(IntegrityEventType.Paste, 4, null, 1_000_000));

        _guardadas.Single().PastedChars.Should().Be(ExamIntegrityService.MaxPastedChars);
    }

    [Theory]
    [InlineData(1)]     // de test
    [InlineData(99)]    // no es del examen
    public async Task Pegado_FueraDeUnaAbiertaDelExamen_SeRechaza(int questionId)
    {
        ConfigurarSesion();

        var act = () => _sut.RecordAsync(42, 5, new IntegrityEventInputDto(IntegrityEventType.Paste, questionId, null, 10));

        await act.Should().ThrowAsync<ValidationException>();
        _guardadas.Should().BeEmpty();
    }

    [Fact]
    public async Task SesionAjena_SeRechazaSinGuardar()
    {
        ConfigurarSesion(dueno: 6);

        var act = () => _sut.RecordAsync(42, 5, new IntegrityEventInputDto(IntegrityEventType.PageLeft, 1, null, null));

        await act.Should().ThrowAsync<SessionAccessDeniedException>();
        _guardadas.Should().BeEmpty();
    }

    [Fact]
    public async Task SesionInexistente_SeRechazaComoAjena()
    {
        var act = () => _sut.RecordAsync(404, 5, new IntegrityEventInputDto(IntegrityEventType.PageLeft, 1, null, null));

        await act.Should().ThrowAsync<SessionAccessDeniedException>();
    }

    [Fact]
    public async Task SesionTerminada_SeRechazaConConflicto()
    {
        ConfigurarSesion(estado: SessionStatus.Completed);

        var act = () => _sut.RecordAsync(42, 5, new IntegrityEventInputDto(IntegrityEventType.PageLeft, 1, null, null));

        await act.Should().ThrowAsync<ConflictException>();
        _guardadas.Should().BeEmpty();
    }

    [Fact]
    public async Task SesionEnCursoConResultado_SeRechazaConConflicto()
    {
        // El envío escribe el resultado y el estado en dos pasos: con resultado, la prueba
        // está enviada aunque la sesión siga marcada InProgress.
        ConfigurarSesion();
        _resultadosDeLaSesion = 1;

        var act = () => _sut.RecordAsync(42, 5, new IntegrityEventInputDto(IntegrityEventType.PageLeft, 1, null, null));

        await act.Should().ThrowAsync<ConflictException>();
        _guardadas.Should().BeEmpty();
    }

    [Fact]
    public async Task TipoDesconocido_SeRechaza()
    {
        ConfigurarSesion();

        var act = () => _sut.RecordAsync(42, 5, new IntegrityEventInputDto((IntegrityEventType)99, 1, null, null));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SenalPasadoElTope_NoSeGuardaYMarcaLaSesion()
    {
        var sesion = ConfigurarSesion();
        _senalesPrevias = ExamIntegrityService.MaxEventsPerSession;

        await _sut.RecordAsync(42, 5, new IntegrityEventInputDto(IntegrityEventType.PageLeft, 1, null, null));

        _guardadas.Should().BeEmpty();
        sesion.IntegrityLimitReached.Should().BeTrue();
        _sessionRepo.Verify(r => r.UpdateAsync(sesion, default), Times.Once);
    }

    [Fact]
    public async Task SenalPasadoElTope_NoVuelveAEscribirLaMarca()
    {
        var sesion = ConfigurarSesion();
        sesion.IntegrityLimitReached = true;
        _senalesPrevias = ExamIntegrityService.MaxEventsPerSession + 3;

        await _sut.RecordAsync(42, 5, new IntegrityEventInputDto(IntegrityEventType.PageLeft, 1, null, null));

        _sessionRepo.Verify(r => r.UpdateAsync(It.IsAny<ExamSession>(), default), Times.Never);
    }

    // ---------- Lectura para el corrector ----------

    private void ConfigurarResultado(int? semilla, bool limite = false, params ExamIntegrityEvent[] senales)
    {
        _resultRepo.Setup(r => r.GetByIdAsync(9, default))
            .ReturnsAsync(new ExamResult { Id = 9, ExamSessionId = 42, ExamId = 7 });
        _sessionRepo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(new ExamSession { Id = 42, ShuffleSeed = semilla, IntegrityLimitReached = limite });
        _eventRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamIntegrityEvent, bool>>>(), default))
            .ReturnsAsync(senales.ToList());
    }

    private static ExamIntegrityEvent Senal(int id, int minuto, IntegrityEventType tipo, int? pregunta,
        int? ausencia = null, int? caracteres = null) => new()
    {
        Id = id, ExamSessionId = 42, Type = tipo, QuestionId = pregunta,
        OccurredAt = new DateTime(2026, 9, 28, 10, minuto, 0, DateTimeKind.Utc),
        AwaySeconds = ausencia, PastedChars = caracteres
    };

    [Fact]
    public async Task Informe_ResumeYOrdenaLasSenales()
    {
        // Desordenadas a propósito: el informe las ordena por hora.
        ConfigurarResultado(semilla: 1, limite: false,
            Senal(7, 30, IntegrityEventType.Paste, 4, caracteres: 820),
            Senal(1, 1, IntegrityEventType.PageLeft, 1),
            Senal(2, 2, IntegrityEventType.PageReturned, 1, ausencia: 20),
            Senal(5, 20, IntegrityEventType.PageLeft, 4),
            Senal(6, 23, IntegrityEventType.PageReturned, 4, ausencia: 150),
            Senal(3, 10, IntegrityEventType.PageLeft, 1),
            Senal(4, 11, IntegrityEventType.PageReturned, 1, ausencia: 45));

        var informe = await _sut.GetReportAsync(9);

        informe.Availability.Should().Be(IntegrityAvailability.Recorded);
        informe.PageLeftCount.Should().Be(3);
        informe.TotalAwaySeconds.Should().Be(215);
        informe.PasteCount.Should().Be(1);
        informe.TotalPastedChars.Should().Be(820);
        informe.PastesByQuestion.Should().ContainSingle()
            .Which.Should().Be(new PastesByQuestionDto(4, 2, 1, 820));
        informe.Events.Should().HaveCount(7);
        informe.Events.Select(e => e.OccurredAt).Should().BeInAscendingOrder();
        informe.LimitReached.Should().BeFalse();
    }

    [Fact]
    public async Task Informe_SinSenales_EsRegistradoYVacio()
    {
        ConfigurarResultado(semilla: 1);

        var informe = await _sut.GetReportAsync(9);

        informe.Availability.Should().Be(IntegrityAvailability.Recorded);
        informe.Events.Should().BeEmpty();
        informe.PageLeftCount.Should().Be(0);
    }

    [Fact]
    public async Task Informe_SesionAnteriorAlCambio_NoSePresentaComoSinSenales()
    {
        ConfigurarResultado(semilla: null);

        var informe = await _sut.GetReportAsync(9);

        informe.Availability.Should().Be(IntegrityAvailability.NotRecorded);
    }

    [Fact]
    public async Task Informe_IndicaElLimiteAlcanzado()
    {
        ConfigurarResultado(semilla: 1, limite: true);

        var informe = await _sut.GetReportAsync(9);

        informe.LimitReached.Should().BeTrue();
    }

    [Fact]
    public async Task Informe_ResultadoInexistente_EsNoEncontrado()
    {
        var act = () => _sut.GetReportAsync(404);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---------- Acceso a los endpoints ----------

    private static string? PoliticaDe(MemberInfo m)
        => m.GetCustomAttribute<AuthorizeAttribute>()?.Policy;

    [Fact]
    public void EndpointDeRegistro_ExigeRolAlumno()
    {
        var metodo = typeof(ExamSessionController).GetMethod(nameof(ExamSessionController.RecordIntegrityEvent))!;

        PoliticaDe(metodo).Should().Be(Policies.Alumno);
    }

    [Fact]
    public void EndpointDeLectura_ExigeRolAdmin()
    {
        PoliticaDe(typeof(ResultsController)).Should().Be(Policies.Gestion);
        typeof(ResultsController).GetMethod(nameof(ResultsController.GetIntegrity))!
            .GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
    }
}
