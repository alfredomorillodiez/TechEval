using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using TechEval.Application;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Services;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Repositories;
using TechEval.Infrastructure.Security;

namespace TechEval.Tests.Services;

/// <summary>
/// Spec exam-management — Asignación de evaluadores; y spec user-management — el cambio
/// de rol de un evaluador limpia sus asignaciones y reservas.
/// </summary>
public class ExamEvaluatorServiceTests
{
    private const int Admin = 1, Laura = 7, Alumno = 9, Inactivo = 10;

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"asignacion-{Guid.NewGuid()}").Options);
    private readonly ExamEvaluatorService _sut;

    public ExamEvaluatorServiceTests()
    {
        _db.Users.AddRange(
            new User { Id = Admin, Email = "admin@example.test", Name = "Admin", Role = UserRole.Admin, PasswordHash = "h" },
            new User { Id = 2, Email = "otro@example.test", Name = "Otro", Role = UserRole.Admin, PasswordHash = "h" },
            new User { Id = Laura, Email = "laura@example.test", Name = "Laura Gil", Role = UserRole.Evaluador, PasswordHash = "h" },
            new User { Id = Alumno, Email = "ana@example.test", Name = "Ana", Role = UserRole.Alumno },
            new User { Id = Inactivo, Email = "luis@example.test", Name = "Luis", Role = UserRole.Evaluador, IsActive = false });
        _db.Exams.AddRange(new Exam { Id = 3, Title = "A" }, new Exam { Id = 4, Title = "B" });
        _db.SaveChanges();

        _sut = new ExamEvaluatorService(
            new BaseRepository<ExamEvaluator>(_db), new BaseRepository<User>(_db), new ExamRepository(_db));
    }

    [Fact]
    public async Task Asignar_un_evaluador_lo_incluye_en_la_lista()
    {
        var lista = await _sut.AssignAsync(3, Laura, Admin);

        lista.Should().ContainSingle().Which.Name.Should().Be("Laura Gil");
        _db.ExamEvaluators.Single().AssignedByUserId.Should().Be(Admin);
    }

    [Theory]
    [InlineData(Admin)]
    [InlineData(Alumno)]
    [InlineData(Inactivo)]
    public async Task Solo_se_asignan_evaluadores_activos(int usuario)
    {
        await FluentActions.Invoking(() => _sut.AssignAsync(3, usuario, Admin))
            .Should().ThrowAsync<ValidationException>();
        _db.ExamEvaluators.Should().BeEmpty();
    }

    [Fact]
    public async Task Asignar_dos_veces_no_duplica()
    {
        await _sut.AssignAsync(3, Laura, Admin);
        await _sut.AssignAsync(3, Laura, Admin);

        _db.ExamEvaluators.Should().ContainSingle();
    }

    [Fact]
    public async Task Prueba_inexistente_es_404()
        => await FluentActions.Invoking(() => _sut.AssignAsync(99, Laura, Admin))
            .Should().ThrowAsync<NotFoundException>();

    [Fact]
    public async Task Quitar_la_asignacion_conserva_las_correcciones()
    {
        await _sut.AssignAsync(3, Laura, Admin);
        _db.ExamResults.Add(new ExamResult { Id = 1, ExamId = 3, CandidateName = "Ana", CandidateEmail = "ana@example.test",
            Status = ExamResultStatus.Reviewed, ReviewedByUserId = Laura });
        await _db.SaveChangesAsync();

        (await _sut.UnassignAsync(3, Laura)).Should().BeEmpty();

        _db.ExamResults.Single().ReviewedByUserId.Should().Be(Laura);
    }

    // ---- Cambio de rol ------------------------------------------------------------------

    private UserManagementService Usuarios()
    {
        var users = new BaseRepository<User>(_db);
        var passwordSetup = new PasswordSetupService(new BaseRepository<PasswordSetupToken>(_db), users,
            new TokenService(Options.Create(new JwtSettings())), new Mock<IEmailService>().Object, new FakeUnitOfWork());
        return new UserManagementService(users, passwordSetup, new AdminCountLock(_db), new FakeUnitOfWork(),
            new BaseRepository<ExamEvaluator>(_db), new ExamResultRepository(_db));
    }

    [Fact]
    public async Task El_evaluador_ascendido_pierde_asignaciones_y_reservas_pero_no_sus_correcciones()
    {
        await _sut.AssignAsync(3, Laura, Admin);
        await _sut.AssignAsync(4, Laura, Admin);
        _db.ExamResults.AddRange(
            new ExamResult { Id = 1, ExamId = 3, CandidateName = "A", CandidateEmail = "a@example.test",
                Status = ExamResultStatus.PendingReview, ReservedByUserId = Laura, ReservedUntil = DateTime.UtcNow.AddMinutes(20) },
            new ExamResult { Id = 2, ExamId = 3, CandidateName = "B", CandidateEmail = "b@example.test",
                Status = ExamResultStatus.Reviewed, ReviewedByUserId = Laura });
        await _db.SaveChangesAsync();

        await Usuarios().ChangeRoleAsync(Laura, new(UserRole.Admin), Admin, "http://web");

        _db.ExamEvaluators.Should().BeEmpty();
        _db.ExamResults.Single(r => r.Id == 1).ReservedByUserId.Should().BeNull();
        _db.ExamResults.Single(r => r.Id == 2).ReviewedByUserId.Should().Be(Laura);
    }

    [Fact]
    public async Task El_evaluador_desactivado_y_reactivado_conserva_sus_asignaciones()
    {
        await _sut.AssignAsync(3, Laura, Admin);
        var usuarios = Usuarios();

        await usuarios.DeactivateAsync(Laura, Admin);
        await usuarios.ActivateAsync(Laura);

        (await _sut.ListAsync(3)).Should().ContainSingle(e => e.UserId == Laura);
    }
}
