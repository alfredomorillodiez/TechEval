using System.Linq.Expressions;

namespace TechEval.Domain.Interfaces.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);

    /// <summary>
    /// Una página de las filas que cumplen el filtro, y cuántas cumplen en total. Se cuenta antes
    /// de ordenar y de saltar. <paramref name="order"/> debe desempatar por una clave única,
    /// o dos páginas seguidas pueden repetir o perder filas.
    /// </summary>
    Task<(IReadOnlyList<T> Items, int Total)> GetPageAsync(
        Expression<Func<T, bool>>? filter,
        Func<IQueryable<T>, IOrderedQueryable<T>> order,
        int skip, int take, CancellationToken ct = default);
}
