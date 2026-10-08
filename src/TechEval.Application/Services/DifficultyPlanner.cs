using TechEval.Domain.Enums;

namespace TechEval.Application.Services;

/// <summary>Un nivel del reparto que necesita más preguntas de las que hay.</summary>
public record LevelShortage(DifficultyLevel Level, int Needed, int Available);

/// <summary>
/// Cuántas preguntas salen de cada nivel y qué niveles no alcanzan. Con faltantes, la
/// generación se rechaza: un nivel nunca se completa con preguntas de otro.
/// </summary>
public record DifficultyPlan(
    IReadOnlyDictionary<DifficultyLevel, int> Requested,
    IReadOnlyList<LevelShortage> Shortages)
{
    public bool CanGenerate => Shortages.Count == 0;
}

/// <summary>
/// Reparto de una prueba por nivel de dificultad. Spec exam-management — Reparto por nivel.
/// </summary>
/// <remarks>
/// Es una función pura a propósito: la usa el servidor al generar y la Web en la vista previa,
/// así que las dos dicen lo mismo. No consulta nada; recibe la disponibilidad ya contada.
/// </remarks>
public static class DifficultyPlanner
{
    public static readonly IReadOnlyList<DifficultyLevel> Levels =
        new[] { DifficultyLevel.Basic, DifficultyLevel.Intermediate, DifficultyLevel.Advanced };

    /// <exception cref="ValidationException">Los porcentajes no son válidos.</exception>
    public static DifficultyPlan Plan(
        int total,
        IReadOnlyDictionary<DifficultyLevel, int> percentages,
        IReadOnlyDictionary<DifficultyLevel, int> available)
    {
        Validate(percentages);

        var requested = LargestRemainder(total, percentages);

        var shortages = Levels
            .Select(l => new LevelShortage(l, requested[l], available.GetValueOrDefault(l)))
            .Where(s => s.Needed > s.Available)
            .ToList();

        return new DifficultyPlan(requested, shortages);
    }

    /// <summary>Tres niveles, cada uno de 0 a 100, que suman 100.</summary>
    public static void Validate(IReadOnlyDictionary<DifficultyLevel, int> percentages)
    {
        if (percentages.Count != Levels.Count || Levels.Any(l => !percentages.ContainsKey(l)))
            throw new ValidationException("El reparto debe indicar un porcentaje para cada nivel.");
        if (percentages.Values.Any(p => p is < 0 or > 100))
            throw new ValidationException("Cada porcentaje debe estar entre 0 y 100.");
        var sum = percentages.Values.Sum();
        if (sum != 100)
            throw new ValidationException($"Los porcentajes deben sumar 100 (suman {sum}).");
    }

    /// <summary>
    /// Método del mayor resto: la parte entera de cada cuota, y las preguntas que faltan, una a
    /// una, a los niveles con mayor parte decimal. Así la suma es siempre el total.
    /// </summary>
    private static Dictionary<DifficultyLevel, int> LargestRemainder(
        int total, IReadOnlyDictionary<DifficultyLevel, int> percentages)
    {
        // En enteros: la cuota es total·p/100, así que la parte entera es (total·p) / 100 y el
        // resto (total·p) % 100. Sin decimales no hay errores de redondeo en los empates.
        var result = Levels.ToDictionary(l => l, l => total * percentages[l] / 100);
        var pending = total - result.Values.Sum();

        var order = Levels
            .OrderByDescending(l => total * percentages[l] % 100)
            .ThenByDescending(l => percentages[l])
            .ThenBy(l => (int)l)          // empate: el nivel más fácil
            .ToList();

        for (var i = 0; i < pending; i++)
            result[order[i]]++;

        return result;
    }
}
