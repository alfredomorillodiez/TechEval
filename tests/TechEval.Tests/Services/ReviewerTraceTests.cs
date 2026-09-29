using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// Spec exam-results — el detalle muestra quién corrigió; y spec admin-console — aviso de
/// pruebas con preguntas abiertas y sin evaluador.
/// </summary>
public class ReviewerTraceTests
{
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"traza-{Guid.NewGuid()}").Options);
    private readonly ResultService _sut;

    public ReviewerTraceTests()
    {
        _db.Users.Add(new User { Id = 7, Email = "laura@example.test", Name = "Laura Gil", Role = UserRole.Evaluador, IsActive = false });
        _db.Questions.AddRange(
            new Question { Id = 1, Text = "Test", Type = QuestionType.MultipleChoice, Points = 1 },
            new Question { Id = 2, Text = "Abierta", Type = QuestionType.OpenEnded, Points = 4 });
        _db.Exams.AddRange(
            new Exam { Id = 3, Title = "Con abierta y sin evaluador", IsActive = true,
                ExamQuestions = new List<ExamQuestion> { new() { Id = 30, QuestionId = 2, Order = 1 } } },
            new Exam { Id = 4, Title = "Con abierta y con evaluador", IsActive = true,
                ExamQuestions = new List<ExamQuestion> { new() { Id = 40, QuestionId = 2, Order = 1 } } },
            new Exam { Id = 5, Title = "Solo test", IsActive = true,
                ExamQuestions = new List<ExamQuestion> { new() { Id = 50, QuestionId = 1, Order = 1 } } },
            new Exam { Id = 6, Title = "Inactiva con abierta", IsActive = false,
                ExamQuestions = new List<ExamQuestion> { new() { Id = 60, QuestionId = 2, Order = 1 } } });
        _db.ExamEvaluators.Add(new ExamEvaluator { ExamId = 4, UserId = 7, AssignedByUserId = 1 });
        _db.ExamResults.AddRange(
            new ExamResult { Id = 1, ExamId = 3, CandidateName = "Ana", CandidateEmail = "ana@example.test",
                Status = ExamResultStatus.Reviewed, ReviewedByUserId = 7, ReviewedAt = new DateTime(2026, 9, 29, 9, 0, 0, DateTimeKind.Utc),
                ExamSession = new ExamSession { Id = 11 } },
            new ExamResult { Id = 2, ExamId = 5, CandidateName = "Luis", CandidateEmail = "luis@example.test",
                Status = ExamResultStatus.Reviewed, ExamSession = new ExamSession { Id = 12 } });
        _db.SaveChanges();

        _sut = new ResultService(new ExamResultRepository(_db), new ExamRepository(_db), new QuestionRepository(_db));
    }

    [Fact]
    public async Task El_detalle_dice_quien_corrigio_aunque_su_cuenta_este_desactivada()
    {
        var detalle = await _sut.GetDetailAsync(1);

        detalle!.ReviewedByName.Should().Be("Laura Gil");
        detalle.ReviewedAt.Should().Be(new DateTime(2026, 9, 29, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Un_resultado_sin_abiertas_no_tiene_corrector()
        => (await _sut.GetDetailAsync(2))!.ReviewedByName.Should().BeNull();

    [Fact]
    public async Task El_dashboard_lista_solo_las_pruebas_activas_con_abiertas_y_sin_evaluador()
    {
        var stats = await _sut.GetDashboardStatsAsync();

        stats.ExamsWithoutEvaluator!.Select(e => e.Id).Should().Equal(3);
    }
}
