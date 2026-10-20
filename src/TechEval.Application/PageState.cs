using TechEval.Domain.Common;

namespace TechEval.Application;

/// <summary>
/// La página y el tamaño de página que muestra un listado, y el total de filas que cumplen el filtro.
/// Cada pantalla tiene uno. Vive en Aplicación y no en la Web para poder probarlo.
/// </summary>
public sealed class PageState
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = PageQuery.DefaultPageSize;
    public int Total { get; private set; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    /// <summary>Primera fila de la página, empezando en 1. 0 si no hay filas.</summary>
    public int FirstRow => Total == 0 ? 0 : (Page - 1) * PageSize + 1;

    /// <summary>Última fila de la página.</summary>
    public int LastRow => Math.Min(Total, Page * PageSize);

    /// <summary>Un cambio de búsqueda, de filtro o de orden vuelve a la primera página.</summary>
    public void Reset() => Page = 1;

    /// <summary>Cambiar el tamaño de página vuelve a la primera: la página 3 de 50 no es la 3 de 25.</summary>
    public void SetPageSize(int size)
    {
        PageSize = size;
        Page = 1;
    }

    /// <summary>Guarda lo que devolvió el servidor.</summary>
    public void Apply<T>(PagedResult<T> result)
    {
        Total = result.Total;
        Page = result.Page;
        PageSize = result.PageSize;
    }

    /// <summary>
    /// True si la página pedida ya no existe porque hay menos filas (se guardó la 5 y ahora hay
    /// 3 páginas). Con eso la pantalla pide la última, una sola vez, y no enseña una tabla vacía.
    /// </summary>
    public bool IsBeyondEnd<T>(PagedResult<T> result)
        => result.Items.Count == 0 && result.Total > 0 && result.Page > result.TotalPages;

    /// <summary>Va a la última página existente.</summary>
    public void GoToLast() => Page = Math.Max(1, TotalPages);

    /// <summary>La petición de esta página con el texto y el orden dados.</summary>
    public PageQuery ToQuery(string? text, SortState sort) => new()
    {
        Page = Page,
        PageSize = PageSize,
        Q = text,
        Sort = sort.Key,
        Desc = sort.Descending
    };
}
