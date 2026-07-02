using FluentAssertions;
using Moq;
using Xunit;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Tests.Services;

public class ExamServiceTests
{
    private readonly Mock<IExamRepository> _examRepoMock = new();
    private readonly Mock<IQuestionRepository> _questionRepoMock = new();
    private readonly ExamService _sut;

    public ExamServiceTests()
        => _sut = new ExamService(_examRepoMock.Object, _questionRepoMock.Object);

    [Fact]
    public async Task GenerateAsync_NotEnoughQuestions_ThrowsException()
    {
        _questionRepoMock
            .Setup(r => r.GetRandomAsync(10, null, null, default))
            .ReturnsAsync(new List<Question> { new() { Id = 1 } }); // solo 1, se piden 10

        var dto = new GenerateExamDto("Test", "", 60, 70, 10, null, null);

        var act = async () => await _sut.GenerateAsync(dto, createdByUserId: 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*suficientes preguntas*");
    }

    [Fact]
    public async Task CreateAsync_AllQuestionsExist_CreatesExam()
    {
        var questions = Enumerable.Range(1, 3).Select(i => new Question
        {
            Id = i, IsActive = true,
            Answers = new List<Answer>()
        }).ToList();

        _questionRepoMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Question, bool>>>(), default))
            .ReturnsAsync(questions);

        var createdExam = new Exam
        {
            Id = 10, Title = "Mi examen", TimeLimitMinutes = 30,
            ExamQuestions = questions.Select((q, i) => new ExamQuestion
            {
                QuestionId = q.Id, Order = i + 1,
                Question = new Question { Points = 2, Answers = new List<Answer>(), Type = QuestionType.MultipleChoice, Difficulty = DifficultyLevel.Basic }
            }).ToList()
        };

        _examRepoMock.Setup(r => r.AddAsync(It.IsAny<Exam>(), default))
            .ReturnsAsync((Exam e, CancellationToken _) => { e.Id = 10; return e; });
        _examRepoMock.Setup(r => r.GetWithQuestionsAsync(10, default)).ReturnsAsync(createdExam);

        var dto = new CreateExamDto("Mi examen", "", 30, 70, new List<int> { 1, 2, 3 });

        var result = await _sut.CreateAsync(dto, createdByUserId: 1);

        result.Should().NotBeNull();
        result.Id.Should().Be(10);
        result.QuestionCount.Should().Be(3);
    }
}
