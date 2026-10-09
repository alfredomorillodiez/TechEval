using TechEval.Domain.Common;
using TechEval.Domain.Entities;

namespace TechEval.Domain.Interfaces.Repositories;

public interface IExamRepository : IRepository<Exam>
{
    Task<Exam?> GetWithQuestionsAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Una página del listado de pruebas. Los recuentos (preguntas, puntos, invitaciones,
    /// resultados, abandonadas) se calculan en la consulta: no se carga el árbol de cada prueba.
    /// </summary>
    Task<PagedResult<ExamSummaryRow>> GetPagedSummariesAsync(
        ExamFilter filter, PageQuery query, CancellationToken ct = default);

    /// <summary>Todas las pruebas, solo con identificador y título, ordenadas por título.</summary>
    Task<IReadOnlyList<ExamOption>> GetOptionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Cuántos exámenes están activos. Sin sus preguntas, invitaciones, sesiones ni
    /// resultados: el dashboard solo necesita el número, y esas inclusiones se resuelven
    /// con uniones que multiplican filas.
    /// </summary>
    Task<int> CountActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Pruebas activas con alguna pregunta abierta y ningún evaluador asignado: sus resultados
    /// solo los puede corregir el administrador. Solo Id y Title vienen rellenos.
    /// </summary>
    Task<IReadOnlyList<Exam>> GetActiveWithoutEvaluatorAsync(CancellationToken ct = default);
}
