using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TechEval.Web;
using TechEval.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000/";

// Singleton: el manejador y el layout tienen que compartir la misma instancia.
builder.Services.AddSingleton<SessionEvents>();
builder.Services.AddScoped(sp => new HttpClient(
    new SessionExpiryHandler(sp.GetRequiredService<SessionEvents>()) { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = new Uri(apiBaseUrl)
});
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<AuthStateService>();

builder.Logging.SetMinimumLevel(LogLevel.Warning);

await builder.Build().RunAsync();
