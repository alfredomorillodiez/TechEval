using TechEval.Domain.Common;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;

namespace TechEval.Domain.Interfaces.Repositories;

public interface IQuestionRepository : IRepository<Question>
{
    Task<Question?> GetWithAnswersAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Question>> GetByCategoryAsync(int categoryId, CancellationToken ct = default);
    /// <summary>
    /// Una página del banco, con la categoría y sin las opciones: la tabla no las muestra.
    /// </summary>
    Task<PagedResult<Question>> GetPagedAsync(
        QuestionFilter filter, PageQuery query, CancellationToken ct = default);

    Task<IReadOnlyList<Question>> GetRandomAsync(
        int count,
        List<int>? categoryIds,
        DifficultyLevel? difficulty,
        CancellationToken ct = default);

    /// <summary>
    /// Preguntas activas de cada nivel en esas categorías (en todas, sin categorías). Los tres
    /// niveles siempre, también los que tienen cero.
    /// </summary>
    Task<IReadOnlyDictionary<DifficultyLevel, int>> CountByDifficultyAsync(
        List<int>? categoryIds, CancellationToken ct = default);

    /// <summary>
    /// Identificadores de las opciones de esta pregunta que algún candidato ya eligió.
    /// Borrarlas rompería la clave foránea de `UserAnswer.SelectedAnswerId` y, con ella,
    /// el registro de lo que ese candidato respondió.
    /// </summary>
    Task<IReadOnlyList<int>> GetReferencedAnswerIdsAsync(int questionId, CancellationToken ct = default);
}
