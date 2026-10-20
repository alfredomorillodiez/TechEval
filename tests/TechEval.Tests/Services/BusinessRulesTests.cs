using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TechEval.Application;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// Validación de los datos de una prueba y de una invitación, prueba desactivada que no admite
/// invitaciones, y categoría desactivada que no aporta preguntas. Hasta ahora nada de esto se
/// comprobaba en el servidor: se podía crear una prueba de 0 minutos o sin preguntas.
/// </summary>
public class BusinessRulesTests
{
    // ---------- Datos de la prueba ----------

    private readonly Mock<IExamRepository> _examRepo = new();
    private readonly Mock<IQuestionRepository> _questionRepo = new();
    private readonly ExamService _exams;

    public BusinessRulesTests() => _exams = new ExamService(_examRepo.Object, _questionRepo.Object);

    private static CreateExamDto Prueba(
        string titulo = "Prueba", int minutos = 60, int nota = 70, params int[] preguntas)
        => new(titulo, "", minutos, nota, preguntas.Length == 0 ? new List<int> { 1 } : preguntas.ToList());

    private async Task AssertRechazaAsync(CreateExamDto dto, string fragmento)
    {
        var act = () => _exams.CreateAsync(dto, 1);
        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().Contain(fragmento);

        // Se valida antes de leer o escribir nada.
        _questionRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Question, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
        _examRepo.Verify(r => r.AddAsync(It.IsAny<Exam>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(601)]
    public async Task Crear_ConTiempoFueraDeRango_SeRechaza(int minutos)
        => await AssertRechazaAsync(Prueba(minutos: minutos), "entre 1 y 600 minutos");

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(150)]
    public async Task Crear_ConNotaDeCorteFueraDeRango_SeRechaza(int nota)
        => await AssertRechazaAsync(Prueba(nota: nota), "entre 0 y 100");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Crear_SinTitulo_SeRechaza(string titulo)
        => await AssertRechazaAsync(Prueba(titulo: titulo), "título es obligatorio");

    [Fact]
    public async Task Crear_ConTituloDe201Caracteres_SeRechaza()
        => await AssertRechazaAsync(Prueba(titulo: new string('t', 201)), "200 caracteres");

    [Fact]
    public async Task Crear_SinPreguntas_SeRechaza()
        => await AssertRechazaAsync(
            new CreateExamDto("Prueba", "", 60, 70, new List<int>()), "al menos una pregunta");

    [Fact]
    public async Task Crear_ConPreguntasRepetidas_SeRechaza()
        => await AssertRechazaAsync(Prueba(preguntas: new[] { 1, 2, 1 }), "repetidas");

    [Fact]
    public async Task Crear_ConLosLimites_Pasa()
    {
        _questionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Question, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Question> { new() { Id = 1, IsActive = true, Answers = new List<Answer>() } });
        _examRepo.Setup(r => r.AddAsync(It.IsAny<Exam>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Exam e, CancellationToken _) => { e.Id = 5; return e; });
        _examRepo.Setup(r => r.GetWithQuestionsAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Exam { Id = 5, Title = "T", ExamQuestions = new List<ExamQuestion>() });

        await _exams.CreateAsync(Prueba(minutos: 1, nota: 0), 1);
        await _exams.CreateAsync(Prueba(minutos: 600, nota: 100), 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public async Task Generar_ConCantidadFueraDeRango_SeRechaza(int cantidad)
    {
        var act = () => _exams.GenerateAsync(
            new GenerateExamDto("Prueba", "", 60, 70, cantidad, null, null), 1);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().Contain("entre 1 y 200");
        _questionRepo.Verify(r => r.GetRandomAsync(
            It.IsAny<int>(), It.IsAny<List<int>?>(), It.IsAny<DifficultyLevel?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Generar_ConTiempoCero_SeRechaza()
    {
        var act = () => _exams.GenerateAsync(new GenerateExamDto("Prueba", "", 0, 70, 5, null, null), 1);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Editar_ConTiempoCero_SeRechazaSinLeerLaPrueba()
    {
        var act = () => _exams.UpdateAsync(3, new UpdateExamDto("Prueba", "", 0, 70, true));

        await act.Should().ThrowAsync<ValidationException>();
        _examRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- Invitaciones ----------

    private sealed class Invitaciones
    {
        public Mock<IExamTokenRepository> Tokens { get; } = new();
        public Mock<IExamRepository> Exams { get; } = new();
        public Mock<IRepository<User>> Users { get; } = new();
        public Mock<IEmailService> Email { get; } = new();
        public ExamTokenService Sut { get; }

        public Invitaciones(bool activa = true)
        {
            Users.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());
            Exams.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Exam { Id = 7, Title = "Prueba SQL", IsActive = activa });

            var tokens = new Mock<ITokenService>();
            tokens.Setup(t => t.GenerateSecureToken()).Returns("tok");

            Sut = new ExamTokenService(
                Tokens.Object, Exams.Object,
                new Mock<IRepository<ExamSession>>().Object, new Mock<IRepository<UserAnswer>>().Object,
                new Mock<IExamResultRepository>().Object, Users.Object,
                Email.Object, tokens.Object, new FakeUnitOfWork());
        }

        public void SinTokensNiCorreos()
        {
            Tokens.Verify(r => r.AddAsync(It.IsAny<ExamToken>(), It.IsAny<CancellationToken>()), Times.Never);
            Email.Verify(e => e.SendExamInvitationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Theory]
    [InlineData("no-es-un-correo")]
    [InlineData("ana@sinpunto")]
    [InlineData("Ana <ana@x.com>")]
    [InlineData("")]
    public async Task Invitar_ConCorreoInvalido_SeRechazaSinCrearNada(string correo)
    {
        var f = new Invitaciones();

        var act = () => f.Sut.SendExamAsync(new SendExamDto(7, "Ana", correo, 72), "http://x");

        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().Contain("correo");
        f.SinTokensNiCorreos();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(721)]
    public async Task Invitar_ConExpiracionFueraDeRango_SeRechaza(int horas)
    {
        var f = new Invitaciones();

        var act = () => f.Sut.SendExamAsync(new SendExamDto(7, "Ana", "ana@x.com", horas), "http://x");

        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().Contain("entre 1 y 720 horas");
        f.SinTokensNiCorreos();
    }

    [Fact]
    public async Task Invitar_SinNombre_SeRechaza()
    {
        var f = new Invitaciones();

        var act = () => f.Sut.SendExamAsync(new SendExamDto(7, "  ", "ana@x.com", 72), "http://x");

        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().Contain("nombre");
    }

    [Fact]
    public async Task Invitar_ADatosValidos_CreaElTokenYEnviaElCorreo()
    {
        var f = new Invitaciones();

        var token = await f.Sut.SendExamAsync(new SendExamDto(7, "Ana", "ana@x.com", 72), "http://x");

        token.Should().Be("tok");
        f.Tokens.Verify(r => r.AddAsync(It.IsAny<ExamToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invitar_APruebaDesactivada_Da409SinTokensNiCorreo()
    {
        var f = new Invitaciones(activa: false);

        var act = () => f.Sut.SendExamAsync(new SendExamDto(7, "Ana", "ana@x.com", 72), "http://x");

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message
            .Should().Contain("Prueba SQL").And.Contain("desactivada");
        f.SinTokensNiCorreos();
    }

    [Fact]
    public async Task Masivo_APruebaDesactivada_SeRechazaEntero()
    {
        var f = new Invitaciones(activa: false);
        var dto = new BulkSendExamDto(7, new List<BulkCandidateDto> { new("A", "a@x.com"), new("B", "b@x.com") }, 72);

        var act = () => f.Sut.SendExamBulkAsync(dto, "http://x");

        await act.Should().ThrowAsync<ConflictException>();
        f.SinTokensNiCorreos();
    }

    [Fact]
    public async Task Masivo_Vacio_SeRechaza()
    {
        var f = new Invitaciones();

        var act = () => f.Sut.SendExamBulkAsync(new BulkSendExamDto(7, new List<BulkCandidateDto>(), 72), "http://x");

        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().Contain("vacía");
    }

    [Fact]
    public async Task Masivo_DeMasDeQuinientos_SeRechaza()
    {
        var f = new Invitaciones();
        var lista = Enumerable.Range(1, 501).Select(i => new BulkCandidateDto($"C{i}", $"c{i}@x.com")).ToList();

        var act = () => f.Sut.SendExamBulkAsync(new BulkSendExamDto(7, lista, 72), "http://x");

        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().Contain("500");
    }

    [Fact]
    public async Task Masivo_ConUnaLineaInvalida_NoCreaNingunToken()
    {
        var f = new Invitaciones();
        var dto = new BulkSendExamDto(7, new List<BulkCandidateDto>
        {
            new("Ana", "ana@x.com"), new("Luis", "luis-sin-arroba")
        }, 72);

        var act = () => f.Sut.SendExamBulkAsync(dto, "http://x");

        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().StartWith("Candidato 2:");
        f.SinTokensNiCorreos();
    }

    // ---------- Categoría desactivada ----------

    private static AppDbContext ContextoConCategorias()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"categorias-{Guid.NewGuid()}").Options;
        var db = new AppDbContext(options);

        db.Categories.AddRange(
            new Category { Id = 1, Name = "Activa", IsActive = true },
            new Category { Id = 2, Name = "De baja", IsActive = false });
        db.Questions.AddRange(
            Pregunta(1, 1, DifficultyLevel.Basic, activa: true),
            Pregunta(2, 1, DifficultyLevel.Basic, activa: true),
            Pregunta(3, 2, DifficultyLevel.Basic, activa: true),    // activa, pero su categoría no
            Pregunta(4, 2, DifficultyLevel.Advanced, activa: true),
            Pregunta(5, 1, DifficultyLevel.Basic, activa: false));  // desactivada ella misma
        db.SaveChanges();
        return db;
    }

    private static Question Pregunta(int id, int categoria, DifficultyLevel nivel, bool activa) => new()
    {
        Id = id, Text = $"P{id}", Type = QuestionType.MultipleChoice, CategoryId = categoria,
        Difficulty = nivel, IsActive = activa, Points = 1,
        Answers = new List<Answer> { new() { Id = id * 10, Text = "A", IsCorrect = true, Order = 1 } }
    };

    [Fact]
    public async Task Generar_NoTomaPreguntasDeUnaCategoriaDesactivada()
    {
        using var db = ContextoConCategorias();
        var repo = new QuestionRepository(db);

        var elegidas = await repo.GetRandomAsync(10, null, null);

        elegidas.Select(q => q.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task Generar_PidiendoUnaCategoriaDesactivada_NoDevuelveNada()
    {
        using var db = ContextoConCategorias();
        var repo = new QuestionRepository(db);

        (await repo.GetRandomAsync(10, new List<int> { 2 }, null)).Should().BeEmpty();
    }

    [Fact]
    public async Task Disponibilidad_NoCuentaUnaCategoriaDesactivada()
    {
        using var db = ContextoConCategorias();
        var repo = new QuestionRepository(db);

        var cuentas = await repo.CountByDifficultyAsync(null);

        cuentas[DifficultyLevel.Basic].Should().Be(2);
        cuentas[DifficultyLevel.Advanced].Should().Be(0, "la única avanzada es de la categoría de baja");
    }
}
