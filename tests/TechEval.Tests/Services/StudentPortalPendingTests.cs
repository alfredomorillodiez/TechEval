using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// Pendientes del portal del alumno. Una prueba a medias tiene que seguir apareciendo:
/// es el segundo camino de vuelta cuando el candidato ya no encuentra el correo.
/// </summary>
public class StudentPortalPendingTests
{
    // ---------- Filtro del repositorio ----------

    private static AppDbContext NuevoContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"portal-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<AppDbContext> ContextoConTokensAsync()
    {
        var db = NuevoContexto();

        var admin = new User { Id = 1, Email = "admin@test.com", Name = "Admin", PasswordHash = "x", IsAdmin = true };
        var alumno = new User { Id = 5, Email = "ana@test.com", Name = "Ana", PasswordHash = "x" };
        db.Users.AddRange(admin, alumno);

        var exam = new Exam { Id = 7, Title = "Prueba", CreatedByUserId = 1, TimeLimitMinutes = 60 };
        db.Exams.Add(exam);

        // 1 — sin empezar y en plazo
        db.ExamTokens.Add(new ExamToken
        {
            Id = 1, Token = "sin-empezar", ExamId = 7, UserId = 5,
            CandidateName = "Ana", CandidateEmail = "ana@test.com",
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });

        // 2 — empezada y sin terminar
        db.ExamTokens.Add(new ExamToken
        {
            Id = 2, Token = "a-medias", ExamId = 7, UserId = 5, IsUsed = true,
            CandidateName = "Ana", CandidateEmail = "ana@test.com",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            ExamSession = new ExamSession { Id = 20, Status = SessionStatus.InProgress }
        });

        // 3 — ya enviada
        db.ExamTokens.Add(new ExamToken
        {
            Id = 3, Token = "enviada", ExamId = 7, UserId = 5, IsUsed = true,
            CandidateName = "Ana", CandidateEmail = "ana@test.com",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            ExamSession = new ExamSession { Id = 30, Status = SessionStatus.Completed }
        });

        // 4 — enviada pero con la sesión todavía marcada InProgress: el proceso cayó entre
        // la escritura del resultado y la del estado. Está terminada de hecho.
        db.ExamTokens.Add(new ExamToken
        {
            Id = 6, Token = "enviada-a-medio-cerrar", ExamId = 7, UserId = 5, IsUsed = true,
            CandidateName = "Ana", CandidateEmail = "ana@test.com",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            ExamSession = new ExamSession
            {
                Id = 60,
                Status = SessionStatus.InProgress,
                ExamResult = new ExamResult
                {
                    Id = 60, ExamId = 7, CandidateName = "Ana", CandidateEmail = "ana@test.com"
                }
            }
        });

        // 5 — caducada sin empezar
        db.ExamTokens.Add(new ExamToken
        {
            Id = 4, Token = "caducada", ExamId = 7, UserId = 5,
            CandidateName = "Ana", CandidateEmail = "ana@test.com",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        });

        // 6 — de otro alumno
        db.ExamTokens.Add(new ExamToken
        {
            Id = 5, Token = "de-otro", ExamId = 7, UserId = 99,
            CandidateName = "Luis", CandidateEmail = "luis@test.com",
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });

        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Pendientes_IncluyenLaSinEmpezarYLaQueEstaAMedias()
    {
        using var db = await ContextoConTokensAsync();
        var repo = new ExamTokenRepository(db);

        var pendientes = await repo.GetPendingByUserAsync(5);

        pendientes.Select(t => t.Token).Should().BeEquivalentTo(new[] { "sin-empezar", "a-medias" });
    }

    [Fact]
    public async Task Pendientes_ExcluyenLaEnviadaYLaCaducadaSinEmpezar()
    {
        using var db = await ContextoConTokensAsync();
        var repo = new ExamTokenRepository(db);

        var pendientes = await repo.GetPendingByUserAsync(5);

        pendientes.Select(t => t.Token).Should().NotContain("enviada");
        pendientes.Select(t => t.Token).Should().NotContain("caducada");
    }

    [Fact]
    public async Task Pendientes_ExcluyenLaEnviadaAunqueLaSesionSigaMarcadaEnCurso()
    {
        using var db = await ContextoConTokensAsync();
        var repo = new ExamTokenRepository(db);

        var pendientes = await repo.GetPendingByUserAsync(5);

        pendientes.Select(t => t.Token).Should().NotContain("enviada-a-medio-cerrar");
    }

    [Fact]
    public async Task Pendientes_NoIncluyenLasDeOtroAlumno()
    {
        using var db = await ContextoConTokensAsync();
        var repo = new ExamTokenRepository(db);

        var pendientes = await repo.GetPendingByUserAsync(5);

        pendientes.Select(t => t.Token).Should().NotContain("de-otro");
    }

    [Fact]
    public async Task Pendientes_AlumnoSinInvitaciones_DevuelveListaVacia()
    {
        using var db = await ContextoConTokensAsync();
        var repo = new ExamTokenRepository(db);

        var pendientes = await repo.GetPendingByUserAsync(1234);

        pendientes.Should().BeEmpty();
    }

    // ---------- Marca de estado que ve el portal ----------

    [Fact]
    public async Task Portal_DistingueLaPruebaSinEmpezarDeLaQueEstaAMedias()
    {
        var tokenRepo = new Mock<IExamTokenRepository>();
        var resultRepo = new Mock<IExamResultRepository>();
        var exam = new Exam { Id = 7, Title = "Prueba" };

        tokenRepo.Setup(r => r.GetPendingByUserAsync(5, default)).ReturnsAsync(new List<ExamToken>
        {
            new()
            {
                Token = "sin-empezar", Exam = exam, ExpiresAt = DateTime.UtcNow.AddDays(1)
            },
            new()
            {
                Token = "a-medias", Exam = exam, ExpiresAt = DateTime.UtcNow.AddDays(1), IsUsed = true,
                ExamSession = new ExamSession { Id = 20, Status = SessionStatus.InProgress }
            }
        });

        var sut = new StudentPortalService(tokenRepo.Object, resultRepo.Object);

        var pendientes = await sut.GetPendingAsync(5);

        pendientes.Single(p => p.Token == "sin-empezar").InProgress.Should().BeFalse();
        pendientes.Single(p => p.Token == "a-medias").InProgress.Should().BeTrue();
    }
}
