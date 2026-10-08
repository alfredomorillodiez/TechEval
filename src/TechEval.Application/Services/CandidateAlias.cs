namespace TechEval.Application.Services;

/// <summary>
/// Cómo ve el evaluador al candidato. Spec evaluator-review — Alias estable del candidato.
/// </summary>
/// <remarks>
/// Derivado del resultado y no del candidato: dos pruebas de la misma persona dan alias
/// distintos, y el evaluador no puede relacionarlas. El identificador del resultado ya está en
/// la ruta de la pantalla, así que el alias no revela nada que el evaluador no tenga.
/// </remarks>
public static class CandidateAlias
{
    public static string For(int resultId) => $"Candidato R-{resultId}";
}
