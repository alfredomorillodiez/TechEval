using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Services;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// La reserva también vale para el administrador (spec open-question-review), y él puede
/// liberar la de otro.
/// </summary>
public class AdminReservationTests
{
    private const int Admin = 1, Laura = 7;

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"admin-reserva-{Guid.NewGuid()}").Options);
    private readonly OpenQuestionReviewService _sut;
    private readonly ExamResult _resultado;

    public AdminReservationTests()
    {
        _db.Users.AddRange(
            new User { Id = Admin, Email = "admin@example.test", Name = "Admin", Role = UserRole.Admin },
            new User { Id = Laura, Email = "laura@example.test", Name = "Laura Gil", Role = UserRole.Evaluador });
        var exam = new Exam { Id = 3, Title = "Prueba", PassingScorePercentage = 50 };
        var abierta = new UserAnswer
        {
            Id = 20, ExamSessionId = 5, QuestionId = 2, OpenAnswer = "texto",
            Question = new Question { Id = 2, Text = "¿Por qué?", Type = QuestionType.OpenEnded, Points = 4 }
        };
        _resultado = new ExamResult
        {
            Id = 1, ExamId = 3, Exam = exam, CandidateName = "Ana", CandidateEmail = "ana@example.test",
            TotalPoints = 4, Status = ExamResultStatus.PendingReview,
            ExamSession = new ExamSession { Id = 5, UserAnswers = new List<UserAnswer> { abierta } }
        };
        _db.ExamResults.Add(_resultado);
        _db.SaveChanges();

        var resultService = new Mock<IResultService>();
        resultService.Setup(r => r.GetDetailAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExamResultDto(1, "Ana", "ana@example.test", "Prueba", 4, 3, 75m, true,
                ExamResultStatus.Reviewed, DateTime.UtcNow, new List<AnswerReviewDto>()));

        _sut = new OpenQuestionReviewService(
            new ExamResultRepository(_db), new ExamRepository(_db), new BaseRepository<UserAnswer>(_db),
            new Mock<IEmailService>().Object, resultService.Object, new FakeUnitOfWork());
    }

    private void ReservadoPorLaura()
    {
        _resultado.ReservedByUserId = Laura;
        _resultado.ReservedUntil = DateTime.UtcNow.AddMinutes(20);
        _db.SaveChanges();
    }

    private static SubmitReviewDto Correccion() => new(new List<ReviewAnswerInputDto> { new(20, 3, null) });

    [Fact]
    public async Task Abrir_el_detalle_reserva_para_el_administrador()
    {
        var detalle = await _sut.GetDetailAsync(1, Admin);

        detalle!.ReservedUntil.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
        _resultado.ReservedByUserId.Should().Be(Admin);
    }

    [Fact]
    public async Task El_administrador_recibe_409_con_el_nombre_de_quien_la_tiene()
    {
        ReservadoPorLaura();

        var abrir = () => _sut.GetDetailAsync(1, Admin);

        (await abrir.Should().ThrowAsync<ReservationConflictException>())
            .Which.Message.Should().Contain("Laura Gil");
    }

    [Fact]
    public async Task El_envio_del_administrador_con_reserva_ajena_no_escribe_nada()
    {
        ReservadoPorLaura();

        await FluentActions.Invoking(() => _sut.SubmitReviewAsync(1, Correccion(), Admin))
            .Should().ThrowAsync<ReservationConflictException>();
        _resultado.Status.Should().Be(ExamResultStatus.PendingReview);
        _db.UserAnswers.Single().AwardedPoints.Should().BeNull();
    }

    [Fact]
    public async Task Tras_liberar_la_reserva_ajena_el_administrador_corrige()
    {
        ReservadoPorLaura();

        await _sut.ReleaseReservationAsync(1);
        await _sut.SubmitReviewAsync(1, Correccion(), Admin);

        _resultado.Status.Should().Be(ExamResultStatus.Reviewed);
        _resultado.ReviewedByUserId.Should().Be(Admin);
        _resultado.ReservedByUserId.Should().BeNull();
    }
}
