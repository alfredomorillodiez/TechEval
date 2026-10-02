using FluentAssertions;
using Moq;
using Xunit;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// La ficha del resultado muestra las cuatro opciones de cada pregunta de test, con la
/// marcada y la correcta señaladas. Si la pregunta se editó después del examen, solo las
/// copias del envío.
/// </summary>
public class ResultDetailOptionsTests
{
    private readonly Mock<IExamResultRepository> _resultRepo = new();
    private readonly ResultService _sut;

    public ResultDetailOptionsTests()
    {
        _sut = new ResultService(
            _resultRepo.Object, new Mock<IExamRepository>().Object, new Mock<IQuestionRepository>().Object);
    }

    private static Question Test() => new()
    {
        Id = 10,
        Text = "¿Qué devuelve 2 + 2?",
        Type = QuestionType.MultipleChoice,
        Points = 1,
        // A propósito fuera de orden: la ficha tiene que ordenarlas por Order.
        Answers =
        [
            new Answer { Id = 103, Text = "5", Order = 3 },
            new Answer { Id = 101, Text = "3", Order = 1 },
            new Answer { Id = 102, Text = "4", Order = 2, IsCorrect = true },
            new Answer { Id = 104, Text = "22", Order = 4 }
        ]
    };

    private void SetupResult(Question question, UserAnswer answer)
    {
        answer.QuestionId = question.Id;
        answer.Question = question;
        answer.SelectedAnswer = question.Answers.FirstOrDefault(a => a.Id == answer.SelectedAnswerId);

        _resultRepo.Setup(r => r.GetWithDetailsAsync(1, default)).ReturnsAsync(new ExamResult
        {
            Id = 1,
            Exam = new Exam { ExamQuestions = [new ExamQuestion { QuestionId = question.Id, Order = 1 }] },
            ExamSession = new ExamSession { UserAnswers = [answer] }
        });
    }

    [Fact]
    public async Task Muestra_las_cuatro_opciones_en_orden_con_la_marcada_y_la_correcta()
    {
        var question = Test();
        SetupResult(question, new UserAnswer
        {
            Id = 1, SelectedAnswerId = 103, IsCorrect = false,
            QuestionTextSnapshot = question.Text, SelectedAnswerTextSnapshot = "5", CorrectAnswerTextSnapshot = "4"
        });

        var detail = await _sut.GetDetailAsync(1);

        var options = detail!.Answers.Single().Options;
        options.Should().NotBeNull();
        options!.Select(o => o.Text).Should().Equal("3", "4", "5", "22");
        options.Single(o => o.IsSelected).Text.Should().Be("5");
        options.Single(o => o.IsCorrect).Text.Should().Be("4");
    }

    [Fact]
    public async Task Sin_respuesta_muestra_las_opciones_sin_ninguna_marcada()
    {
        var question = Test();
        SetupResult(question, new UserAnswer { Id = 1, IsCorrect = false, CorrectAnswerTextSnapshot = "4" });

        var options = (await _sut.GetDetailAsync(1))!.Answers.Single().Options;

        options.Should().HaveCount(4);
        options!.Should().NotContain(o => o.IsSelected);
    }

    [Fact]
    public async Task Respuesta_anterior_a_las_copias_tambien_muestra_las_opciones()
    {
        var question = Test();
        SetupResult(question, new UserAnswer { Id = 1, SelectedAnswerId = 102, IsCorrect = true });

        var options = (await _sut.GetDetailAsync(1))!.Answers.Single().Options;

        options.Should().HaveCount(4);
    }

    [Theory]
    [InlineData("enunciado")]
    [InlineData("marcada")]
    [InlineData("correcta")]
    public async Task Si_la_pregunta_se_edito_despues_no_muestra_las_opciones_de_hoy(string queCambio)
    {
        var question = Test();
        var answer = new UserAnswer
        {
            Id = 1, SelectedAnswerId = 103, IsCorrect = false,
            QuestionTextSnapshot = question.Text, SelectedAnswerTextSnapshot = "5", CorrectAnswerTextSnapshot = "4"
        };
        switch (queCambio)
        {
            case "enunciado": question.Text = "¿Cuánto es 2 + 2?"; break;
            case "marcada": question.Answers.Single(a => a.Id == 103).Text = "6"; break;
            case "correcta":
                question.Answers.Single(a => a.Id == 102).IsCorrect = false;
                question.Answers.Single(a => a.Id == 104).IsCorrect = true;
                break;
        }
        SetupResult(question, answer);

        var review = (await _sut.GetDetailAsync(1))!.Answers.Single();

        review.Options.Should().BeNull();
        review.SelectedAnswerText.Should().Be("5");
        review.CorrectAnswerText.Should().Be("4");
    }

    [Fact]
    public async Task Las_preguntas_abiertas_no_tienen_opciones()
    {
        var question = new Question { Id = 20, Text = "Explica SOLID", Type = QuestionType.OpenEnded, Points = 5 };
        SetupResult(question, new UserAnswer { Id = 1, OpenAnswer = "..." });

        (await _sut.GetDetailAsync(1))!.Answers.Single().Options.Should().BeNull();
    }
}
