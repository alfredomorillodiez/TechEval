using TechEval.Domain.Entities;
using TechEval.Domain.Enums;

namespace TechEval.Domain.Interfaces.Repositories;

public interface IQuestionRepository : IRepository<Question>
{
    Task<Question?> GetWithAnswersAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Question>> GetByCategoryAsync(int categoryId, CancellationToken ct = default);
    Task<IReadOnlyList<Question>> GetFilteredAsync(
        int? categoryId,
        DifficultyLevel? difficulty,
        QuestionType? type,
        bool onlyActive = true,
        CancellationToken ct = default);
    Task<IReadOnlyList<Question>> GetRandomAsync(
        int count,
        List<int>? categoryIds,
        DifficultyLevel? difficulty,
        CancellationToken ct = default);

    /// <summary>
    /// Identificadores de las opciones de esta pregunta que algún candidato ya eligió.
    /// Borrarlas rompería la clave foránea de `UserAnswer.SelectedAnswerId` y, con ella,
    /// el registro de lo que ese candidato respondió.
    /// </summary>
    Task<IReadOnlyList<int>> GetReferencedAnswerIdsAsync(int questionId, CancellationToken ct = default);
}
