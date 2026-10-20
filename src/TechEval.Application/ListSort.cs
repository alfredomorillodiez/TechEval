using System.Globalization;

namespace TechEval.Application;

/// <summary>
/// Columna y dirección por las que está ordenado un listado. Pulsar la cabecera de la misma
/// columna invierte el orden; pulsar otra la pone ascendente.
/// </summary>
public sealed class SortState
{
    public string? Key { get; set; }
    public bool Descending { get; set; }

    public void Toggle(string key)
    {
        if (Key == key) Descending = !Descending;
        else { Key = key; Descending = false; }
    }

    /// <summary>Valor de `aria-sort` de una cabecera: ascending, descending o none.</summary>
    public string AriaSort(string key)
        => Key != key ? "none" : Descending ? "descending" : "ascending";

    public SortState Clone() => new() { Key = Key, Descending = Descending };
}

/// <summary>
/// Ordena un listado por la columna elegida. Pura y sin dependencias del navegador, para poder
/// probarla. Los listados cargan todo y ordenan en el cliente: no hay paginación en servidor.
/// </summary>
public static class ListSort
{
    private static readonly CompareInfo Spanish = BuildCompareInfo();

    private static CompareInfo BuildCompareInfo()
    {
        try { return new CultureInfo("es-ES").CompareInfo; }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture.CompareInfo; }
    }

    /// <summary>
    /// Sin clave, o con una clave que no es una columna, devuelve la lista tal cual. Los valores
    /// vacíos (nulos y cadenas vacías) van siempre al final, en las dos direcciones: ordenarlos al
    /// principio de una lista descendente enterraría los datos. El desempate es el orden original.
    /// </summary>
    public static IEnumerable<T> Apply<T>(
        IEnumerable<T> source, SortState state, IReadOnlyDictionary<string, Func<T, object?>> columns)
    {
        if (state.Key is null || !columns.TryGetValue(state.Key, out var selector))
            return source;

        // Primero «¿está vacío?» (los vacíos al final), después el valor en la dirección pedida.
        var ordered = source.OrderBy(item => IsEmpty(selector(item)));
        return state.Descending
            ? ordered.ThenByDescending(selector, ValueComparer.Instance)
            : ordered.ThenBy(selector, ValueComparer.Instance);
    }

    private static bool IsEmpty(object? value) => value is null || value is string { Length: 0 };

    private sealed class ValueComparer : IComparer<object?>
    {
        public static readonly ValueComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (x is null || y is null) return 0;   // los vacíos ya se separaron
            if (x is string a && y is string b)
                return Spanish.Compare(a, b, CompareOptions.IgnoreCase);
            return x is IComparable comparable ? comparable.CompareTo(y) : 0;
        }
    }
}
