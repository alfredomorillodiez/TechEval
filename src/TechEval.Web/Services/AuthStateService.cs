using Microsoft.JSInterop;

namespace TechEval.Web.Services;

public class AuthStateService
{
    private readonly IJSRuntime _js;
    private readonly ApiService _api;

    public string? Token { get; private set; }
    public string? UserName { get; private set; }
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
        if (!string.IsNullOrEmpty(Token))
            _api.SetAuthToken(Token);
    }

    public async Task LoginAsync(string token, string userName)
    {
        Token = token;
        UserName = userName;
        _api.SetAuthToken(token);
        await _js.InvokeVoidAsync("localStorage.setItem", "auth_token", token);
        await _js.InvokeVoidAsync("localStorage.setItem", "auth_user", userName);
        NotifyStateChanged();
    }

    public async Task LogoutAsync()
    {
        Token = null;
        UserName = null;
        _api.ClearAuthToken();
        await _js.InvokeVoidAsync("localStorage.removeItem", "auth_token");
        await _js.InvokeVoidAsync("localStorage.removeItem", "auth_user");
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
