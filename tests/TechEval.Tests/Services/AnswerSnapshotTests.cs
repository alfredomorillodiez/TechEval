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
/// La respuesta guarda lo que se le preguntó al candidato. Antes la ficha del resultado
/// leía el enunciado, las opciones y los puntos de la pregunta actual del banco, así que
/// corregir una errata cambiaba hacia atrás lo que constaba en cada examen ya cerrado.
/// </summary>
public class AnswerSnapshotTests
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
    private readonly ExamTokenService _sut;

    public AnswerSnapshotTests()
    {
        _sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamSession, bool>>>(), default))
            .ReturnsAsync(new List<ExamSession>
            {
                new() { Id = 1, ExamTokenId = 1, StartedAt = DateTime.UtcNow.AddMinutes(-10) }
            });

        _tokenRepo.Setup(r => r.GetByIdAsync(1, default))
            .ReturnsAsync(new ExamToken { Id = 1, Token = "tok" });

        _answerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserAnswer, bool>>>(), default))
            .ReturnsAsync(new List<UserAnswer>());
        _answerRepo.Setup(r => r.AddAsync(It.IsAny<UserAnswer>(), default))
            .ReturnsAsync((UserAnswer a, CancellationToken _) => { _saved.Add(a); return a; });

        _resultRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExamResult, bool>>>(), default))
            .ReturnsAsync(new List<ExamResult>());
        _resultRepo.Setup(r => r.AddAsync(It.IsAny<ExamResult>(), default))
            .ReturnsAsync((ExamResult r, CancellationToken _) => { r.Id = 99; return r; });

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

    [Fact]
    public async Task Submit_PreguntaDeTest_GuardaEnunciadoOpcionesYPuntos()
    {
        SetupExam(new Question
        {
            Id = 1, Points = 10, Text = "¿Qué es una transacción?",
            Type = QuestionType.MultipleChoice,
            Answers = new List<Answer>
            {
                new() { Id = 11, Text = "Una unidad atómica de trabajo", IsCorrect = true },
                new() { Id = 12, Text = "Un tipo de índice", IsCorrect = false }
            }
        });

        await _sut.SubmitExamAsync(
            new SubmitExamDto(1, new List<SubmitAnswerDto> { new(1, 12, null) }), userId: 5);

        var guardada = _saved.Single();
        guardada.QuestionTextSnapshot.Should().Be("¿Qué es una transacción?");
        guardada.SelectedAnswerTextSnapshot.Should().Be("Un tipo de índice",
            "es la opción que eligió, no la que era correcta");
        guardada.CorrectAnswerTextSnapshot.Should().Be("Una unidad atómica de trabajo");
        guardada.QuestionPointsSnapshot.Should().Be(10);
    }

    [Fact]
    public async Task Submit_PreguntaAbierta_GuardaEnunciadoYPuntosSinOpciones()
    {
        SetupExam(new Question
        {
            Id = 2, Points = 20, Text = "Explica el aislamiento de transacciones.",
            Type = QuestionType.OpenEnded, Answers = new List<Answer>()
        });

        await _sut.SubmitExamAsync(
            new SubmitExamDto(1, new List<SubmitAnswerDto> { new(2, null, "Mi respuesta") }),
            userId: 5);

        var guardada = _saved.Single();
        guardada.QuestionTextSnapshot.Should().Be("Explica el aislamiento de transacciones.");
        guardada.QuestionPointsSnapshot.Should().Be(20);
        guardada.SelectedAnswerTextSnapshot.Should().BeNull("una abierta no tiene opciones");
        guardada.CorrectAnswerTextSnapshot.Should().BeNull();
    }

    [Fact]
    public async Task Submit_LaHoraDeLaRespuestaNoVaPorDelanteDeLaSesion()
    {
        // D5: AnsweredAt usaba DateTime.Now y StartedAt DateTime.UtcNow. En un servidor
        // con horario español la respuesta quedaba una o dos horas por delante de su
        // propia sesión, y en el peor caso posterior al cierre.
        var iniciada = DateTime.UtcNow.AddMinutes(-10);
        SetupExam(new Question
        {
            Id = 1, Points = 5, Text = "Pregunta", Type = QuestionType.MultipleChoice,
            Answers = new List<Answer> { new() { Id = 11, Text = "Sí", IsCorrect = true } }
        });

        await _sut.SubmitExamAsync(
            new SubmitExamDto(1, new List<SubmitAnswerDto> { new(1, 11, null) }), userId: 5);

        var guardada = _saved.Single();
        guardada.AnsweredAt.Should().BeAfter(iniciada);
        guardada.AnsweredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1),
            "la marca está en UTC, igual que el resto del modelo");
    }

    [Fact]
    public void UserAnswerNueva_NaceConLaHoraEnUtc()
    {
        var recien = new UserAnswer();

        recien.AnsweredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
