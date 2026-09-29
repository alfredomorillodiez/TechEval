using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// Spec open-question-review — Reserva de un resultado mientras se corrige. Aquí se prueban
/// las reglas. La carrera de dos aperturas simultáneas depende de la escritura condicional
/// de SQL Server y se prueba contra la base real.
/// </summary>
public class ReservationTests
{
    private static readonly DateTime Ahora = new(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);
    private const int Laura = 7, Luis = 8;

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"reserva-{Guid.NewGuid()}").Options);
    private readonly ExamResultRepository _repo;
    private readonly ExamResult _pendiente;

    public ReservationTests()
    {
        _repo = new ExamResultRepository(_db);
        _pendiente = new ExamResult { Id = 1, ExamId = 1, CandidateName = "Ana", CandidateEmail = "ana@example.test",
            Status = ExamResultStatus.PendingReview };
        _db.ExamResults.Add(_pendiente);
        _db.SaveChanges();
    }

    private Task<bool> Reservar(int usuario, int minutosDesdeAhora = 0)
        => _repo.TryReserveAsync(1, usuario, Ahora.AddMinutes(minutosDesdeAhora), Ahora.AddMinutes(minutosDesdeAhora + 30));

    [Fact]
    public async Task Un_resultado_libre_se_reserva()
    {
        (await Reservar(Laura)).Should().BeTrue();

        _pendiente.ReservedByUserId.Should().Be(Laura);
        _pendiente.ReservedUntil.Should().Be(Ahora.AddMinutes(30));
    }

    [Fact]
    public async Task Otra_persona_no_lo_toma_mientras_dura_la_reserva()
    {
        await Reservar(Laura);

        (await Reservar(Luis, minutosDesdeAhora: 10)).Should().BeFalse();
        (await _repo.TryReleaseForSubmitAsync(1, Luis, Ahora.AddMinutes(10))).Should().BeFalse();
        _pendiente.ReservedByUserId.Should().Be(Laura);
    }

    [Fact]
    public async Task Una_reserva_caducada_la_toma_el_siguiente()
    {
        await Reservar(Laura);

        (await Reservar(Luis, minutosDesdeAhora: 31)).Should().BeTrue();
        _pendiente.ReservedByUserId.Should().Be(Luis);
    }

    [Fact]
    public async Task Quien_la_tiene_la_renueva()
    {
        await Reservar(Laura);

        (await Reservar(Laura, minutosDesdeAhora: 25)).Should().BeTrue();
        _pendiente.ReservedUntil.Should().Be(Ahora.AddMinutes(55));
    }

    [Fact]
    public async Task El_envio_libera_la_reserva_propia_o_caducada()
    {
        await Reservar(Laura);

        (await _repo.TryReleaseForSubmitAsync(1, Laura, Ahora.AddMinutes(5))).Should().BeTrue();
        _pendiente.ReservedByUserId.Should().BeNull();
        _pendiente.ReservedUntil.Should().BeNull();
    }

    [Fact]
    public async Task El_envio_sin_reserva_se_permite()
        => (await _repo.TryReleaseForSubmitAsync(1, Luis, Ahora)).Should().BeTrue();

    [Fact]
    public async Task Liberar_la_propia_deja_el_resultado_libre()
    {
        await Reservar(Laura);

        (await _repo.ReleaseAsync(1, Laura)).Should().BeTrue();
        (await Reservar(Luis, minutosDesdeAhora: 1)).Should().BeTrue();
    }

    [Fact]
    public async Task Nadie_libera_la_reserva_de_otro_salvo_el_administrador()
    {
        await Reservar(Laura);

        (await _repo.ReleaseAsync(1, Luis)).Should().BeFalse();
        _pendiente.ReservedByUserId.Should().Be(Laura);

        (await _repo.ReleaseAsync(1, userId: null)).Should().BeTrue();
        _pendiente.ReservedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task Un_resultado_corregido_no_se_reserva()
    {
        _pendiente.Status = ExamResultStatus.Reviewed;
        await _db.SaveChangesAsync();

        (await Reservar(Laura)).Should().BeFalse();
    }

    [Fact]
    public async Task Liberar_todas_las_de_un_usuario()
    {
        _db.ExamResults.Add(new ExamResult { Id = 2, ExamId = 1, CandidateName = "B", CandidateEmail = "b@example.test",
            Status = ExamResultStatus.PendingReview });
        await _db.SaveChangesAsync();
        await Reservar(Laura);
        await _repo.TryReserveAsync(2, Laura, Ahora, Ahora.AddMinutes(30));

        await _repo.ReleaseAllOfAsync(Laura);

        _db.ExamResults.Should().OnlyContain(r => r.ReservedByUserId == null);
    }
}
