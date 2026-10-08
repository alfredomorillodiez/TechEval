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
using TechEval.Tests;

namespace TechEval.Tests.Services;

/// <summary>
/// Entrada y vuelta a la prueba: cuándo un token da paso, cuándo lo cierra, y qué recibe
/// el candidato que reanuda tras una recarga.
/// </summary>
public class ExamTokenServiceTests
{
    private readonly Mock<IExamTokenRepository> _tokenRepo = new();
    private readonly Mock<IExamRepository> _examRepo = new();
    private readonly Mock<IRepository<ExamSession>> _sessionRepo = new();
    private readonly Mock<IRepository<UserAnswer>> _answerRepo = new();
    private readonly Mock<IExamResultRepository> _resultRepo = new();
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly FakeUnitOfWork _uow = new();

    private readonly List<ExamSession> _sessionsCreadas = new();

    private readonly ExamTokenService _sut;

    public ExamTokenServiceTests()
    {
        _userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), default))
            .ReturnsAsync(new List<User> { new() { Id = 5, Email = "ana@test.com", Name = "Ana" } });

        _tokens.Setup(t => t.GenerateJwtToken(It.Is<User>(u => u.Role == UserRole.Alumno)))
            .Returns("jwt-alumno");

        _sessionRepo.Setup(r => r.AddAsync(It.IsAny<ExamSession>(), default))
            .ReturnsAsync((ExamSession s, CancellationToken _) =>
            {
                s.Id = 50 + _sessionsCreadas.Count;
                _sessionsCreadas.Add(s);
                return s;
            });

        _sut = new ExamTokenService(
            _tokenRepo.Object, _examRepo.Object, _sessionRepo.Object, _answerRepo.Object,
            _resultRepo.Object, _userRepo.Object, _email.Object, _tokens.Object, _uow);
    }

    private static Exam ExamenDe(int minutos) => new()
    {
        Id = 7,
        Title = "Prueba",
        TimeLimitMinutes = minutos,
        ExamQuestions = new List<ExamQuestion>
        {
            new()
            {
                QuestionId = 1, Order = 1,
                Question = new Question
                {
                    Id = 1, Text = "¿Pregunta?", Type = QuestionType.MultipleChoice, Points = 5,
                    Answers = new List<Answer> { new() { Id = 11, Text = "Sí", IsCorrect = true, Order = 1 } }
                }
            },
            new()
            {
                QuestionId = 2, Order = 2,
                Question = new Question
                {
                    Id = 2, Text = "Explica.", Type = QuestionType.OpenEnded, Points = 5,
                    Answers = new List<Answer>()
                }
            }
        }
    };

    /// <summary>Arma el token que devuelve el repositorio para "tok".</summary>
    private ExamToken ConfigurarToken(
        bool usado = false,
        int horasHastaExpirar = 24,
        SessionStatus? estadoSesion = null,
        DateTime? inicioSesion = null,
        int minutosExamen = 60,
        bool conResultado = false,
        params UserAnswer[] respuestas)
    {
        var exam = ExamenDe(minutosExamen);

        var token = new ExamToken
        {
            Id = 1,
            Token = "tok",
            ExamId = 7,
            Exam = exam,
            CandidateName = "Ana",
            CandidateEmail = "ana@test.com",
            IsUsed = usado,
            ExpiresAt = DateTime.UtcNow.AddHours(horasHastaExpirar)
        };

        if (estadoSesion is not null)
        {
            token.ExamSession = new ExamSession
            {
                Id = 42,
                ExamTokenId = 1,
                Status = estadoSesion.Value,
                StartedAt = inicioSesion ?? DateTime.UtcNow,
                UserAnswers = respuestas.ToList(),
                ExamResult = conResultado ? new ExamResult { Id = 77, ExamSessionId = 42, ExamId = 7 } : null
            };
        }

        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", default)).ReturnsAsync(token);
        return token;
    }

    // ---------- Validación del token ----------

    [Fact]
    public async Task Validate_TokenSinSesion_EsValidoYSinIdentificadorDeSesion()
    {
        ConfigurarToken();

        var result = await _sut.ValidateTokenAsync("tok");

        result.IsValid.Should().BeTrue();
        result.SessionId.Should().BeNull();
        result.ExamTitle.Should().Be("Prueba");
    }

    [Fact]
    public async Task Validate_TokenSinSesion_TraeLoQueMuestraLaBienvenida()
    {
        // La bienvenida se pinta con esto. Si tuviera que pedir la sesión para saberlo, el
        // reloj empezaría a correr mientras el candidato lee.
        ConfigurarToken(minutosExamen: 45);

        var result = await _sut.ValidateTokenAsync("tok");

        result.QuestionCount.Should().Be(2);
        result.TimeLimitMinutes.Should().Be(45);
    }

    [Fact]
    public async Task Validate_NoIniciaLaPrueba_NiConsumeElToken()
    {
        // Quien abre el enlace solo para verlo y vuelve horas después tiene que encontrar la
        // prueba entera: validar no crea la sesión, que es la que lleva la hora de inicio.
        var token = ConfigurarToken(minutosExamen: 60);

        await _sut.ValidateTokenAsync("tok");
        await _sut.ValidateTokenAsync("tok");

        _sessionsCreadas.Should().BeEmpty();
        token.IsUsed.Should().BeFalse();
        token.UsedAt.Should().BeNull();

        var session = await _sut.StartSessionAsync("tok");

        session!.RemainingSeconds.Should().BeInRange(60 * 60 - 5, 60 * 60);
    }

    [Fact]
    public async Task Validate_TokenConSesionEnCurso_EsValidoYDevuelveLaSesion()
    {
        ConfigurarToken(usado: true, estadoSesion: SessionStatus.InProgress);

        var result = await _sut.ValidateTokenAsync("tok");

        result.IsValid.Should().BeTrue();
        result.SessionId.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task Validate_TokenConSesionTerminada_SeRechaza()
    {
        ConfigurarToken(usado: true, estadoSesion: SessionStatus.Completed);

        var result = await _sut.ValidateTokenAsync("tok");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Esta prueba ya ha sido completada.");
    }

    [Fact]
    public async Task Validate_TokenExpiradoSinSesion_SeRechaza()
    {
        ConfigurarToken(horasHastaExpirar: -1);

        var result = await _sut.ValidateTokenAsync("tok");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("El enlace ha expirado.");
    }

    [Fact]
    public async Task Validate_TokenExpiradoConSesionEnCurso_SigueSiendoValido()
    {
        // ExpiresAt es el plazo para empezar, no para terminar: una prueba abierta continúa.
        ConfigurarToken(usado: true, horasHastaExpirar: -1, estadoSesion: SessionStatus.InProgress);

        var result = await _sut.ValidateTokenAsync("tok");

        result.IsValid.Should().BeTrue();
        result.SessionId.Should().Be(42);
    }

    [Fact]
    public async Task Validate_SesionInProgressConResultado_SeRechaza()
    {
        // SubmitExamAsync escribe el resultado y el estado de la sesión en dos confirmaciones.
        // Si el proceso cae entre ambas, la prueba está enviada aunque la sesión siga marcada
        // InProgress. Darla por reanudable llevaría a un segundo resultado sobre la misma sesión.
        ConfigurarToken(usado: true, estadoSesion: SessionStatus.InProgress, conResultado: true);

        var result = await _sut.ValidateTokenAsync("tok");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Esta prueba ya ha sido completada.");
    }

    [Fact]
    public async Task Validate_AlumnoNuevo_SeAprovisionaSinContrasenaUtilizable()
    {
        // Derivarla del email convertía un dato que circula en cualquier proceso de
        // selección en la llave del portal del candidato.
        _userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), default))
            .ReturnsAsync(new List<User>());

        User? creado = null;
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, CancellationToken _) => { u.Id = 5; creado = u; return u; });

        ConfigurarToken();
        await _sut.ValidateTokenAsync("tok");

        creado.Should().NotBeNull();
        creado!.Email.Should().Be("ana@test.com");
        creado.Username.Should().BeNull();
        creado.PasswordHash.Should().BeEmpty();

        // Y esa cuenta no deja entrar ni con la parte local de su email ni con nada.
        PasswordHasher.Verify("ana", creado.PasswordHash).IsValid.Should().BeFalse();
        PasswordHasher.Verify("ana@test.com", creado.PasswordHash).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_DosCandidatosConLaMismaParteLocal_TienenCadaUnoSuCuenta()
    {
        // El nombre de usuario salía de la parte local del email, con índice único: la
        // invitación de maria.lopez@hotmail.com daba 500 si ya existía la de gmail.com.
        _userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), default))
            .ReturnsAsync(new List<User>());

        var creados = new List<User>();
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), default))
            .ReturnsAsync((User u, CancellationToken _) => { u.Id = 10 + creados.Count; creados.Add(u); return u; });

        foreach (var (clave, email) in new[] { ("gmail", "maria.lopez@gmail.com"), ("hotmail", "maria.lopez@hotmail.com") })
        {
            _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync(clave, default)).ReturnsAsync(new ExamToken
            {
                Id = 1, Token = clave, ExamId = 7, Exam = ExamenDe(60),
                CandidateName = "María López", CandidateEmail = email,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });

            (await _sut.ValidateTokenAsync(clave)).IsValid.Should().BeTrue();
        }

        creados.Select(u => u.Email).Should().Equal("maria.lopez@gmail.com", "maria.lopez@hotmail.com");
        creados.Should().OnlyContain(u => u.Username == null,
            "el índice único está filtrado a los no nulos, así que ninguna cuenta sin nombre choca con otra");
    }

    [Fact]
    public async Task Validate_AlumnoExistente_NoTocaSuContrasena()
    {
        _userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), default))
            .ReturnsAsync(new List<User>
            {
                new() { Id = 5, Email = "ana@test.com", Name = "Ana", PasswordHash = "hash-existente" }
            });

        ConfigurarToken();
        await _sut.ValidateTokenAsync("tok");

        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
    }

    [Fact]
    public async Task Validate_TokenInexistente_SeRechaza()
    {
        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("nada", default)).ReturnsAsync((ExamToken?)null);

        var result = await _sut.ValidateTokenAsync("nada");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("Token no válido.");
    }

    // ---------- Prueba desactivada ----------

    [Fact]
    public async Task Validate_PruebaDesactivadaSinSesion_RetiraElEnlace()
    {
        var token = ConfigurarToken();
        token.Exam.IsActive = false;

        var result = await _sut.ValidateTokenAsync("tok");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("ya no está disponible");
        result.AuthToken.Should().BeNull();
    }

    [Fact]
    public async Task Validate_PruebaDesactivadaConSesionEnCurso_DejaTerminar()
    {
        var token = ConfigurarToken(usado: true, estadoSesion: SessionStatus.InProgress);
        token.Exam.IsActive = false;

        var result = await _sut.ValidateTokenAsync("tok");

        result.IsValid.Should().BeTrue();
        result.SessionId.Should().Be(42);
    }

    [Fact]
    public async Task Start_PruebaDesactivadaSinSesion_NoCreaSesion()
    {
        var token = ConfigurarToken();
        token.Exam.IsActive = false;

        var session = await _sut.StartSessionAsync("tok");

        session.Should().BeNull();
        _sessionsCreadas.Should().BeEmpty();
        token.IsUsed.Should().BeFalse();
    }

    [Fact]
    public async Task Start_PruebaDesactivadaConSesionEnCurso_Reanuda()
    {
        var token = ConfigurarToken(usado: true, estadoSesion: SessionStatus.InProgress);
        token.Exam.IsActive = false;

        var session = await _sut.StartSessionAsync("tok");

        session!.SessionId.Should().Be(42);
    }

    // ---------- Inicio y reanudación ----------

    [Fact]
    public async Task Start_TokenNuevo_CreaLaSesionYLoMarcaUsado()
    {
        var token = ConfigurarToken();

        var session = await _sut.StartSessionAsync("tok");

        session.Should().NotBeNull();
        _sessionsCreadas.Should().HaveCount(1);
        token.IsUsed.Should().BeTrue();
        token.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Start_ConSesionEnCurso_ReanudaSinCrearOtra()
    {
        var inicio = DateTime.UtcNow.AddMinutes(-12);
        ConfigurarToken(usado: true, estadoSesion: SessionStatus.InProgress, inicioSesion: inicio);

        var primera = await _sut.StartSessionAsync("tok");
        var segunda = await _sut.StartSessionAsync("tok");

        primera!.SessionId.Should().Be(42);
        segunda!.SessionId.Should().Be(42);
        segunda.StartedAt.Should().Be(inicio);
        _sessionsCreadas.Should().BeEmpty();
        _answerRepo.Verify(r => r.AddAsync(It.IsAny<UserAnswer>(), default), Times.Never);
        _answerRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAnswer>(), default), Times.Never);
    }

    [Fact]
    public async Task Start_ConSesionTerminada_SeRechazaSinCrearSesion()
    {
        ConfigurarToken(usado: true, estadoSesion: SessionStatus.Completed);

        var session = await _sut.StartSessionAsync("tok");

        session.Should().BeNull();
        _sessionsCreadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_SesionInProgressConResultado_SeRechazaSinCrearSesion()
    {
        ConfigurarToken(usado: true, estadoSesion: SessionStatus.InProgress, conResultado: true);

        var session = await _sut.StartSessionAsync("tok");

        session.Should().BeNull();
        _sessionsCreadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_TokenExpiradoSinSesion_SeRechazaSinCrearSesion()
    {
        ConfigurarToken(horasHastaExpirar: -1);

        var session = await _sut.StartSessionAsync("tok");

        session.Should().BeNull();
        _sessionsCreadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_TokenInexistente_SeRechazaSinCrearSesion()
    {
        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("nada", default)).ReturnsAsync((ExamToken?)null);

        var session = await _sut.StartSessionAsync("nada");

        session.Should().BeNull();
        _sessionsCreadas.Should().BeEmpty();
    }

    // ---------- Tiempo restante ----------

    [Fact]
    public async Task TiempoRestante_SesionNueva_EsElTiempoLimiteCompleto()
    {
        ConfigurarToken(minutosExamen: 60);

        var session = await _sut.StartSessionAsync("tok");

        session!.RemainingSeconds.Should().BeInRange(60 * 60 - 5, 60 * 60);
    }

    [Fact]
    public async Task TiempoRestante_ReanudacionAMitad_DescuentaLoTranscurrido()
    {
        ConfigurarToken(
            usado: true,
            estadoSesion: SessionStatus.InProgress,
            inicioSesion: DateTime.UtcNow.AddMinutes(-12),
            minutosExamen: 60);

        var session = await _sut.StartSessionAsync("tok");

        session!.RemainingSeconds.Should().BeInRange(48 * 60 - 5, 48 * 60);
    }

    [Fact]
    public async Task TiempoRestante_TiempoAgotado_EsCeroYNuncaNegativo()
    {
        ConfigurarToken(
            usado: true,
            estadoSesion: SessionStatus.InProgress,
            inicioSesion: DateTime.UtcNow.AddMinutes(-90),
            minutosExamen: 60);

        var session = await _sut.StartSessionAsync("tok");

        session!.RemainingSeconds.Should().Be(0);
    }

    // ---------- Respuestas guardadas ----------

    [Fact]
    public async Task RespuestasGuardadas_SesionNueva_VienenVacias()
    {
        ConfigurarToken();

        var session = await _sut.StartSessionAsync("tok");

        session!.SavedAnswers.Should().BeEmpty();
    }

    [Fact]
    public async Task RespuestasGuardadas_Reanudacion_DevuelveLoYaRespondido()
    {
        ConfigurarToken(
            usado: true,
            estadoSesion: SessionStatus.InProgress,
            respuestas: new[]
            {
                new UserAnswer { Id = 1, QuestionId = 1, SelectedAnswerId = 11 },
                new UserAnswer { Id = 2, QuestionId = 2, OpenAnswer = "Mi respuesta" }
            });

        var session = await _sut.StartSessionAsync("tok");

        session!.SavedAnswers.Should().HaveCount(2);
        session.SavedAnswers.Should().ContainEquivalentOf(new SubmitAnswerDto(1, 11, null));
        session.SavedAnswers.Should().ContainEquivalentOf(new SubmitAnswerDto(2, null, "Mi respuesta"));
    }

    [Fact]
    public async Task RespuestasGuardadas_NoRevelanElSolucionario()
    {
        // SubmitAnswerDto no tiene sitio para IsCorrect ni para los puntos otorgados, y las
        // opciones viajan como AnswerOptionDto, que tampoco marca cuál es la correcta.
        ConfigurarToken(
            usado: true,
            estadoSesion: SessionStatus.InProgress,
            respuestas: new[]
            {
                new UserAnswer { Id = 1, QuestionId = 1, SelectedAnswerId = 11, IsCorrect = true, AwardedPoints = 5 }
            });

        var session = await _sut.StartSessionAsync("tok");

        var guardada = session!.SavedAnswers.Single();
        guardada.Should().BeEquivalentTo(new SubmitAnswerDto(1, 11, null));

        typeof(SubmitAnswerDto).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(new[] { "QuestionId", "SelectedAnswerId", "OpenAnswer" });

        typeof(AnswerOptionDto).GetProperties().Select(p => p.Name)
            .Should().NotContain("IsCorrect");
    }
}
