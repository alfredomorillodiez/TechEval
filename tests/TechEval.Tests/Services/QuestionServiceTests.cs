using FluentAssertions;
using Moq;
using Xunit;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Tests.Services;

public class QuestionServiceTests
{
    private readonly Mock<IQuestionRepository> _repoMock = new();
    private readonly QuestionService _sut;

    public QuestionServiceTests() => _sut = new QuestionService(_repoMock.Object);

    [Fact]
    public async Task CreateAsync_ValidMultipleChoice_ReturnsDto()
    {
        var dto = new CreateQuestionDto(
            "¿Qué es SOLID?",
            QuestionType.MultipleChoice,
            DifficultyLevel.Intermediate,
            CategoryId: 1,
            Points: 2,
            SampleAnswer: null,
            Answers: new List<CreateAnswerDto>
            {
                new("Opción A", false, 1),
                new("Opción B (correcta)", true, 2),
                new("Opción C", false, 3),
                new("Opción D", false, 4)
            });

        _repoMock.Setup(r => r.AddAsync(It.IsAny<Question>(), default))
            .ReturnsAsync((Question q, CancellationToken _) =>
            {
                q.Id = 42;
                q.Category = new Category { Name = "Arquitectura" };
                return q;
            });

        var result = await _sut.CreateAsync(dto);

        result.Should().NotBeNull();
        result.Id.Should().Be(42);
        result.Answers.Should().HaveCount(4);
        result.Answers.Count(a => a.IsCorrect).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_MultipleChoiceWithoutCorrectAnswer_ThrowsException()
    {
        var dto = new CreateQuestionDto(
            "Pregunta inválida",
            QuestionType.MultipleChoice,
            DifficultyLevel.Basic,
            1, 1, null,
            new List<CreateAnswerDto>
            {
                new("A", false, 1), new("B", false, 2),
                new("C", false, 3), new("D", false, 4)
            });

        var act = async () => await _sut.CreateAsync(dto);

        await act.Should().ThrowAsync<TechEval.Application.ValidationException>()
            .WithMessage("*exactamente 1 respuesta correcta*");
    }

    [Fact]
    public async Task CreateAsync_MultipleChoiceWithWrongCount_ThrowsException()
    {
        var dto = new CreateQuestionDto(
            "Sólo 2 opciones",
            QuestionType.MultipleChoice,
            DifficultyLevel.Basic,
            1, 1, null,
            new List<CreateAnswerDto>
            {
                new("A", true, 1), new("B", false, 2)
            });

        var act = async () => await _sut.CreateAsync(dto);

        await act.Should().ThrowAsync<TechEval.Application.ValidationException>()
            .WithMessage("*exactamente 4 respuestas*");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingQuestion_ReturnsDto()
    {
        var question = new Question
        {
            Id = 5, Text = "Test?", Type = QuestionType.OpenEnded,
            Difficulty = DifficultyLevel.Advanced, CategoryId = 2, Points = 3,
            Category = new Category { Name = "SQL" },
            Answers = new List<Answer>()
        };

        _repoMock.Setup(r => r.GetWithAnswersAsync(5, default)).ReturnsAsync(question);

        var result = await _sut.GetByIdAsync(5);

        result.Should().NotBeNull();
        result!.Text.Should().Be("Test?");
        result.CategoryName.Should().Be("SQL");
    }

    [Fact]
    public async Task GetByIdAsync_NonExisting_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetWithAnswersAsync(999, default)).ReturnsAsync((Question?)null);

        var result = await _sut.GetByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ExistingQuestion_SetsInactive()
    {
        var question = new Question { Id = 1, IsActive = true };
        _repoMock.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(question);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Question>(), default)).Returns(Task.CompletedTask);

        var result = await _sut.DeleteAsync(1);

        result.Should().BeTrue();
        question.IsActive.Should().BeFalse();
        _repoMock.Verify(r => r.UpdateAsync(question, default), Times.Once);
    }
}
