using Microsoft.JSInterop;
using TechEval.Domain.Enums;

namespace TechEval.Web.Services;

public class AuthStateService
{
    private readonly IJSRuntime _js;
    private readonly ApiService _api;

    public string? Token { get; private set; }
    public string? UserName { get; private set; }
    public string? Email { get; private set; }
    public UserRole? Role { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public bool IsAdmin => Role == UserRole.Admin;
    public bool IsEvaluador => Role == UserRole.Evaluador;
    public bool IsAlumno => Role == UserRole.Alumno;

    /// <summary>Página de inicio del rol de la sesión.</summary>
    public string Home => HomeFor(Role);

    public event Action? OnChange;

    public AuthStateService(IJSRuntime js, ApiService api)
    {
        _js = js;
        _api = api;
    }

    public static string HomeFor(UserRole? role) => role switch
    {
        UserRole.Admin => "/admin",
        UserRole.Evaluador => "/evaluacion",
        UserRole.Alumno => "/portal",
        _ => "/login"
    };

    public async Task InitializeAsync()
    {
        Token = await _js.InvokeAsync<string?>("localStorage.getItem", "auth_token");
        UserName = await _js.InvokeAsync<string?>("localStorage.getItem", "auth_user");
        Email = await _js.InvokeAsync<string?>("localStorage.getItem", "auth_email");
        var role = await _js.InvokeAsync<string?>("localStorage.getItem", "auth_role");
        Role = Enum.TryParse<UserRole>(role, out var parsed) ? parsed : null;

        // Un token guardado por la versión anterior no trae rol. La API ya no lo acepta,
        // porque no lleva sello de seguridad, así que se descarta en vez de adivinar el rol.
        if (!string.IsNullOrEmpty(Token) && Role is null)
        {
            await LogoutAsync();
            return;
        }

        if (!string.IsNullOrEmpty(Token))
            _api.SetAuthToken(Token);
    }

    public async Task LoginAsync(string token, string userName, UserRole role, string? email = null)
    {
        Token = token;
        UserName = userName;
        Email = email;
        Role = role;
        _api.SetAuthToken(token);
        await _js.InvokeVoidAsync("localStorage.setItem", "auth_token", token);
        await _js.InvokeVoidAsync("localStorage.setItem", "auth_user", userName);
        await _js.InvokeVoidAsync("localStorage.setItem", "auth_role", role.ToString());
        if (email is null) await _js.InvokeVoidAsync("localStorage.removeItem", "auth_email");
        else await _js.InvokeVoidAsync("localStorage.setItem", "auth_email", email);
        NotifyStateChanged();
    }

    public async Task LogoutAsync()
    {
        Token = null;
        UserName = null;
        Email = null;
        Role = null;
        _api.ClearAuthToken();
        await _js.InvokeVoidAsync("localStorage.removeItem", "auth_token");
        await _js.InvokeVoidAsync("localStorage.removeItem", "auth_user");
        await _js.InvokeVoidAsync("localStorage.removeItem", "auth_role");
        await _js.InvokeVoidAsync("localStorage.removeItem", "auth_email");
        // Clave de la versión anterior, que guardaba un booleano en vez del rol.
        await _js.InvokeVoidAsync("localStorage.removeItem", "auth_is_admin");
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
