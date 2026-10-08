using Microsoft.Extensions.Options;
using TechEval.Application.Services;

namespace TechEval.API;

/// <summary>
/// Marca como abandonadas las sesiones que nadie envió. Cada `Sessions:SweepMinutes` minutos
/// revisa las sesiones en curso y marca las que vencieron hace más de `Sessions:AbandonAfterHours`
/// horas. Con `AbandonAfterHours` a 0 no hace nada.
/// </summary>
/// <remarks>
/// Nunca tumba el servidor: un fallo de la base de datos se registra y la revisión se repite en el
/// siguiente intervalo. Marcar es idempotente, así que varias instancias de la API no se estorban.
/// </remarks>
public class AbandonedSessionWorker : BackgroundService
{
    /// <summary>Espera tras el arranque, para que la API acabe de sembrar y atender sus primeras peticiones.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly SessionSweepOptions _options;
    private readonly ILogger<AbandonedSessionWorker> _logger;

    public AbandonedSessionWorker(
        IServiceScopeFactory scopes, IOptions<SessionSweepOptions> options, ILogger<AbandonedSessionWorker> logger)
    {
        _scopes = scopes;
        _options = options.Value.Normalize();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.AbandonAfterHours == 0)
        {
            _logger.LogInformation("Sessions:AbandonAfterHours es 0: no se marcarán sesiones abandonadas.");
            return;
        }

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.SweepMinutes));
            do
            {
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Parada normal de la API.
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISessionMaintenanceService>();

            var marked = await service.MarkAbandonedAsync(
                DateTime.UtcNow, TimeSpan.FromHours(_options.AbandonAfterHours), ct);

            if (marked > 0)
                _logger.LogInformation("{Count} sesión(es) marcada(s) como abandonada(s).", marked);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falló la revisión de sesiones abandonadas; se repetirá en el siguiente intervalo.");
        }
    }
}
