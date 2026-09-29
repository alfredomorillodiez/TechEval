namespace TechEval.Web.Services;

/// <summary>
/// Aviso de que la API rechazó la sesión. Lo lanza <see cref="SessionExpiryHandler"/> y lo
/// atiende el layout, que es quien puede cerrar la sesión y navegar.
/// </summary>
/// <remarks>
/// Existe para separar las dos piezas: el manejador HTTP no conoce la navegación ni el
/// estado de la sesión, y así no hay dependencia circular entre ApiService y AuthStateService.
/// </remarks>
public class SessionEvents
{
    public event Func<Task>? SessionRejected;

    public Task RaiseSessionRejectedAsync()
        => SessionRejected?.Invoke() ?? Task.CompletedTask;
}
