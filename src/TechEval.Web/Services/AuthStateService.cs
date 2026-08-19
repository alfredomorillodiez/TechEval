using Microsoft.JSInterop;

namespace TechEval.Web.Services;

public class AuthStateService
{
    private readonly IJSRuntime _js;
    private readonly ApiService _api;

    public string? Token { get; private set; }
    public string? UserName { get; private set; }
    public bool IsAdmin { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public event Action? OnChange;

    public AuthStateService(IJSRuntime js, ApiService api)
    {
        _js = js;
        _api = api;
    }

    public async Task InitializeAsync()
    {
        Token = await _js.InvokeAsync<string?>("localStorage.getItem", "auth_token");
        UserName = await _js.InvokeAsync<string?>("localStorage.getItem", "auth_user");
        IsAdmin = await _js.InvokeAsync<string?>("localStorage.getItem", "auth_is_admin") == "true";
        if (!string.IsNullOrEmpty(Token))
            _api.SetAuthToken(Token);
    }

    public async Task LoginAsync(string token, string userName, bool isAdmin)
    {
        Token = token;
        UserName = userName;
        IsAdmin = isAdmin;
        _api.SetAuthToken(token);
        await _js.InvokeVoidAsync("localStorage.setItem", "auth_token", token);
        await _js.InvokeVoidAsync("localStorage.setItem", "auth_user", userName);
        await _js.InvokeVoidAsync("localStorage.setItem", "auth_is_admin", isAdmin ? "true" : "false");
        NotifyStateChanged();
    }

    public async Task LogoutAsync()
    {
        Token = null;
        UserName = null;
        IsAdmin = false;
        _api.ClearAuthToken();
        await _js.InvokeVoidAsync("localStorage.removeItem", "auth_token");
        await _js.InvokeVoidAsync("localStorage.removeItem", "auth_user");
        await _js.InvokeVoidAsync("localStorage.removeItem", "auth_is_admin");
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
