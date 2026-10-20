namespace TechEval.Web.Services;

/// <summary>
/// Estado de los listados (búsqueda, filtros y orden) mientras dure la sesión del navegador.
/// Antes cada pantalla lo guardaba en campos privados y lo perdía al navegar: quien revisaba 40
/// preguntas de una categoría tenía que volver a filtrar cada vez que abría una.
/// </summary>
/// <remarks>
/// Vive en memoria y nada más: no se guarda en disco ni viaja al servidor. Los textos de búsqueda
/// son nombres y correos de candidatos, y dejarlos en el almacén del navegador sería guardar
/// datos personales sin necesidad. Cerrar o abrir sesión lo borra, para que otra persona que
/// entre en el mismo navegador no herede los filtros de la anterior.
/// </remarks>
public class ListState
{
    private readonly Dictionary<string, object> _states = new();

    /// <summary>El estado guardado de una pantalla, o null si no hay (o es de otro tipo).</summary>
    public T? Get<T>(string key) where T : class
        => _states.TryGetValue(key, out var value) ? value as T : null;

    public void Set<T>(string key, T state) where T : class => _states[key] = state;

    public void Clear() => _states.Clear();
}
