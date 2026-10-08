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
        _examRepo.Setup(r => r.CountActiveAsync(default)).ReturnsAsync(1);
        _questionRepo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Question, bool>>>(), default))
            .ReturnsAsync(50);

        _sut = new ResultService(_resultRepo.Object, _examRepo.Object, _questionRepo.Object);
    }

    /// <summary>
    /// Prepara el doble a partir de una lista de resultados, derivando de ella las cifras
    /// que ahora agrega la base de datos.
    ///
    /// Se deriva a propósito, en vez de escribir los agregados a mano: escribirlos
    /// convertiría la prueba en una comprobación de mis propias cuentas contra mis propias
    /// cuentas. Derivándolos, la prueba sigue diciendo «dados estos resultados, el
    /// dashboard muestra esto», que es lo que afirmaba antes del cambio.
    /// </summary>
    private void ConResultados(params ExamResult[] resultados)
    {
        var corregidos = resultados.Where(r => r.Status == ExamResultStatus.Reviewed).ToList();

        _resultRepo
            .Setup(r => r.GetPeriodStatsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync(new PeriodResultStats(
                Total: resultados.Length,
                Scored: corregidos.Count,
                ScoreSum: corregidos.Sum(r => r.ScorePercentage),
                Passed: corregidos.Count(r => r.Passed == true)));

        _resultRepo.Setup(r => r.CountPendingReviewAsync(default))
            .ReturnsAsync(resultados.Count(r => r.Status == ExamResultStatus.PendingReview));

        _resultRepo.Setup(r => r.GetRecentAsync(It.IsAny<int>(), default))
            .ReturnsAsync((int n, CancellationToken _) =>
                resultados.OrderByDescending(r => r.CompletedAt).Take(n).ToList());
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
        ConResultados(
            Result(80m, true,  ExamResultStatus.Reviewed),
            Result(80m, true,  ExamResultStatus.Reviewed),
            Result(20m, null,  ExamResultStatus.PendingReview)   // puntuación parcial
        );

        var stats = await _sut.GetDashboardStatsAsync();

        stats.AverageScoreThisMonth.Should().Be(80m);   // no 60
        stats.PassRateThisMonth.Should().Be(100);       // no 66
        stats.PendingReviewCount.Should().Be(1);
        stats.TotalResultsThisMonth.Should().Be(3);     // el recuento sí los incluye
    }

    [Fact]
    public async Task Dashboard_SoloPendientes_NoFallaYDevuelveCeros()
    {
        ConResultados(Result(10m, null, ExamResultStatus.PendingReview));

        var stats = await _sut.GetDashboardStatsAsync();

        stats.AverageScoreThisMonth.Should().Be(0);
        stats.PassRateThisMonth.Should().Be(0);
        stats.PendingReviewCount.Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_SinResultados_NoFalla()
    {
        ConResultados();

        var stats = await _sut.GetDashboardStatsAsync();

        stats.AverageScoreThisMonth.Should().Be(0);
        stats.PassRateThisMonth.Should().Be(0);
        stats.PendingReviewCount.Should().Be(0);
    }

    [Fact]
    public async Task Dashboard_NoSeTraeElHistoricoNiElArbolDeExamenes()
    {
        // R2: el dashboard necesita seis cifras y diez filas. Antes pedía todos los
        // resultados con su examen, y todos los exámenes con sus preguntas, invitaciones,
        // sesiones y resultados, para acabar contando. El coste crecía con el histórico,
        // no con lo que la pantalla muestra.
        ConResultados(Result(80m, true, ExamResultStatus.Reviewed));

        await _sut.GetDashboardStatsAsync();

        _resultRepo.Verify(r => r.GetAllWithDetailsAsync(default), Times.Never,
            "el dashboard no debe materializar el histórico completo de resultados");
        _examRepo.Verify(r => r.GetWithStatsAsync(default), Times.Never,
            "contar exámenes activos no debe traer sus preguntas, invitaciones ni sesiones");
    }

    [Fact]
    public async Task Dashboard_PideElMesComoRangoDeFechas()
    {
        // Comparar año y mes contra el reloj dentro de la consulta no puede usar el índice
        // de CompletedAt. Un rango sí.
        DateTime? desde = null, hasta = null;
        _resultRepo
            .Setup(r => r.GetPeriodStatsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .Callback((DateTime d, DateTime h, CancellationToken _) => { desde = d; hasta = h; })
            .ReturnsAsync(new PeriodResultStats(0, 0, 0m, 0));
        _resultRepo.Setup(r => r.CountPendingReviewAsync(default)).ReturnsAsync(0);
        _resultRepo.Setup(r => r.GetRecentAsync(It.IsAny<int>(), default))
            .ReturnsAsync(new List<ExamResult>());

        await _sut.GetDashboardStatsAsync();

        desde.Should().NotBeNull();
        desde!.Value.Day.Should().Be(1, "el rango empieza el primer día del mes");
        desde.Value.TimeOfDay.Should().Be(TimeSpan.Zero);
        hasta.Should().Be(desde.Value.AddMonths(1), "y termina al empezar el siguiente");
    }
}
