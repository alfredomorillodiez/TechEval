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
/// Orden propio de cada sesión: estable para la misma sesión, distinto entre sesiones, y
/// sin efecto sobre la corrección ni sobre el orden de los resultados.
/// </summary>
public class SessionOrderTests
{
    private readonly Mock<IExamTokenRepository> _tokenRepo = new();
    private readonly Mock<IRepository<ExamSession>> _sessionRepo = new();
    private readonly ExamTokenService _sut;

    public SessionOrderTests()
    {
        _sessionRepo.Setup(r => r.AddAsync(It.IsAny<ExamSession>(), default))
            .ReturnsAsync((ExamSession s, CancellationToken _) => { s.Id = 50; return s; });

        _sut = new ExamTokenService(
            _tokenRepo.Object, new Mock<IExamRepository>().Object, _sessionRepo.Object,
            new Mock<IRepository<UserAnswer>>().Object, new Mock<IExamResultRepository>().Object,
            new Mock<IRepository<User>>().Object, new Mock<IEmailService>().Object,
            new Mock<ITokenService>().Object, new FakeUnitOfWork());
    }

    /// <summary>Examen de diez preguntas de test, cada una con cuatro opciones.</summary>
    private static Exam ExamenDeDiez() => new()
    {
        Id = 7,
        Title = "Prueba",
        TimeLimitMinutes = 60,
        ExamQuestions = Enumerable.Range(1, 10).Select(i => new ExamQuestion
        {
            QuestionId = i,
            Order = i,
            Question = new Question
            {
                Id = i, Text = $"Pregunta {i}", Type = QuestionType.MultipleChoice, Points = 1,
                Answers = Enumerable.Range(1, 4).Select(j => new Answer
                {
                    Id = i * 10 + j, Text = $"Opción {j}", Order = j, IsCorrect = j == 3
                }).ToList()
            }
        }).ToList()
    };

    private static List<int> OrdenDePreguntas(Exam exam, int? seed)
        => SessionOrder.Questions(exam.ExamQuestions, seed).Select(eq => eq.QuestionId).ToList();

    private static List<int> OrdenDeOpciones(Exam exam, int questionId, int? seed)
        => SessionOrder.Answers(
                exam.ExamQuestions.Single(eq => eq.QuestionId == questionId).Question.Answers,
                questionId, seed)
            .Select(a => a.Id).ToList();

    // ---------- La función de orden ----------

    [Fact]
    public void MismaSemilla_DaElMismoOrden()
    {
        var exam = ExamenDeDiez();

        OrdenDePreguntas(exam, 1234).Should().Equal(OrdenDePreguntas(exam, 1234));
        OrdenDeOpciones(exam, 5, 1234).Should().Equal(OrdenDeOpciones(exam, 5, 1234));
    }

    [Fact]
    public void SemillasDistintas_DanOrdenesDistintos()
    {
        var exam = ExamenDeDiez();

        OrdenDePreguntas(exam, 1).Should().NotEqual(OrdenDePreguntas(exam, 2));
    }

    [Fact]
    public void ConSemilla_ElOrdenEsUnaPermutacionDelExamen()
    {
        var exam = ExamenDeDiez();

        var orden = OrdenDePreguntas(exam, 99);

        orden.Should().BeEquivalentTo(Enumerable.Range(1, 10));
        orden.Should().NotEqual(Enumerable.Range(1, 10), "con diez preguntas, la permutación identidad sería casualidad");
    }

    [Fact]
    public void SinSemilla_SeConservaElOrdenDelExamen()
    {
        var exam = ExamenDeDiez();
        // El orden del examen no coincide con el de los identificadores a propósito.
        exam.ExamQuestions.First(eq => eq.QuestionId == 1).Order = 11;

        OrdenDePreguntas(exam, null).Should().Equal(2, 3, 4, 5, 6, 7, 8, 9, 10, 1);
        OrdenDeOpciones(exam, 4, null).Should().Equal(41, 42, 43, 44);
    }

    // ---------- La sesión ----------

    private ExamToken ConfigurarToken(ExamSession? sesion = null)
    {
        var token = new ExamToken
        {
            Id = 1, Token = "tok", ExamId = 7, Exam = ExamenDeDiez(),
            CandidateName = "Ana", CandidateEmail = "ana@test.com",
            IsUsed = sesion is not null, ExpiresAt = DateTime.UtcNow.AddHours(24),
            ExamSession = sesion
        };
        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", default)).ReturnsAsync(token);
        return token;
    }

    [Fact]
    public async Task Inicio_CreaLaSesionConSemilla()
    {
        ConfigurarToken();

        await _sut.StartSessionAsync("tok");

        _sessionRepo.Verify(r => r.AddAsync(
            It.Is<ExamSession>(s => s.ShuffleSeed != null), default), Times.Once);
    }

    [Fact]
    public async Task Reanudacion_DevuelveElMismoOrdenYNoCambiaLaSemilla()
    {
        var sesion = new ExamSession
        {
            Id = 42, ExamTokenId = 1, Status = SessionStatus.InProgress,
            StartedAt = DateTime.UtcNow, ShuffleSeed = 777
        };
        ConfigurarToken(sesion);

        var primera = await _sut.StartSessionAsync("tok");
        var segunda = await _sut.StartSessionAsync("tok");

        sesion.ShuffleSeed.Should().Be(777);
        segunda!.Questions.Select(q => q.QuestionId)
            .Should().Equal(primera!.Questions.Select(q => q.QuestionId));
        segunda.Questions.Select(q => q.Answers.Select(a => a.Id).ToList())
            .Should().BeEquivalentTo(primera.Questions.Select(q => q.Answers.Select(a => a.Id).ToList()),
                o => o.WithStrictOrdering());
        primera.Questions.Select(q => q.QuestionId)
            .Should().Equal(OrdenDePreguntas(ExamenDeDiez(), 777));
    }

    [Fact]
    public async Task Detalle_NumeraLasPosicionesDeLaSesion()
    {
        ConfigurarToken(new ExamSession
        {
            Id = 42, ExamTokenId = 1, Status = SessionStatus.InProgress,
            StartedAt = DateTime.UtcNow, ShuffleSeed = 777
        });

        var info = await _sut.StartSessionAsync("tok");

        info!.Questions.Select(q => q.Order).Should().Equal(Enumerable.Range(1, 10));
        info.Questions.Should().OnlyContain(q => q.Answers.Select(a => a.Order).SequenceEqual(new[] { 1, 2, 3, 4 }));
        info.CandidateEmail.Should().Be("ana@test.com");
    }

    [Fact]
    public async Task SesionAnteriorAlCambio_ConservaElOrdenDelExamen()
    {
        ConfigurarToken(new ExamSession
        {
            Id = 42, ExamTokenId = 1, Status = SessionStatus.InProgress,
            StartedAt = DateTime.UtcNow, ShuffleSeed = null
        });

        var info = await _sut.StartSessionAsync("tok");

        info!.Questions.Select(q => q.QuestionId).Should().Equal(Enumerable.Range(1, 10));
        info.Questions[0].Answers.Select(a => a.Id).Should().Equal(11, 12, 13, 14);
    }

    // ---------- Resultados ----------

    [Fact]
    public void Resultados_SeOrdenanPorElExamenYNoPorElOrdenDeGuardado()
    {
        var exam = ExamenDeDiez();
        // El candidato guardó primero la pregunta 3, luego la 1 y luego la 2.
        var respuestas = new List<UserAnswer>
        {
            new() { Id = 100, QuestionId = 3 },
            new() { Id = 101, QuestionId = 1 },
            new() { Id = 102, QuestionId = 2 }
        };

        SessionOrder.ByExamOrder(respuestas, exam).Select(ua => ua.QuestionId)
            .Should().Equal(1, 2, 3);
    }
}

/// <summary>
/// La corrección identifica la opción por su identificador: la posición en que la vio el
/// candidato no interviene.
/// </summary>
public class SessionOrderGradingTests
{
    [Fact]
    public async Task OpcionCorrectaEnOtraPosicion_SeCorrigeComoCorrecta()
    {
        var question = new Question
        {
            Id = 1, Points = 5, Type = QuestionType.MultipleChoice,
            Answers = new List<Answer>
            {
                new() { Id = 11, Text = "A", Order = 1 },
                new() { Id = 12, Text = "B", Order = 2 },
                new() { Id = 13, Text = "C", Order = 3, IsCorrect = true }
            }
        };
        var exam = new Exam
        {
            Id = 7, Title = "Prueba", PassingScorePercentage = 70, TimeLimitMinutes = 60,
            ExamQuestions = new List<ExamQuestion> { new() { QuestionId = 1, Order = 1, Question = question } }
        };

        // Una semilla con la que la correcta, tercera en el banco, no queda tercera en la sesión.
        var seed = Enumerable.Range(1, 1000).First(s =>
            SessionOrder.Answers(question.Answers, 1, s).Select(a => a.Id).ToList().IndexOf(13) != 2);
        var posicionEnSesion = SessionOrder.Answers(question.Answers, 1, seed).Select(a => a.Id).ToList().IndexOf(13);
        posicionEnSesion.Should().NotBe(2);

        var tokenRepo = new Mock<IExamTokenRepository>();
        var sessionRepo = new Mock<IRepository<ExamSession>>();
        var answerRepo = new Mock<IRepository<UserAnswer>>();
        var resultRepo = new Mock<IExamResultRepository>();
        var examRepo = new Mock<IExamRepository>();
        var saved = new List<UserAnswer>();
        ExamResult? persisted = null;

        sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamSession, bool>>>(), default))
            .ReturnsAsync(new List<ExamSession> { new() { Id = 1, ExamTokenId = 1, ShuffleSeed = seed } });
        tokenRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new ExamToken { Id = 1, Token = "tok" });
        tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", default)).ReturnsAsync(new ExamToken
        {
            Id = 1, Token = "tok", ExamId = 7, Exam = exam, UserId = 5,
            CandidateName = "Ana", CandidateEmail = "ana@test.com"
        });
        examRepo.Setup(r => r.GetWithQuestionsAsync(7, default)).ReturnsAsync(exam);
        answerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), default))
            .ReturnsAsync(new List<UserAnswer>());
        answerRepo.Setup(r => r.AddAsync(It.IsAny<UserAnswer>(), default))
            .ReturnsAsync((UserAnswer a, CancellationToken _) => { saved.Add(a); return a; });
        resultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), default))
            .ReturnsAsync(new List<ExamResult>());
        resultRepo.Setup(r => r.AddAsync(It.IsAny<ExamResult>(), default))
            .ReturnsAsync((ExamResult r, CancellationToken _) => { persisted = r; r.Id = 99; return r; });

        var sut = new ExamTokenService(
            tokenRepo.Object, examRepo.Object, sessionRepo.Object, answerRepo.Object,
            resultRepo.Object, new Mock<IRepository<User>>().Object, new Mock<IEmailService>().Object,
            new Mock<ITokenService>().Object, new FakeUnitOfWork());

        await sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto> { new(1, 13, null) }), userId: 5);

        saved.Single().IsCorrect.Should().BeTrue();
        persisted!.ObtainedPoints.Should().Be(5);
    }
}
