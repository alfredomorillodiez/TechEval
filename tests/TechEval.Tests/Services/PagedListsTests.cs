using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TechEval.Application;
using TechEval.Application.Services;
using TechEval.Domain.Common;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// Spec list-pagination: contrato, filtros, orden, desempate y cifras de los listados paginados.
/// Con EF en memoria. El orden por texto con tildes y la intercalación se verifican contra SQL Server.
/// </summary>
public class PagedListsTests
{
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"paginacion-{Guid.NewGuid()}").Options);

    private static readonly DateTime Base = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    // ---------- PageQuery ----------

    [Theory]
    [InlineData(1, 25, 1, 25, 0)]
    [InlineData(0, 25, 1, 25, 0)]
    [InlineData(-4, 25, 1, 25, 0)]
    [InlineData(3, 25, 3, 25, 50)]
    [InlineData(2, 0, 2, 25, 25)]
    [InlineData(2, -3, 2, 25, 25)]
    [InlineData(2, 5000, 2, 100, 100)]
    [InlineData(1, 100, 1, 100, 0)]
    [InlineData(1, 101, 1, 100, 0)]
    public void PageQuery_corrige_lo_que_esta_fuera_de_rango(
        int page, int size, int expectedPage, int expectedSize, int expectedSkip)
    {
        var q = new PageQuery { Page = page, PageSize = size };

        q.EffectivePage.Should().Be(expectedPage);
        q.EffectivePageSize.Should().Be(expectedSize);
        q.Skip.Should().Be(expectedSkip);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  ana ", "ana")]
    public void PageQuery_Text_recorta_y_trata_el_blanco_como_nulo(string? q, string? esperado)
        => new PageQuery { Q = q }.Text.Should().Be(esperado);

    [Fact]
    public void PageQuery_SortKey_va_en_minusculas()
        => new PageQuery { Sort = " Score " }.SortKey.Should().Be("score");

    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(25, 25, 1)]
    [InlineData(26, 25, 2)]
    [InlineData(60, 25, 3)]
    public void PagedResult_TotalPages(int total, int size, int paginas)
        => new PagedResult<int>(new List<int>(), total, 1, size).TotalPages.Should().Be(paginas);

    [Fact]
    public void PagedResult_Map_conserva_total_y_pagina()
    {
        var r = new PagedResult<int>(new List<int> { 1, 2 }, 40, 2, 25).Map(x => x * 10);

        r.Items.Should().Equal(10, 20);
        (r.Total, r.Page, r.PageSize).Should().Be((40, 2, 25));
    }

    [Fact]
    public void Un_parametro_PageQuery_no_se_llama_como_ninguna_de_sus_propiedades()
    {
        // Con [FromQuery] PageQuery page, MVC toma «page» como prefijo del objeto y no como el valor
        // `page=3` de la cadena de consulta: `page`, `pageSize` y `q` se ignoran y el listado devuelve
        // siempre la primera página de 25. Las pruebas del servicio no lo ven; solo un GET real.
        var reservados = typeof(PageQuery).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToHashSet();

        var controladores = typeof(TechEval.API.Controllers.ResultsController).Assembly.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t));

        var usos = 0;
        var culpables = new List<string>();
        foreach (var tipo in controladores)
            foreach (var metodo in tipo.GetMethods())
                foreach (var parametro in metodo.GetParameters())
                {
                    if (parametro.ParameterType != typeof(PageQuery)) continue;
                    usos++;
                    if (reservados.Contains(parametro.Name!.ToLowerInvariant()))
                        culpables.Add($"{tipo.Name}.{metodo.Name}({parametro.Name})");
                }

        usos.Should().BeGreaterThanOrEqualTo(5, "la comprobación tiene que estar mirando los listados de verdad");
        culpables.Should().BeEmpty("un parámetro con el nombre de una propiedad rompe el enlazado de la consulta");
    }

    // ---------- Resultados: datos ----------

    private ExamResultRepository Resultados() => new(_db);

    private int _siguiente = 1;

    private ExamResult Resultado(
        string nombre, string correo, decimal nota, ExamResultStatus estado = ExamResultStatus.Reviewed,
        bool? aprobado = null, int examId = 1, int diasAntes = 0)
    {
        if (!_db.Exams.Any(e => e.Id == examId))
            _db.Exams.Add(new Exam { Id = examId, Title = $"Prueba {examId}" });

        var r = new ExamResult
        {
            Id = _siguiente,
            ExamSessionId = _siguiente,
            ExamId = examId,
            CandidateName = nombre,
            CandidateEmail = correo,
            ScorePercentage = nota,
            Status = estado,
            Passed = estado == ExamResultStatus.PendingReview ? null : aprobado ?? nota >= 70,
            CompletedAt = Base.AddDays(-diasAntes)
        };
        _siguiente++;
        _db.ExamResults.Add(r);
        _db.SaveChanges();
        return r;
    }

    private static PageQuery Q(string? sort = null, bool desc = false, int page = 1, int size = 25, string? q = null)
        => new() { Sort = sort, Desc = desc, Page = page, PageSize = size, Q = q };

    // ---------- Resultados: filtros ----------

    [Fact]
    public async Task Resultados_busca_por_nombre_y_por_correo_sin_distinguir_mayusculas()
    {
        Resultado("Ana Pérez", "ana@empresa.com", 80);
        Resultado("Luis Gil", "luis@empresa.com", 60);

        var porNombre = await Resultados().GetPagedAsync(new ResultFilter(), Q(q: "PÉREZ"));
        var porCorreo = await Resultados().GetPagedAsync(new ResultFilter(), Q(q: "ANA@"));

        porNombre.Items.Should().ContainSingle().Which.CandidateEmail.Should().Be("ana@empresa.com");
        porCorreo.Items.Should().ContainSingle().Which.CandidateName.Should().Be("Ana Pérez");
        porCorreo.Total.Should().Be(1);
    }

    [Fact]
    public async Task Resultados_el_comodin_de_LIKE_se_busca_literal()
    {
        Resultado("Ana", "ana@empresa.com", 80);
        Resultado("100% Real", "real@empresa.com", 80);

        var r = await Resultados().GetPagedAsync(new ResultFilter(), Q(q: "%"));

        r.Items.Should().ContainSingle().Which.CandidateName.Should().Be("100% Real");
    }

    [Theory]
    [InlineData("pending", 1)]
    [InlineData("passed", 2)]
    [InlineData("failed", 1)]
    [InlineData("PASSED", 2)]
    [InlineData("", 4)]
    [InlineData("otra-cosa", 4)]
    [InlineData(null, 4)]
    public async Task Resultados_filtra_por_veredicto(string? outcome, int esperados)
    {
        Resultado("A", "a@t.com", 90);
        Resultado("B", "b@t.com", 75);
        Resultado("C", "c@t.com", 40);
        Resultado("D", "d@t.com", 30, ExamResultStatus.PendingReview);

        var r = await Resultados().GetPagedAsync(new ResultFilter(Outcome: outcome), Q());

        r.Total.Should().Be(esperados);
    }

    [Fact]
    public async Task Resultados_el_rango_de_nota_deja_fuera_a_los_pendientes()
    {
        Resultado("Corregido", "a@t.com", 80);
        Resultado("Pendiente", "b@t.com", 85, ExamResultStatus.PendingReview);

        var min = await Resultados().GetPagedAsync(new ResultFilter(ScoreMin: 70), Q());
        var max = await Resultados().GetPagedAsync(new ResultFilter(ScoreMax: 90), Q());

        min.Items.Should().ContainSingle().Which.CandidateName.Should().Be("Corregido");
        max.Items.Should().ContainSingle().Which.CandidateName.Should().Be("Corregido");
    }

    [Fact]
    public async Task Resultados_el_rango_de_nota_incluye_los_extremos()
    {
        Resultado("Justo 70", "a@t.com", 70);
        Resultado("Justo 90", "b@t.com", 90);
        Resultado("Fuera", "c@t.com", 91);

        var r = await Resultados().GetPagedAsync(new ResultFilter(ScoreMin: 70, ScoreMax: 90), Q());

        r.Items.Select(x => x.CandidateName).Should().BeEquivalentTo("Justo 70", "Justo 90");
    }

    [Fact]
    public async Task Resultados_from_es_inclusivo_y_to_es_exclusivo()
    {
        var corte = Base;
        Resultado("Antes", "a@t.com", 80, diasAntes: 1);
        Resultado("En el corte", "b@t.com", 80, diasAntes: 0);

        var desde = await Resultados().GetPagedAsync(new ResultFilter(From: corte), Q());
        var hasta = await Resultados().GetPagedAsync(new ResultFilter(To: corte), Q());

        desde.Items.Should().ContainSingle().Which.CandidateName.Should().Be("En el corte");
        hasta.Items.Should().ContainSingle().Which.CandidateName.Should().Be("Antes");
    }

    [Fact]
    public async Task Resultados_filtra_por_prueba()
    {
        Resultado("De uno", "a@t.com", 80, examId: 1);
        Resultado("De dos", "b@t.com", 80, examId: 2);

        var r = await Resultados().GetPagedAsync(new ResultFilter(ExamId: 2), Q());

        r.Items.Should().ContainSingle().Which.ExamId.Should().Be(2);
    }

    // ---------- Resultados: orden y páginas ----------

    [Fact]
    public async Task Resultados_sin_orden_van_por_fecha_descendente()
    {
        Resultado("Viejo", "a@t.com", 80, diasAntes: 5);
        Resultado("Nuevo", "b@t.com", 80, diasAntes: 0);
        Resultado("Medio", "c@t.com", 80, diasAntes: 2);

        var r = await Resultados().GetPagedAsync(new ResultFilter(), Q());

        r.Items.Select(x => x.CandidateName).Should().Equal("Nuevo", "Medio", "Viejo");
    }

    [Theory]
    [InlineData("candidate", false, new[] { "ana", "Bea", "carlos" })]
    [InlineData("candidate", true, new[] { "carlos", "Bea", "ana" })]
    [InlineData("score", false, new[] { "carlos", "ana", "Bea" })]
    [InlineData("score", true, new[] { "Bea", "ana", "carlos" })]
    [InlineData("date", false, new[] { "carlos", "ana", "Bea" })]
    [InlineData("date", true, new[] { "Bea", "ana", "carlos" })]
    public async Task Resultados_ordena_por_cada_columna_sin_distinguir_mayusculas(
        string sort, bool desc, string[] esperado)
    {
        Resultado("carlos", "c@t.com", 50, diasAntes: 9);
        Resultado("ana", "a@t.com", 70, diasAntes: 5);
        Resultado("Bea", "b@t.com", 90, diasAntes: 1);

        var r = await Resultados().GetPagedAsync(new ResultFilter(), Q(sort, desc));

        r.Items.Select(x => x.CandidateName).Should().Equal(esperado);
    }

    [Fact]
    public async Task Resultados_ordena_por_prueba()
    {
        Resultado("A", "a@t.com", 80, examId: 3);
        Resultado("B", "b@t.com", 80, examId: 1);
        Resultado("C", "c@t.com", 80, examId: 2);

        var r = await Resultados().GetPagedAsync(new ResultFilter(), Q("exam"));

        r.Items.Select(x => x.Exam.Title).Should().Equal("Prueba 1", "Prueba 2", "Prueba 3");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resultados_los_pendientes_quedan_al_final_al_ordenar_por_nota(bool desc)
    {
        Resultado("Pendiente", "p@t.com", 99, ExamResultStatus.PendingReview);
        Resultado("Baja", "b@t.com", 40);
        Resultado("Alta", "a@t.com", 95);

        var r = await Resultados().GetPagedAsync(new ResultFilter(), Q("score", desc));

        r.Items.Last().CandidateName.Should().Be("Pendiente");
    }

    [Fact]
    public async Task Resultados_ordena_por_veredicto_aprobado_reprobado_pendiente()
    {
        Resultado("Pendiente", "p@t.com", 50, ExamResultStatus.PendingReview);
        Resultado("Reprobado", "r@t.com", 40);
        Resultado("Aprobado", "a@t.com", 90);

        var r = await Resultados().GetPagedAsync(new ResultFilter(), Q("outcome"));

        r.Items.Select(x => x.CandidateName).Should().Equal("Aprobado", "Reprobado", "Pendiente");
    }

    [Fact]
    public async Task Resultados_una_columna_desconocida_cae_en_el_orden_natural()
    {
        Resultado("Viejo", "a@t.com", 80, diasAntes: 5);
        Resultado("Nuevo", "b@t.com", 80, diasAntes: 0);

        var r = await Resultados().GetPagedAsync(new ResultFilter(), Q("DROP TABLE"));

        r.Items.Select(x => x.CandidateName).Should().Equal("Nuevo", "Viejo");
    }

    [Fact]
    public async Task Resultados_con_la_misma_nota_dos_paginas_no_repiten_ni_pierden_filas()
    {
        for (var i = 0; i < 30; i++) Resultado($"C{i:00}", $"c{i}@t.com", 70);

        var p1 = await Resultados().GetPagedAsync(new ResultFilter(), Q("score", size: 10, page: 1));
        var p2 = await Resultados().GetPagedAsync(new ResultFilter(), Q("score", size: 10, page: 2));
        var p3 = await Resultados().GetPagedAsync(new ResultFilter(), Q("score", size: 10, page: 3));

        var ids = p1.Items.Concat(p2.Items).Concat(p3.Items).Select(x => x.Id).ToList();
        ids.Should().OnlyHaveUniqueItems().And.HaveCount(30);
    }

    [Fact]
    public async Task Resultados_ultima_pagina_incompleta_y_pagina_mas_alla_del_final()
    {
        for (var i = 0; i < 60; i++) Resultado($"C{i:00}", $"c{i}@t.com", 70);

        var ultima = await Resultados().GetPagedAsync(new ResultFilter(), Q(page: 3, size: 25));
        var mas = await Resultados().GetPagedAsync(new ResultFilter(), Q(page: 9, size: 25));
        var primera = await Resultados().GetPagedAsync(new ResultFilter(), Q());

        ultima.Items.Should().HaveCount(10);
        ultima.Total.Should().Be(60);
        mas.Items.Should().BeEmpty();
        (mas.Total, mas.Page).Should().Be((60, 9));
        (primera.Items.Count, primera.Total, primera.Page, primera.PageSize).Should().Be((25, 60, 1, 25));
    }

    [Fact]
    public async Task Resultados_el_total_cuenta_el_filtro_y_no_la_pagina()
    {
        for (var i = 0; i < 12; i++) Resultado($"Ana {i}", $"ana{i}@t.com", 70);
        for (var i = 0; i < 8; i++) Resultado($"Luis {i}", $"luis{i}@t.com", 70);

        var r = await Resultados().GetPagedAsync(new ResultFilter(), Q(q: "ana", size: 5));

        r.Items.Should().HaveCount(5);
        r.Total.Should().Be(12);
    }

    // ---------- Cifras de resultados ----------

    [Fact]
    public async Task Cifras_globales()
    {
        for (var i = 0; i < 6; i++) Resultado($"A{i}", $"a{i}@t.com", 80);
        for (var i = 0; i < 4; i++) Resultado($"R{i}", $"r{i}@t.com", 40);
        for (var i = 0; i < 4; i++) Resultado($"P{i}", $"p{i}@t.com", 10, ExamResultStatus.PendingReview);

        var s = await Resultados().GetStatsAsync(null);

        (s.Total, s.Reviewed, s.Pending, s.Passed).Should().Be((14, 10, 4, 6));
        s.AverageScore.Should().Be(64m);
        (s.MaxScore, s.MinScore).Should().Be((80m, 40m));
    }

    [Fact]
    public async Task Cifras_los_pendientes_no_entran_en_la_nota_media()
    {
        Resultado("Corregido", "a@t.com", 80);
        Resultado("Pendiente", "p@t.com", 10, ExamResultStatus.PendingReview);

        var s = await Resultados().GetStatsAsync(null);

        s.AverageScore.Should().Be(80m);
        s.Distribution.Sum().Should().Be(1);
    }

    [Fact]
    public async Task Cifras_sin_corregidos_todo_a_cero()
    {
        Resultado("Pendiente", "p@t.com", 10, ExamResultStatus.PendingReview);

        var s = await Resultados().GetStatsAsync(null);

        (s.Total, s.Reviewed, s.Pending, s.Passed).Should().Be((1, 0, 1, 0));
        (s.AverageScore, s.MaxScore, s.MinScore).Should().Be((0m, 0m, 0m));
        s.Distribution.Should().Equal(0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public async Task Cifras_sin_ningun_resultado()
    {
        var s = await Resultados().GetStatsAsync(null);

        (s.Total, s.Reviewed, s.Pending).Should().Be((0, 0, 0));
        s.Distribution.Should().Equal(0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public async Task Cifras_de_una_prueba_no_cuentan_las_de_otra()
    {
        Resultado("De uno", "a@t.com", 90, examId: 1);
        Resultado("De dos", "b@t.com", 30, examId: 2);

        var s = await Resultados().GetStatsAsync(2);

        (s.Total, s.MaxScore, s.MinScore).Should().Be((1, 30m, 30m));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(49, 0)]
    [InlineData(49.5, 0)]
    [InlineData(50, 1)]
    [InlineData(59, 1)]
    [InlineData(59.9, 1)]
    [InlineData(60, 2)]
    [InlineData(69, 2)]
    [InlineData(70, 3)]
    [InlineData(79, 3)]
    [InlineData(80, 4)]
    [InlineData(89, 4)]
    [InlineData(89.9, 4)]
    [InlineData(90, 5)]
    [InlineData(100, 5)]
    public async Task Cifras_los_tramos_de_la_distribucion_en_los_limites(double nota, int tramo)
    {
        Resultado("X", "x@t.com", (decimal)nota);

        var s = await Resultados().GetStatsAsync(null);

        var esperado = new int[6];
        esperado[tramo] = 1;
        s.Distribution.Should().Equal(esperado);
    }

    // ---------- Cifras en el servicio ----------

    [Fact]
    public async Task Servicio_calcula_el_porcentaje_de_aprobados_y_pone_el_titulo()
    {
        for (var i = 0; i < 6; i++) Resultado($"A{i}", $"a{i}@t.com", 80);
        for (var i = 0; i < 4; i++) Resultado($"R{i}", $"r{i}@t.com", 40);
        var sut = new ResultService(Resultados(), new ExamRepository(_db), new QuestionRepository(_db));

        var s = await sut.GetStatsAsync(1);

        s.PassRate.Should().Be(60);
        s.ExamTitle.Should().Be("Prueba 1");
    }

    [Fact]
    public async Task Servicio_global_no_lleva_titulo_y_sin_corregidos_el_porcentaje_es_cero()
    {
        Resultado("Pendiente", "p@t.com", 10, ExamResultStatus.PendingReview);
        var sut = new ResultService(Resultados(), new ExamRepository(_db), new QuestionRepository(_db));

        var s = await sut.GetStatsAsync(null);

        s.ExamTitle.Should().BeNull();
        s.PassRate.Should().Be(0);
    }

    [Fact]
    public async Task Servicio_una_prueba_que_no_existe_es_NotFound()
    {
        var sut = new ResultService(Resultados(), new ExamRepository(_db), new QuestionRepository(_db));

        var act = () => sut.GetStatsAsync(999);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---------- Preguntas ----------

    private QuestionRepository Preguntas() => new(_db);

    private int _preguntas = 1;

    private Question Pregunta(
        string texto, string categoria, DifficultyLevel nivel = DifficultyLevel.Basic,
        QuestionType tipo = QuestionType.MultipleChoice, int puntos = 1, bool activa = true)
    {
        var cat = _db.Categories.FirstOrDefault(c => c.Name == categoria)
            ?? _db.Categories.Add(new Category { Name = categoria }).Entity;
        _db.SaveChanges();

        var q = new Question
        {
            Id = _preguntas++, Text = texto, CategoryId = cat.Id, Difficulty = nivel, Type = tipo,
            Points = puntos, IsActive = activa,
            Answers = new List<Answer> { new() { Text = "opción", IsCorrect = true, Order = 1 } }
        };
        _db.Questions.Add(q);
        _db.SaveChanges();
        return q;
    }

    [Fact]
    public async Task Preguntas_combina_filtros_y_busca_en_el_enunciado()
    {
        var sql = Pregunta("¿Qué hace un JOIN?", "SQL", DifficultyLevel.Advanced);
        Pregunta("¿Qué hace un JOIN?", "SQL", DifficultyLevel.Basic);
        Pregunta("¿Qué es un índice?", "SQL", DifficultyLevel.Advanced);
        Pregunta("¿Qué hace un JOIN?", "Otra", DifficultyLevel.Advanced);

        var r = await Preguntas().GetPagedAsync(
            new QuestionFilter(CategoryId: sql.CategoryId, Difficulty: DifficultyLevel.Advanced), Q(q: "join"));

        r.Items.Should().ContainSingle().Which.Id.Should().Be(sql.Id);
        r.Total.Should().Be(1);
    }

    [Fact]
    public async Task Preguntas_filtra_por_tipo()
    {
        Pregunta("Test", "A", tipo: QuestionType.MultipleChoice);
        Pregunta("Abierta", "A", tipo: QuestionType.OpenEnded);

        var r = await Preguntas().GetPagedAsync(new QuestionFilter(Type: QuestionType.OpenEnded), Q());

        r.Items.Should().ContainSingle().Which.Text.Should().Be("Abierta");
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    [InlineData(null, 3)]
    public async Task Preguntas_el_filtro_de_estado_tiene_tres_valores(bool? activa, int esperadas)
    {
        Pregunta("Activa 1", "A");
        Pregunta("Activa 2", "A");
        Pregunta("Inactiva", "A", activa: false);

        var r = await Preguntas().GetPagedAsync(new QuestionFilter(Active: activa), Q());

        r.Total.Should().Be(esperadas);
    }

    [Fact]
    public async Task Preguntas_no_cargan_las_opciones_y_si_la_categoria()
    {
        Pregunta("Con opciones", "SQL");

        var r = await Preguntas().GetPagedAsync(new QuestionFilter(), Q());

        r.Items.Single().Answers.Should().BeEmpty();
        r.Items.Single().Category.Name.Should().Be("SQL");
    }

    [Fact]
    public async Task Preguntas_sin_orden_van_por_categoria_y_nivel()
    {
        Pregunta("b-avanzada", "B", DifficultyLevel.Advanced);
        Pregunta("a-avanzada", "A", DifficultyLevel.Advanced);
        Pregunta("a-basica", "A", DifficultyLevel.Basic);

        var r = await Preguntas().GetPagedAsync(new QuestionFilter(), Q());

        r.Items.Select(x => x.Text).Should().Equal("a-basica", "a-avanzada", "b-avanzada");
    }

    [Theory]
    [InlineData("text", false, new[] { "alfa", "Beta", "gamma" })]
    [InlineData("text", true, new[] { "gamma", "Beta", "alfa" })]
    [InlineData("points", false, new[] { "gamma", "alfa", "Beta" })]
    [InlineData("points", true, new[] { "Beta", "alfa", "gamma" })]
    [InlineData("difficulty", false, new[] { "alfa", "Beta", "gamma" })]
    [InlineData("difficulty", true, new[] { "gamma", "Beta", "alfa" })]
    public async Task Preguntas_ordena_por_cada_columna(string sort, bool desc, string[] esperado)
    {
        Pregunta("gamma", "A", DifficultyLevel.Advanced, puntos: 1);
        Pregunta("alfa", "A", DifficultyLevel.Basic, puntos: 3);
        Pregunta("Beta", "A", DifficultyLevel.Intermediate, puntos: 5);

        var r = await Preguntas().GetPagedAsync(new QuestionFilter(), Q(sort, desc));

        r.Items.Select(x => x.Text).Should().Equal(esperado);
    }

    [Fact]
    public async Task Preguntas_ordena_por_categoria_tipo_y_estado()
    {
        Pregunta("z", "Zeta", tipo: QuestionType.OpenEnded, activa: false);
        Pregunta("a", "Alfa", tipo: QuestionType.MultipleChoice, activa: true);

        (await Preguntas().GetPagedAsync(new QuestionFilter(), Q("category")))
            .Items.Select(x => x.Text).Should().Equal("a", "z");
        (await Preguntas().GetPagedAsync(new QuestionFilter(), Q("type")))
            .Items.Select(x => x.Text).Should().Equal("a", "z");
        (await Preguntas().GetPagedAsync(new QuestionFilter(), Q("status")))
            .Items.Select(x => x.Text).Should().Equal("z", "a");
    }

    [Fact]
    public async Task Preguntas_con_los_mismos_valores_dos_paginas_no_repiten_ni_pierden_filas()
    {
        for (var i = 0; i < 30; i++) Pregunta($"Pregunta {i}", "A", puntos: 1);

        var p1 = await Preguntas().GetPagedAsync(new QuestionFilter(), Q("points", size: 10, page: 1));
        var p2 = await Preguntas().GetPagedAsync(new QuestionFilter(), Q("points", size: 10, page: 2));
        var p3 = await Preguntas().GetPagedAsync(new QuestionFilter(), Q("points", size: 10, page: 3));

        p1.Items.Concat(p2.Items).Concat(p3.Items).Select(x => x.Id)
            .Should().OnlyHaveUniqueItems().And.HaveCount(30);
    }

    // ---------- Pruebas ----------

    private ExamRepository Pruebas() => new(_db);

    /// <summary>Una prueba con 4 preguntas de 5 puntos, 3 invitaciones, 2 resultados y 1 abandonada.</summary>
    private Exam PruebaCompleta(int id, string titulo, int minutos = 60, int diasAntes = 0, bool activa = true)
    {
        var exam = new Exam
        {
            Id = id, Title = titulo, TimeLimitMinutes = minutos, IsActive = activa,
            CreatedAt = Base.AddDays(-diasAntes)
        };
        _db.Exams.Add(exam);
        for (var i = 0; i < 4; i++)
        {
            var q = new Question { Id = id * 100 + i, Text = $"q{i}", Points = 5, CategoryId = 1 };
            _db.Questions.Add(q);
            _db.Set<ExamQuestion>().Add(new ExamQuestion { ExamId = id, QuestionId = q.Id, Order = i });
        }

        ExamToken Token(int n, SessionStatus? estado, bool conResultado = false)
        {
            var t = new ExamToken { Id = id * 100 + n, ExamId = id, Token = $"t{id}-{n}", CandidateEmail = $"c{n}@t.com" };
            _db.ExamTokens.Add(t);
            if (estado is { } e)
            {
                var s = new ExamSession { Id = id * 100 + n, ExamTokenId = t.Id, Status = e };
                _db.ExamSessions.Add(s);
                if (conResultado)
                    _db.ExamResults.Add(new ExamResult { Id = id * 100 + n, ExamSessionId = s.Id, ExamId = id });
            }
            return t;
        }

        Token(1, SessionStatus.Completed, conResultado: true);
        Token(2, SessionStatus.Completed, conResultado: true);
        Token(3, SessionStatus.Abandoned);
        _db.SaveChanges();
        return exam;
    }

    [Fact]
    public async Task Pruebas_los_recuentos_salen_de_la_consulta()
    {
        PruebaCompleta(1, "Completa");

        var fila = (await Pruebas().GetPagedSummariesAsync(new ExamFilter(), Q())).Items.Single();

        (fila.QuestionCount, fila.TotalPoints, fila.InvitationCount, fila.ResultCount, fila.AbandonedCount)
            .Should().Be((4, 20, 3, 2, 1));
    }

    [Fact]
    public async Task Pruebas_los_recuentos_coinciden_con_los_de_contar_el_arbol_en_memoria()
    {
        // Lo que hacía el listado antiguo: cargar cada prueba con todo su árbol y contar.
        PruebaCompleta(1, "Una");
        PruebaCompleta(2, "Dos");

        var nuevo = (await Pruebas().GetPagedSummariesAsync(new ExamFilter(), Q("title"))).Items;
        var antiguo = await _db.Exams
            .Include(e => e.ExamQuestions).ThenInclude(eq => eq.Question)
            .Include(e => e.ExamTokens).ThenInclude(t => t.ExamSession).ThenInclude(s => s!.ExamResult)
            .ToListAsync();

        foreach (var fila in nuevo)
        {
            var e = antiguo.Single(x => x.Id == fila.Id);
            fila.QuestionCount.Should().Be(e.ExamQuestions.Count);
            fila.TotalPoints.Should().Be(e.ExamQuestions.Sum(eq => eq.Question?.Points ?? 0));
            fila.InvitationCount.Should().Be(e.ExamTokens.Count);
            fila.ResultCount.Should().Be(e.ExamTokens.Count(t => t.ExamSession?.ExamResult != null));
            fila.AbandonedCount.Should().Be(e.ExamTokens.Count(t => t.ExamSession?.Status == SessionStatus.Abandoned));
        }
    }

    [Fact]
    public async Task Pruebas_sin_preguntas_ni_invitaciones_tienen_todo_a_cero()
    {
        _db.Exams.Add(new Exam { Id = 1, Title = "Vacía" });
        _db.SaveChanges();

        var fila = (await Pruebas().GetPagedSummariesAsync(new ExamFilter(), Q())).Items.Single();

        (fila.QuestionCount, fila.TotalPoints, fila.InvitationCount, fila.ResultCount, fila.AbandonedCount)
            .Should().Be((0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Pruebas_busca_por_titulo_y_filtra_por_estado()
    {
        PruebaCompleta(1, "SQL básico");
        PruebaCompleta(2, "SQL avanzado", activa: false);
        PruebaCompleta(3, "Java");

        var texto = await Pruebas().GetPagedSummariesAsync(new ExamFilter(), Q(q: "sql"));
        var activas = await Pruebas().GetPagedSummariesAsync(new ExamFilter(Active: true), Q(q: "sql"));
        var inactivas = await Pruebas().GetPagedSummariesAsync(new ExamFilter(Active: false), Q());

        texto.Total.Should().Be(2);
        activas.Items.Should().ContainSingle().Which.Title.Should().Be("SQL básico");
        inactivas.Items.Should().ContainSingle().Which.Title.Should().Be("SQL avanzado");
    }

    [Theory]
    [InlineData("title", false, new[] { "alfa", "Beta", "gamma" })]
    [InlineData("title", true, new[] { "gamma", "Beta", "alfa" })]
    [InlineData("created", false, new[] { "alfa", "Beta", "gamma" })]
    [InlineData("created", true, new[] { "gamma", "Beta", "alfa" })]
    [InlineData("time", false, new[] { "Beta", "gamma", "alfa" })]
    [InlineData("time", true, new[] { "alfa", "gamma", "Beta" })]
    public async Task Pruebas_ordena_por_cada_columna(string sort, bool desc, string[] esperado)
    {
        PruebaCompleta(1, "gamma", minutos: 45, diasAntes: 1);
        PruebaCompleta(2, "alfa", minutos: 90, diasAntes: 9);
        PruebaCompleta(3, "Beta", minutos: 30, diasAntes: 5);

        var r = await Pruebas().GetPagedSummariesAsync(new ExamFilter(), Q(sort, desc));

        r.Items.Select(x => x.Title).Should().Equal(esperado);
    }

    [Fact]
    public async Task Pruebas_ordena_por_preguntas_e_invitaciones()
    {
        PruebaCompleta(1, "Una");
        var dos = PruebaCompleta(2, "Dos");
        _db.ExamTokens.Add(new ExamToken { Id = 999, ExamId = dos.Id, Token = "extra", CandidateEmail = "x@t.com" });
        _db.Set<ExamQuestion>().Add(new ExamQuestion { ExamId = dos.Id, QuestionId = 200, Order = 9 });
        _db.SaveChanges();

        (await Pruebas().GetPagedSummariesAsync(new ExamFilter(), Q("invitations", desc: true)))
            .Items.First().Title.Should().Be("Dos");
        (await Pruebas().GetPagedSummariesAsync(new ExamFilter(), Q("questions", desc: true)))
            .Items.First().Title.Should().Be("Dos");
    }

    [Fact]
    public async Task Pruebas_las_opciones_salen_ordenadas_por_titulo_sin_distinguir_mayusculas()
    {
        _db.Exams.AddRange(
            new Exam { Id = 1, Title = "zeta" }, new Exam { Id = 2, Title = "Alfa" }, new Exam { Id = 3, Title = "beta" });
        _db.SaveChanges();

        var o = await Pruebas().GetOptionsAsync();

        o.Select(x => x.Title).Should().Equal("Alfa", "beta", "zeta");
        o.Select(x => x.Id).Should().Equal(2, 3, 1);
    }

    // ---------- Rango de días ----------

    [Fact]
    public void DayRange_un_dia_en_horario_de_verano_de_Madrid()
    {
        var madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");

        var (from, to) = DayRange.ToUtc(new DateTime(2026, 10, 8), new DateTime(2026, 10, 8), madrid);

        // Madrid en octubre antes del 25 es UTC+2.
        from.Should().Be(new DateTime(2026, 10, 7, 22, 0, 0, DateTimeKind.Utc));
        to.Should().Be(new DateTime(2026, 10, 8, 22, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void DayRange_el_dia_del_cambio_de_hora_dura_25_horas()
    {
        var madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");

        // El 25 de octubre de 2026 se retrasa el reloj: de UTC+2 a UTC+1.
        var (from, to) = DayRange.ToUtc(new DateTime(2026, 10, 25), new DateTime(2026, 10, 25), madrid);

        from.Should().Be(new DateTime(2026, 10, 24, 22, 0, 0, DateTimeKind.Utc));
        to.Should().Be(new DateTime(2026, 10, 25, 23, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void DayRange_en_UTC_no_desplaza()
    {
        var (from, to) = DayRange.ToUtc(new DateTime(2026, 10, 8, 15, 30, 0), new DateTime(2026, 10, 9, 3, 0, 0), TimeZoneInfo.Utc);

        from.Should().Be(new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc));
        to.Should().Be(new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void DayRange_sin_fechas_no_filtra()
        => DayRange.ToUtc(null, null, TimeZoneInfo.Utc).Should().Be((null, null));

    [Fact]
    public void DayRange_en_una_zona_al_oeste_de_UTC()
    {
        var bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

        var (from, to) = DayRange.ToUtc(new DateTime(2026, 10, 8), null, bogota);

        from.Should().Be(new DateTime(2026, 10, 8, 5, 0, 0, DateTimeKind.Utc));
        to.Should().BeNull();
    }

    // ---------- Genérico: IRepository.GetPageAsync ----------

    [Fact]
    public async Task GetPageAsync_filtra_ordena_salta_y_cuenta()
    {
        for (var i = 1; i <= 10; i++)
            _db.Users.Add(new User { Email = $"u{i}@t.com", Name = $"U{i:00}", Role = UserRole.Evaluador });
        _db.Users.Add(new User { Email = "admin@t.com", Name = "Admin", Role = UserRole.Admin });
        _db.SaveChanges();

        var (items, total) = await new BaseRepository<User>(_db).GetPageAsync(
            u => u.Role == UserRole.Evaluador,
            q => q.OrderByDescending(u => u.Name).ThenBy(u => u.Id),
            skip: 2, take: 3);

        total.Should().Be(10);
        items.Select(u => u.Name).Should().Equal("U08", "U07", "U06");
    }
}
