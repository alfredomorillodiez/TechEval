using FluentAssertions;
using Xunit;
using TechEval.Application;
using TechEval.Application.Services;
using TechEval.Domain.Enums;

namespace TechEval.Tests.Services;

/// <summary>
/// Spec exam-management — Reparto por nivel y rechazo si un nivel no alcanza. Los números son
/// los de los escenarios de la spec.
/// </summary>
public class DifficultyPlannerTests
{
    private static Dictionary<DifficultyLevel, int> N(int basico, int intermedio, int avanzado) => new()
    {
        [DifficultyLevel.Basic] = basico,
        [DifficultyLevel.Intermediate] = intermedio,
        [DifficultyLevel.Advanced] = avanzado
    };

    private static readonly Dictionary<DifficultyLevel, int> Mucho = N(100, 100, 100);

    private static void Reparto(DifficultyPlan plan, int b, int i, int a)
        => plan.Requested.Should().BeEquivalentTo(N(b, i, a));

    [Fact]
    public void Reparto_exacto()
        => Reparto(DifficultyPlanner.Plan(10, N(30, 50, 20), Mucho), 3, 5, 2);

    [Fact]
    public void Mayor_resto_suma_siempre_el_total()
        => Reparto(DifficultyPlanner.Plan(10, N(33, 33, 34), Mucho), 3, 3, 4);

    [Fact]
    public void En_el_empate_gana_el_nivel_mas_facil()
        => Reparto(DifficultyPlanner.Plan(3, N(50, 50, 0), Mucho), 2, 1, 0);

    [Fact]
    public void En_el_empate_de_decimales_gana_el_de_mayor_porcentaje()
        // 10 × 15 % = 1,5 · 10 × 35 % = 3,5 · 10 × 50 % = 5 → falta 1, y los dos 0,5 empatan:
        // gana el intermedio, que tiene más porcentaje, aunque el básico sea más fácil.
        => Reparto(DifficultyPlanner.Plan(10, N(15, 35, 50), Mucho), 1, 4, 5);

    [Fact]
    public void Un_nivel_al_cero_no_aparece()
        => Reparto(DifficultyPlanner.Plan(6, N(0, 100, 0), Mucho), 0, 6, 0);

    [Theory]
    [InlineData(30, 30, 30)]
    [InlineData(50, 60, -10)]
    [InlineData(120, -20, 0)]
    public void Porcentajes_no_validos_se_rechazan(int b, int i, int a)
        => FluentActions.Invoking(() => DifficultyPlanner.Plan(10, N(b, i, a), Mucho))
            .Should().Throw<ValidationException>();

    [Fact]
    public void Falta_un_nivel_en_el_reparto()
        => FluentActions.Invoking(() => DifficultyPlanner.Plan(10,
                new Dictionary<DifficultyLevel, int> { [DifficultyLevel.Basic] = 50, [DifficultyLevel.Advanced] = 50 }, Mucho))
            .Should().Throw<ValidationException>();

    [Fact]
    public void Faltan_avanzadas()
    {
        var plan = DifficultyPlanner.Plan(10, N(20, 30, 50), N(8, 15, 2));

        plan.CanGenerate.Should().BeFalse();
        plan.Shortages.Should().Equal(new LevelShortage(DifficultyLevel.Advanced, 5, 2));
    }

    [Fact]
    public void Faltan_dos_niveles()
    {
        var plan = DifficultyPlanner.Plan(10, N(30, 40, 30), N(1, 20, 1));

        plan.Shortages.Should().Equal(
            new LevelShortage(DifficultyLevel.Basic, 3, 1),
            new LevelShortage(DifficultyLevel.Advanced, 3, 1));
    }

    [Fact]
    public void Sobrar_de_otros_niveles_no_evita_el_faltante()
    {
        var plan = DifficultyPlanner.Plan(5, N(0, 100, 0), N(50, 1, 0));

        plan.Shortages.Should().Equal(new LevelShortage(DifficultyLevel.Intermediate, 5, 1));
    }

    [Fact]
    public void Con_disponibilidad_justa_se_puede_generar()
        => DifficultyPlanner.Plan(10, N(30, 50, 20), N(3, 5, 2)).CanGenerate.Should().BeTrue();
}
