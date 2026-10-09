namespace TechEval.Application;

/// <summary>
/// Ordena las cargas de un listado. Dos problemas de un listado que pide datos al servidor:
/// una respuesta lenta que llega después de una más reciente y la sustituye, y una búsqueda que
/// pide en cada pulsación. Vive en Aplicación y no en la Web para poder probarlo.
/// </summary>
public sealed class ListLoader : IDisposable
{
    private int _version;
    private CancellationTokenSource? _debounce;

    /// <summary>
    /// Pide los datos y los aplica solo si ninguna carga posterior empezó mientras tanto.
    /// Devuelve false si la respuesta se descartó por vieja.
    /// </summary>
    public async Task<bool> LoadAsync<T>(Func<Task<T>> fetch, Action<T> apply)
    {
        var mine = Interlocked.Increment(ref _version);
        var result = await fetch();
        if (mine != _version) return false;
        apply(result);
        return true;
    }

    /// <summary>
    /// Espera <paramref name="delayMs"/> y ejecuta la acción, salvo que otra llamada llegue antes:
    /// entonces la primera se cancela. Cinco pulsaciones seguidas hacen una sola petición.
    /// </summary>
    public async Task DebounceAsync(Func<Task> action, int delayMs = 300)
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
        var cts = _debounce = new CancellationTokenSource();

        try { await Task.Delay(delayMs, cts.Token); }
        catch (TaskCanceledException) { return; }

        await action();
    }

    public void Dispose()
    {
        // Una respuesta que llega después de salir de la pantalla no debe tocar un componente muerto.
        Interlocked.Increment(ref _version);
        _debounce?.Cancel();
        _debounce?.Dispose();
        _debounce = null;
    }
}
