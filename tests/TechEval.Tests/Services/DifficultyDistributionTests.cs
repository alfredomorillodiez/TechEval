using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TechEval.Application;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// Spec exam-management — generación con reparto por nivel; spec question-bank —
/// disponibilidad por nivel.
/// </summary>
public class DifficultyDistributionTests
{
    private const int Sql = 1, CSharp = 2;

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"reparto-{Guid.NewGuid()}").Options);
    private readonly QuestionRepository _questions;
    private readonly ExamService _sut;
    private int _nextId = 1;

    public DifficultyDistributionTests()
    {
        _db.Users.Add(new User { Id = 1, Email = "admin@example.test", Name = "Admin", Role = UserRole.Admin });
        _db.Categories.AddRange(new Category { Id = Sql, Name = "SQL" }, new Category { Id = CSharp, Name = "C#" });
        _db.SaveChanges();

        _questions = new QuestionRepository(_db);
        _sut = new ExamService(new ExamRepository(_db), _questions);
    }

    private void Banco(int categoria, int basicas, int intermedias, int avanzadas, bool activas = true)
    {
        foreach (var (nivel, n) in new[] { (DifficultyLevel.Basic, basicas), (DifficultyLevel.Intermediate, intermedias), (DifficultyLevel.Advanced, avanzadas) })
            for (var i = 0; i < n; i++)
                _db.Questions.Add(new Question
                {
                    Id = _nextId++, Text = $"{nivel} {i}", CategoryId = categoria, Difficulty = nivel,
                    Type = QuestionType.MultipleChoice, Points = 1, IsActive = activas
                });
        _db.SaveChanges();
    }

    private static Dictionary<DifficultyLevel, int> P(int b, int i, int a) => new()
    {
        [DifficultyLevel.Basic] = b, [DifficultyLevel.Intermediate] = i, [DifficultyLevel.Advanced] = a
    };

    private static GenerateExamDto Pedir(int total, Dictionary<DifficultyLevel, int>? reparto,
        List<int>? categorias = null, DifficultyLevel? nivel = null)
        => new("Prueba", "", 30, 50, total, categorias, nivel, reparto);

    private static int[] RepartoDe(ExamDto exam) => new[]
    {
        exam.Questions.Count(q => q.Difficulty == DifficultyLevel.Basic),
        exam.Questions.Count(q => q.Difficulty == DifficultyLevel.Intermediate),
        exam.Questions.Count(q => q.Difficulty == DifficultyLevel.Advanced)
    };

    // ---- Disponibilidad -----------------------------------------------------------------

    [Fact]
    public async Task La_disponibilidad_no_cuenta_bajas_y_trae_los_tres_niveles()
    {
        Banco(Sql, 8, 15, 2);
        Banco(Sql, 0, 0, 1, activas: false);
        Banco(CSharp, 5, 0, 0);

        var sql = await _questions.CountByDifficultyAsync(new List<int> { Sql });
        var csharp = await _questions.CountByDifficultyAsync(new List<int> { CSharp });

        sql.Should().BeEquivalentTo(P(8, 15, 2));
        csharp[DifficultyLevel.Advanced].Should().Be(0);
    }

    [Fact]
    public async Task Sin_categorias_cuenta_todo_el_banco()
    {
        Banco(Sql, 1, 1, 1);
        Banco(CSharp, 2, 2, 2);

        (await _questions.CountByDifficultyAsync(null)).Should().BeEquivalentTo(P(3, 3, 3));
    }

    // ---- Generación ---------------------------------------------------------------------

    [Fact]
    public async Task La_prueba_tiene_el_reparto_del_plan()
    {
        Banco(Sql, 20, 20, 20);

        var exam = await _sut.GenerateAsync(Pedir(10, P(30, 50, 20)), 1);

        RepartoDe(exam).Should().Equal(3, 5, 2);
        exam.Questions.Select(q => q.Order).Should().Equal(Enumerable.Range(1, 10));
    }

    [Fact]
    public async Task Un_nivel_que_no_alcanza_rechaza_aunque_sobren_otros()
    {
        Banco(Sql, 8, 15, 2);

        var generar = () => _sut.GenerateAsync(Pedir(10, P(20, 30, 50), new List<int> { Sql }), 1);

        (await generar.Should().ThrowAsync<ValidationException>())
            .Which.Message.Should().Contain("avanzado").And.Contain("se necesitan 5 y hay 2");
        _db.Exams.Should().BeEmpty();
    }

    [Fact]
    public async Task No_usa_preguntas_de_otras_categorias()
    {
        Banco(Sql, 10, 10, 0);
        Banco(CSharp, 0, 0, 10);

        await FluentActions.Invoking(() => _sut.GenerateAsync(Pedir(5, P(40, 40, 20), new List<int> { Sql }), 1))
            .Should().ThrowAsync<ValidationException>();

        var soloSql = await _sut.GenerateAsync(Pedir(5, P(60, 40, 0), new List<int> { Sql }), 1);
        soloSql.Questions.Should().OnlyContain(q => q.Difficulty != DifficultyLevel.Advanced);
    }

    [Fact]
    public async Task Nivel_unico_y_reparto_a_la_vez_es_400()
    {
        Banco(Sql, 10, 10, 10);

        await FluentActions.Invoking(() => _sut.GenerateAsync(Pedir(5, P(0, 0, 100), nivel: DifficultyLevel.Advanced), 1))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task El_nivel_unico_sigue_estricto()
    {
        Banco(Sql, 10, 10, 2);

        await FluentActions.Invoking(() => _sut.GenerateAsync(Pedir(5, null, nivel: DifficultyLevel.Advanced), 1))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Sin_nivel_ni_reparto_sigue_como_hoy()
    {
        Banco(Sql, 3, 3, 3);

        var exam = await _sut.GenerateAsync(Pedir(9, null), 1);

        exam.Questions.Should().HaveCount(9);
    }

    [Fact]
    public async Task Los_niveles_salen_mezclados()
    {
        Banco(Sql, 30, 30, 30);

        // Con 30 preguntas de tres niveles, que salgan agrupadas por azar en cinco
        // generaciones seguidas es prácticamente imposible.
        var agrupadas = 0;
        for (var i = 0; i < 5; i++)
        {
            var exam = await _sut.GenerateAsync(Pedir(30, P(34, 33, 33)), 1);
            var niveles = exam.Questions.OrderBy(q => q.Order).Select(q => q.Difficulty).ToList();
            if (niveles.SequenceEqual(niveles.OrderBy(n => n))) agrupadas++;
        }

        agrupadas.Should().Be(0);
    }
}
