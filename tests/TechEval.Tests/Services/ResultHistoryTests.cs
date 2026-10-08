using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Xunit;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// D7: la ficha de un resultado no cambia cuando alguien edita la pregunta.
///
/// Cada prueba monta una respuesta cuya copia dice una cosa y cuya pregunta del banco
/// dice otra. Es lo que ocurre en cuanto un administrador corrige una errata: el arreglo
/// de D3 hizo esa edición posible, y con ella el efecto retroactivo.
/// </summary>
public class ResultHistoryTests
{
    private readonly Mock<IExamResultRepository> _resultRepo = new();
    private readonly Mock<IExamRepository> _examRepo = new();
    private readonly Mock<IQuestionRepository> _questionRepo = new();
    private readonly ResultService _sut;

    public ResultHistoryTests()
    {
        _questionRepo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Question, bool>>>(), default))
            .ReturnsAsync(0);
        _sut = new ResultService(_resultRepo.Object, _examRepo.Object, _questionRepo.Object);
    }

    /// <summary>La pregunta tal como está HOY en el banco, ya editada.</summary>
    private static Question PreguntaEditada() => new()
    {
        Id = 1,
        Text = "Enunciado corregido después del examen",
        Points = 5,
        Type = QuestionType.MultipleChoice,
        Answers = new List<Answer>
        {
            new() { Id = 11, Text = "Opción A reescrita", IsCorrect = false },
            new() { Id = 12, Text = "Opción B reescrita", IsCorrect = true }
        }
    };

    private void SetupResult(UserAnswer answer)
    {
        _resultRepo.Setup(r => r.GetWithDetailsAsync(1, default)).ReturnsAsync(new ExamResult
        {
            Id = 1,
            CandidateName = "Ana",
            CandidateEmail = "ana@test.com",
            Exam = new Exam { Id = 7, Title = "Prueba" },
            TotalPoints = 10,
            ObtainedPoints = 10,
            Status = ExamResultStatus.Reviewed,
            ExamSession = new ExamSession { Id = 1, UserAnswers = new List<UserAnswer> { answer } }
        });
    }

    [Fact]
    public async Task Detalle_MuestraLoQueSeLePregunto_NoLaPreguntaEditada()
    {
        var pregunta = PreguntaEditada();
        SetupResult(new UserAnswer
        {
            Id = 1, QuestionId = 1, Question = pregunta,
            SelectedAnswerId = 11, SelectedAnswer = pregunta.Answers.First(a => a.Id == 11),
            IsCorrect = true, AwardedPoints = 10,
            QuestionTextSnapshot = "Enunciado original que vio el candidato",
            SelectedAnswerTextSnapshot = "Opción A original",
            CorrectAnswerTextSnapshot = "Opción A original",
            QuestionPointsSnapshot = 10
        });

        var detalle = await _sut.GetDetailAsync(1);

        var revision = detalle!.Answers.Single();
        revision.QuestionText.Should().Be("Enunciado original que vio el candidato");
        revision.SelectedAnswerText.Should().Be("Opción A original");
        revision.CorrectAnswerText.Should().Be("Opción A original",
            "marcar correcta otra opción no puede cambiar lo que consta que lo era");
        revision.Points.Should().Be(10,
            "bajar la pregunta a 5 puntos dejaría 10 otorgados sobre un máximo de 5");
    }

    [Fact]
    public async Task Detalle_DeUnaRespuestaAnteriorALaCopia_CaeEnLaPreguntaActual()
    {
        // Las filas anteriores al cambio no tienen copia hasta que el guion de esquema las
        // rellena. Mientras tanto deben seguir mostrando algo, no una ficha en blanco.
        var pregunta = PreguntaEditada();
        SetupResult(new UserAnswer
        {
            Id = 1, QuestionId = 1, Question = pregunta,
            SelectedAnswerId = 11, SelectedAnswer = pregunta.Answers.First(a => a.Id == 11),
            IsCorrect = false, AwardedPoints = 0
        });

        var detalle = await _sut.GetDetailAsync(1);

        var revision = detalle!.Answers.Single();
        revision.QuestionText.Should().Be("Enunciado corregido después del examen");
        revision.SelectedAnswerText.Should().Be("Opción A reescrita");
        revision.Points.Should().Be(5);
    }

    [Fact]
    public async Task Detalle_DeUnaAbierta_ConservaEnunciadoYMaximo()
    {
        SetupResult(new UserAnswer
        {
            Id = 1, QuestionId = 2,
            Question = new Question
            {
                Id = 2, Text = "Enunciado reescrito", Points = 3,
                Type = QuestionType.OpenEnded, Answers = new List<Answer>()
            },
            OpenAnswer = "Lo que escribió el candidato",
            AwardedPoints = 15,
            QuestionTextSnapshot = "Enunciado tal como se formuló",
            QuestionPointsSnapshot = 20
        });

        var detalle = await _sut.GetDetailAsync(1);

        var revision = detalle!.Answers.Single();
        revision.QuestionText.Should().Be("Enunciado tal como se formuló");
        revision.OpenAnswer.Should().Be("Lo que escribió el candidato");
        revision.Points.Should().Be(20);
        revision.AwardedPoints.Should().Be(15);
    }
}
