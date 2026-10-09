namespace TechEval.Domain.Common;

/// <summary>
/// Lo que pide un listado paginado: página, tamaño, texto, orden. Es una clase con propiedades
/// y no un <c>record</c> posicional porque el enlazador de <c>[FromQuery]</c> rellena propiedades.
/// Un valor que no es un número (<c>page=abc</c>) lo rechaza el enlazador con un 400. Uno que sí lo
/// es pero está fuera de rango (<c>page=0</c>, <c>pageSize=5000</c>) se corrige: quien consulta lee
/// siempre las propiedades <c>Effective*</c>.
/// </summary>
public sealed class PageQuery
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Texto de búsqueda.</summary>
    public string? Q { get; set; }

    /// <summary>Clave de la columna de orden. Una clave desconocida cae en el orden por defecto.</summary>
    public string? Sort { get; set; }

    public bool Desc { get; set; }

    public int EffectivePage => Math.Max(1, Page);

    public int EffectivePageSize => PageSize < 1 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize);

    public int Skip => (EffectivePage - 1) * EffectivePageSize;

    /// <summary>El texto sin espacios de los extremos, o nulo si está en blanco.</summary>
    public string? Text => string.IsNullOrWhiteSpace(Q) ? null : Q.Trim();

    /// <summary>La clave de orden en minúsculas, o nula.</summary>
    public string? SortKey => string.IsNullOrWhiteSpace(Sort) ? null : Sort.Trim().ToLowerInvariant();
}

/// <summary>Una página de un listado, con el total de filas que cumplen el filtro.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map)
        => new(Items.Select(map).ToList(), Total, Page, PageSize);

    public static PagedResult<T> Of(IReadOnlyList<T> items, int total, PageQuery query)
        => new(items, total, query.EffectivePage, query.EffectivePageSize);
}
