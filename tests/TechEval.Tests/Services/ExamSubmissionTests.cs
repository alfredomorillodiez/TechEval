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
/// Envío de la prueba: qué estado recibe el resultado y cómo se puntúa cada respuesta.
/// </summary>
public class ExamSubmissionTests
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

    private readonly List<UserAnswer> _saved = new();
    private ExamResult? _persisted;

    private readonly ExamTokenService _sut;

    public ExamSubmissionTests()
    {
        _sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamSession, bool>>>(), default))
            .ReturnsAsync(new List<ExamSession> { new() { Id = 1, ExamTokenId = 1 } });

        _tokenRepo.Setup(r => r.GetByIdAsync(1, default))
            .ReturnsAsync(new ExamToken { Id = 1, Token = "tok" });

        // No hay respuestas previas: todas se crean en el envío.
        _answerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), default))
            .ReturnsAsync(new List<UserAnswer>());
        _answerRepo.Setup(r => r.AddAsync(It.IsAny<UserAnswer>(), default))
            .ReturnsAsync((UserAnswer a, CancellationToken _) => { _saved.Add(a); return a; });

        // Sin resultado previo: estas pruebas cubren el primer envío. El reenvío tiene las
        // suyas en ExamResubmissionTests.
        _resultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), default))
            .ReturnsAsync(new List<ExamResult>());

        _resultRepo.Setup(r => r.AddAsync(It.IsAny<ExamResult>(), default))
            .ReturnsAsync((ExamResult r, CancellationToken _) => { _persisted = r; r.Id = 99; return r; });

        _sut = new ExamTokenService(
            _tokenRepo.Object, _examRepo.Object, _sessionRepo.Object, _answerRepo.Object,
            _resultRepo.Object, _userRepo.Object, _email.Object, _tokens.Object, _uow);
    }

    private void SetupExam(params Question[] questions)
    {
        var exam = new Exam
        {
            Id = 7,
            Title = "Prueba",
            PassingScorePercentage = 70,
            ExamQuestions = questions.Select((q, i) => new ExamQuestion
            {
                QuestionId = q.Id, Order = i + 1, Question = q
            }).ToList()
        };

        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", default))
            .ReturnsAsync(new ExamToken
            {
                Id = 1, Token = "tok", ExamId = 7, Exam = exam, UserId = 5,
                CandidateName = "Ana", CandidateEmail = "ana@test.com"
            });
        _examRepo.Setup(r => r.GetWithQuestionsAsync(7, default)).ReturnsAsync(exam);
    }

    private static Question TestQuestion(int id, int points, int correctAnswerId) => new()
    {
        Id = id, Points = points, Type = QuestionType.MultipleChoice,
        Answers = new List<Answer>
        {
            new() { Id = correctAnswerId, Text = "Correcta", IsCorrect = true },
            new() { Id = correctAnswerId + 100, Text = "Incorrecta", IsCorrect = false }
        }
    };

    private static Question OpenQuestion(int id, int points) => new()
    {
        Id = id, Points = points, Type = QuestionType.OpenEnded, Answers = new List<Answer>()
    };

    [Fact]
    public async Task Submit_ExamSinAbiertas_SeCierraComoReviewed()
    {
        SetupExam(TestQuestion(1, 5, 11), TestQuestion(2, 5, 22));

        var receipt = await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto>
        {
            new(1, 11, null),   // acierta
            new(2, 122, null)   // falla
        }), userId: 5);

        receipt.Status.Should().Be(ExamResultStatus.Reviewed);
        _persisted!.Status.Should().Be(ExamResultStatus.Reviewed);
        _persisted.ObtainedPoints.Should().Be(5);
        _persisted.ScorePercentage.Should().Be(50m);
        _persisted.Passed.Should().BeFalse();   // 50% < 70%

        _email.Verify(e => e.SendExamResultAsync(
            "ana@test.com", "Ana", "Prueba", 50m, false, default), Times.Once);
        _email.Verify(e => e.SendExamPendingReviewAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task Submit_ConAbiertaRespondida_QuedaPendienteYSinVeredicto()
    {
        SetupExam(TestQuestion(1, 5, 11), OpenQuestion(2, 5));

        var receipt = await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto>
        {
            new(1, 11, null),
            new(2, null, "Mi respuesta razonada")
        }), userId: 5);

        receipt.Status.Should().Be(ExamResultStatus.PendingReview);
        _persisted!.Passed.Should().BeNull();

        // El acuse no lleva cifras.
        receipt.ScorePercentage.Should().BeNull();
        receipt.ObtainedPoints.Should().BeNull();
        receipt.Passed.Should().BeNull();

        _saved.Single(a => a.QuestionId == 2).AwardedPoints.Should().BeNull();

        _email.Verify(e => e.SendExamPendingReviewAsync(
            "ana@test.com", "Ana", "Prueba", default), Times.Once);
        _email.Verify(e => e.SendExamResultAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<decimal>(), It.IsAny<bool>(), default), Times.Never);
    }

    [Fact]
    public async Task Submit_AbiertasTodasEnBlanco_SiguePendienteConCeroPreasignado()
    {
        SetupExam(TestQuestion(1, 5, 11), OpenQuestion(2, 5), OpenQuestion(3, 5));

        var receipt = await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto>
        {
            new(1, 11, null),
            new(2, null, ""),
            new(3, null, "   ")   // solo espacios cuenta como en blanco
        }), userId: 5);

        // La composición del examen manda: sigue requiriendo validación humana.
        receipt.Status.Should().Be(ExamResultStatus.PendingReview);
        _persisted!.Passed.Should().BeNull();

        _saved.Single(a => a.QuestionId == 2).AwardedPoints.Should().Be(0);
        _saved.Single(a => a.QuestionId == 3).AwardedPoints.Should().Be(0);
    }

    [Fact]
    public async Task Submit_PuntosDeTestSeCongelanEnAwardedPoints()
    {
        SetupExam(TestQuestion(1, 3, 11), TestQuestion(2, 2, 22));

        await _sut.SubmitExamAsync(new SubmitExamDto(1, new List<SubmitAnswerDto>
        {
            new(1, 11, null),   // acierta → 3
            new(2, 122, null)   // falla   → 0
        }), userId: 5);

        _saved.Single(a => a.QuestionId == 1).AwardedPoints.Should().Be(3);
        _saved.Single(a => a.QuestionId == 2).AwardedPoints.Should().Be(0);
        _persisted!.ObtainedPoints.Should().Be(_saved.Sum(a => a.AwardedPoints ?? 0));
    }

    [Fact]
    public async Task Submit_NoDevuelveElSolucionarioAlCandidato()
    {
        SetupExam(TestQuestion(1, 5, 11));

        var receipt = await _sut.SubmitExamAsync(
            new SubmitExamDto(1, new List<SubmitAnswerDto> { new(1, 11, null) }), userId: 5);

        // El acuse no expone respuestas correctas ni evaluación por pregunta:
        // el endpoint de envío es anónimo.
        receipt.GetType().GetProperties()
            .Select(p => p.Name)
            .Should().NotContain(new[] { "Answers", "CorrectAnswerText", "IsCorrect" });
    }
}
