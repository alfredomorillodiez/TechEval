using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TechEval.Application;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Services;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// Spec evaluator-review: cola, detalle, envío, señales e historial del evaluador, y que
/// ninguna respuesta lleve la identidad del candidato ni la hora a la que hizo la prueba.
/// </summary>
public class EvaluationServiceTests
{
    private const int Admin = 1, Laura = 7, Luis = 8;
    private const string Nombre = "Ana Candidata", Email = "ana.candidata@example.test";

    // Horas poco corrientes a propósito: la prueba de ceguera busca estos textos.
    private static readonly DateTime Inicio = new(2026, 9, 28, 3, 21, 0, DateTimeKind.Utc);
    private static readonly DateTime Envio = new(2026, 9, 28, 3, 47, 13, DateTimeKind.Utc);

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"evaluacion-{Guid.NewGuid()}").Options);
    private readonly EvaluationService _sut;

    public EvaluationServiceTests()
    {
        _db.Users.AddRange(
            new User { Id = Admin, Email = "admin@example.test", Name = "Admin", Role = UserRole.Admin },
            new User { Id = Laura, Email = "laura@example.test", Name = "Laura", Role = UserRole.Evaluador },
            new User { Id = Luis, Email = "luis@example.test", Name = "Luis", Role = UserRole.Evaluador });
        _db.Exams.AddRange(
            new Exam { Id = 3, Title = "Asignada", PassingScorePercentage = 50,
                ExamQuestions = new List<ExamQuestion> { new() { Id = 30, QuestionId = 2, Order = 1 } } },
            new Exam { Id = 4, Title = "Sin asignar", PassingScorePercentage = 50 });
        _db.Questions.Add(new Question { Id = 2, Text = "¿Por qué?", Type = QuestionType.OpenEnded, Points = 4 });
        _db.ExamEvaluators.Add(new ExamEvaluator { ExamId = 3, UserId = Laura, AssignedByUserId = Admin });

        Pendiente(1, examId: 3);
        Pendiente(2, examId: 4);
        _db.SaveChanges();

        var results = new ExamResultRepository(_db);
        var integrity = new ExamIntegrityService(new ExamTokenRepository(_db), new ExamRepository(_db),
            new BaseRepository<ExamSession>(_db), new BaseRepository<ExamIntegrityEvent>(_db), results);
        _sut = new EvaluationService(results, new ExamRepository(_db), new BaseRepository<UserAnswer>(_db),
            new BaseRepository<User>(_db), new Mock<IEmailService>().Object, integrity, new FakeUnitOfWork());
    }

    private ExamResult Pendiente(int id, int examId, string email = Email, int? userId = null)
    {
        var sesion = new ExamSession
        {
            Id = 100 + id, StartedAt = Inicio, ShuffleSeed = 1,
            UserAnswers = new List<UserAnswer>
            {
                new() { Id = 1000 + id, ExamSessionId = 100 + id, QuestionId = 2, OpenAnswer = "Porque sí", QuestionPointsSnapshot = 4 }
            }
        };
        var r = new ExamResult
        {
            Id = id, ExamId = examId, ExamSessionId = sesion.Id, ExamSession = sesion,
            CandidateName = Nombre, CandidateEmail = email, UserId = userId,
            TotalPoints = 4, Status = ExamResultStatus.PendingReview, CompletedAt = Envio
        };
        _db.ExamResults.Add(r);
        return r;
    }

    private static SubmitReviewDto Correccion(int resultId) => new(new List<ReviewAnswerInputDto> { new(1000 + resultId, 3, "bien") });

    // ---- Cola ---------------------------------------------------------------------------

    [Fact]
    public async Task La_cola_solo_trae_las_pruebas_asignadas()
        => (await _sut.GetQueueAsync(Laura)).Should().ContainSingle().Which.ResultId.Should().Be(1);

    [Fact]
    public async Task Sin_asignaciones_la_cola_esta_vacia()
        => (await _sut.GetQueueAsync(Luis)).Should().BeEmpty();

    [Fact]
    public async Task El_evaluador_no_ve_sus_propios_resultados_ni_por_usuario_ni_por_email()
    {
        Pendiente(5, examId: 3, email: "otro@example.test", userId: Laura);
        Pendiente(6, examId: 3, email: "laura@example.test");
        await _db.SaveChangesAsync();

        (await _sut.GetQueueAsync(Laura)).Select(q => q.ResultId).Should().Equal(1);
        await FluentActions.Invoking(() => _sut.GetDetailAsync(5, Laura)).Should().ThrowAsync<NotFoundException>();
        await FluentActions.Invoking(() => _sut.GetDetailAsync(6, Laura)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Una_reserva_ajena_se_indica_sin_decir_de_quien()
    {
        var r = _db.ExamResults.Single(x => x.Id == 1);
        r.ReservedByUserId = Luis;
        r.ReservedUntil = DateTime.UtcNow.AddMinutes(20);
        await _db.SaveChangesAsync();

        var item = (await _sut.GetQueueAsync(Laura)).Single();

        item.ReservedByOther.Should().BeTrue();
        item.ReservedUntil.Should().NotBeNull();
    }

    // ---- Alias --------------------------------------------------------------------------

    [Fact]
    public async Task El_alias_es_el_mismo_en_la_cola_y_en_el_detalle()
    {
        var cola = (await _sut.GetQueueAsync(Laura)).Single();
        var detalle = await _sut.GetDetailAsync(1, Laura);

        cola.CandidateAlias.Should().Be("Candidato R-1").And.Be(detalle.CandidateAlias);
    }

    [Fact]
    public async Task Dos_resultados_del_mismo_candidato_tienen_alias_distintos()
    {
        Pendiente(7, examId: 3);
        await _db.SaveChangesAsync();

        (await _sut.GetQueueAsync(Laura)).Select(q => q.CandidateAlias).Should().OnlyHaveUniqueItems();
    }

    // ---- Detalle ------------------------------------------------------------------------

    [Theory]
    [InlineData(2)]   // prueba no asignada
    [InlineData(99)]  // no existe
    public async Task Detalle_sin_acceso_es_404(int resultId)
        => await FluentActions.Invoking(() => _sut.GetDetailAsync(resultId, Laura))
            .Should().ThrowAsync<NotFoundException>();

    [Fact]
    public async Task Abrir_el_detalle_reserva_y_trae_la_fecha_sin_hora()
    {
        var detalle = await _sut.GetDetailAsync(1, Laura);

        detalle.CompletedOn.Should().Be(new DateOnly(2026, 9, 28));
        detalle.Answers.Should().ContainSingle(a => a.OpenAnswer == "Porque sí" && a.MaxPoints == 4);
        _db.ExamResults.Single(x => x.Id == 1).ReservedByUserId.Should().Be(Laura);
    }

    [Fact]
    public async Task Tras_quitar_la_asignacion_el_detalle_es_404()
    {
        await _sut.GetDetailAsync(1, Laura);
        _db.ExamEvaluators.RemoveRange(_db.ExamEvaluators);
        await _db.SaveChangesAsync();

        await FluentActions.Invoking(() => _sut.GetDetailAsync(1, Laura)).Should().ThrowAsync<NotFoundException>();
    }

    // ---- Envío --------------------------------------------------------------------------

    [Fact]
    public async Task El_envio_cierra_registra_al_evaluador_y_libera_la_reserva()
    {
        await _sut.GetDetailAsync(1, Laura);

        var resultado = await _sut.SubmitAsync(1, Correccion(1), Laura);

        resultado.Should().Be(new EvaluatorReviewOutcomeDto(1, "Candidato R-1", 3, 4, 75m, true));
        var r = _db.ExamResults.Single(x => x.Id == 1);
        r.Status.Should().Be(ExamResultStatus.Reviewed);
        r.ReviewedByUserId.Should().Be(Laura);
        r.ReservedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task El_envio_sin_asignacion_es_404_y_no_toca_el_resultado()
    {
        await _sut.GetDetailAsync(1, Laura);
        _db.ExamEvaluators.RemoveRange(_db.ExamEvaluators);
        await _db.SaveChangesAsync();

        await FluentActions.Invoking(() => _sut.SubmitAsync(1, Correccion(1), Laura)).Should().ThrowAsync<NotFoundException>();
        _db.ExamResults.Single(x => x.Id == 1).Status.Should().Be(ExamResultStatus.PendingReview);
    }

    [Fact]
    public async Task El_envio_con_reserva_ajena_es_409_y_no_escribe()
    {
        var r = _db.ExamResults.Single(x => x.Id == 1);
        r.ReservedByUserId = Luis;
        r.ReservedUntil = DateTime.UtcNow.AddMinutes(20);
        await _db.SaveChangesAsync();

        await FluentActions.Invoking(() => _sut.SubmitAsync(1, Correccion(1), Laura))
            .Should().ThrowAsync<ReservationConflictException>();
        _db.UserAnswers.Single(a => a.Id == 1001).AwardedPoints.Should().BeNull();
    }

    [Fact]
    public async Task Liberar_solo_libera_la_propia()
    {
        var r = _db.ExamResults.Single(x => x.Id == 1);
        r.ReservedByUserId = Luis;
        r.ReservedUntil = DateTime.UtcNow.AddMinutes(20);
        await _db.SaveChangesAsync();

        await _sut.ReleaseReservationAsync(1, Laura);

        r.ReservedByUserId.Should().Be(Luis);
    }

    // ---- Señales ------------------------------------------------------------------------

    [Fact]
    public async Task Las_senales_llegan_con_el_tiempo_desde_el_inicio_y_sin_hora()
    {
        _db.ExamIntegrityEvents.Add(new ExamIntegrityEvent
        {
            ExamSessionId = 101, Type = IntegrityEventType.PageLeft, OccurredAt = Inicio.AddSeconds(750)
        });
        await _db.SaveChangesAsync();

        var informe = await _sut.GetIntegrityAsync(1, Laura);

        var señal = informe.Events.Single();
        señal.ElapsedSeconds.Should().Be(750);
        señal.OccurredAt.Should().BeNull();
    }

    [Fact]
    public async Task Las_senales_de_un_resultado_no_asignado_son_404()
        => await FluentActions.Invoking(() => _sut.GetIntegrityAsync(2, Laura)).Should().ThrowAsync<NotFoundException>();

    // ---- Historial ----------------------------------------------------------------------

    [Fact]
    public async Task El_historial_sigue_tras_perder_la_asignacion()
    {
        await _sut.SubmitAsync(1, Correccion(1), Laura);
        _db.ExamEvaluators.RemoveRange(_db.ExamEvaluators);
        await _db.SaveChangesAsync();

        (await _sut.GetHistoryAsync(Laura)).Should().ContainSingle(h => h.CandidateAlias == "Candidato R-1");
        var detalle = await _sut.GetHistoryDetailAsync(1, Laura);
        detalle.Answers.Should().ContainSingle(a => a.AwardedPoints == 3 && a.ReviewerComment == "bien");
    }

    [Fact]
    public async Task El_detalle_de_una_correccion_ajena_es_404()
    {
        await _sut.SubmitAsync(1, Correccion(1), Laura);

        await FluentActions.Invoking(() => _sut.GetHistoryDetailAsync(1, Luis)).Should().ThrowAsync<NotFoundException>();
    }

    // ---- Ceguera ------------------------------------------------------------------------

    [Fact]
    public async Task Ninguna_respuesta_del_evaluador_lleva_identidad_ni_hora_del_envio()
    {
        _db.ExamIntegrityEvents.Add(new ExamIntegrityEvent
        {
            ExamSessionId = 101, Type = IntegrityEventType.Paste, QuestionId = 2, PastedChars = 40,
            OccurredAt = Inicio.AddSeconds(750)
        });
        await _db.SaveChangesAsync();

        var respuestas = new List<object>
        {
            await _sut.GetQueueAsync(Laura),
            await _sut.GetDetailAsync(1, Laura),
            await _sut.GetIntegrityAsync(1, Laura),
            await _sut.SubmitAsync(1, Correccion(1), Laura),
            await _sut.GetHistoryAsync(Laura),
            await _sut.GetHistoryDetailAsync(1, Laura),
        };

        foreach (var respuesta in respuestas)
        {
            var json = SinReservas(JsonSerializer.Serialize(respuesta, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            json.Should().NotContain(Nombre).And.NotContain(Email).And.NotContain("candidata");
            // La hora del envío (03:47), la del inicio (03:21) y la de la señal (03:33:30).
            json.Should().NotContain("03:47").And.NotContain("03:21").And.NotContain("03:33");
        }
    }

    // El fin de la reserva sí lleva hora, y no dice nada del candidato: se quita antes de buscar.
    private static string SinReservas(string json)
    {
        var nodo = JsonNode.Parse(json)!;
        void Limpiar(JsonNode? n)
        {
            if (n is JsonObject o)
            {
                o.Remove("reservedUntil");
                foreach (var hijo in o.Select(p => p.Value).ToList()) Limpiar(hijo);
            }
            else if (n is JsonArray a)
                foreach (var hijo in a) Limpiar(hijo);
        }
        Limpiar(nodo);
        return nodo.ToJsonString();
    }
}
