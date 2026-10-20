using System.Linq.Expressions;

namespace TechEval.Infrastructure.Repositories;

/// <summary>Piezas comunes de las consultas paginadas.</summary>
internal static class PagedQuery
{
    /// <summary>Ordena por una clave, ascendente o descendente.</summary>
    public static IOrderedQueryable<T> By<T, TKey>(IQueryable<T> source, Expression<Func<T, TKey>> key, bool desc)
        => desc ? source.OrderByDescending(key) : source.OrderBy(key);

    /// <summary>Añade una segunda clave de orden, ascendente o descendente.</summary>
    public static IOrderedQueryable<T> ThenBy<T, TKey>(IOrderedQueryable<T> source, Expression<Func<T, TKey>> key, bool desc)
        => desc ? source.ThenByDescending(key) : source.ThenBy(key);

    /// <summary>
    /// El texto en minúsculas para compararlo con <c>ToLower().Contains(...)</c>, que EF traduce a
    /// <c>LOWER(campo) LIKE '%...%'</c> con los comodines escapados, y que en memoria se comporta igual.
    /// </summary>
    public static string Lower(string text) => text.ToLowerInvariant();
}
