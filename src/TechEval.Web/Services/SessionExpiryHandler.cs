using System.Net;

namespace TechEval.Web.Services;

/// <summary>
/// Detecta el 401 de la API en cualquier petición. Desde que el token lleva sello de
/// seguridad, un 401 con sesión abierta significa que la cuenta cambió: la desactivaron, le
/// cambiaron el rol o le restablecieron el acceso. Seguir en la página solo produce errores.
/// </summary>
public class SessionExpiryHandler : DelegatingHandler
{
    // El 401 del login significa credenciales incorrectas, no una sesión rechazada.
    private const string LoginPath = "api/auth/login";

    private readonly SessionEvents _events;

    public SessionExpiryHandler(SessionEvents events) => _events = events;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        var isLogin = request.RequestUri?.AbsolutePath
            .EndsWith(LoginPath, StringComparison.OrdinalIgnoreCase) ?? false;

        if (response.StatusCode == HttpStatusCode.Unauthorized
            && request.Headers.Authorization is not null
            && !isLogin)
            await _events.RaiseSessionRejectedAsync();

        return response;
    }
}
