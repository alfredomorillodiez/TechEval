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
/// Las métricas del dashboard se calculan solo sobre resultados corregidos: los
/// pendientes llevan una puntuación parcial que hundiría la media.
/// </summary>
public class ResultServiceDashboardTests
{
    private readonly Mock<IExamResultRepository> _resultRepo = new();
    private readonly Mock<IExamRepository> _examRepo = new();
    private readonly Mock<IQuestionRepository> _questionRepo = new();
    private readonly ResultService _sut;

    public ResultServiceDashboardTests()
    {
        _examRepo.Setup(r => r.GetWithStatsAsync(default))
            .ReturnsAsync(new List<Exam> { new() { Id = 1, IsActive = true } });
        _questionRepo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Question, bool>>>(), default))
            .ReturnsAsync(50);

        _sut = new ResultService(_resultRepo.Object, _examRepo.Object, _questionRepo.Object);
    }

    private static ExamResult Result(decimal score, bool? passed, ExamResultStatus status) => new()
    {
        Id = 1,
        ExamId = 1,
        Exam = new Exam { Id = 1, Title = "Prueba" },
        ScorePercentage = score,
        Passed = passed,
        Status = status,
        CompletedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Dashboard_LosPendientesNoDistorsionanLasMetricas()
    {
        _resultRepo.Setup(r => r.GetAllWithDetailsAsync(default)).ReturnsAsync(new List<ExamResult>
        {
            Result(80m, true,  ExamResultStatus.Reviewed),
            Result(80m, true,  ExamResultStatus.Reviewed),
            Result(20m, null,  ExamResultStatus.PendingReview)   // puntuación parcial
        });

        var stats = await _sut.GetDashboardStatsAsync();

        stats.AverageScoreThisMonth.Should().Be(80m);   // no 60
        stats.PassRateThisMonth.Should().Be(100);       // no 66
        stats.PendingReviewCount.Should().Be(1);
        stats.TotalResultsThisMonth.Should().Be(3);     // el recuento sí los incluye
    }

    [Fact]
    public async Task Dashboard_SoloPendientes_NoFallaYDevuelveCeros()
    {
        _resultRepo.Setup(r => r.GetAllWithDetailsAsync(default)).ReturnsAsync(new List<ExamResult>
        {
            Result(10m, null, ExamResultStatus.PendingReview)
        });

        var stats = await _sut.GetDashboardStatsAsync();

        stats.AverageScoreThisMonth.Should().Be(0);
        stats.PassRateThisMonth.Should().Be(0);
        stats.PendingReviewCount.Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_SinResultados_NoFalla()
    {
        _resultRepo.Setup(r => r.GetAllWithDetailsAsync(default)).ReturnsAsync(new List<ExamResult>());

        var stats = await _sut.GetDashboardStatsAsync();

        stats.AverageScoreThisMonth.Should().Be(0);
        stats.PassRateThisMonth.Should().Be(0);
        stats.PendingReviewCount.Should().Be(0);
    }
}
