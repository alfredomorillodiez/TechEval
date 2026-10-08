using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Application.Services;

/// <summary>
/// Cuánto tarda una sesión vencida en darse por abandonada y cada cuánto se revisa. Se enlaza a la
/// sección `Sessions` de la configuración.
/// </summary>
public class SessionSweepOptions
{
    public const int DefaultAbandonAfterHours = 24;
    public const int DefaultSweepMinutes = 15;

    /// <summary>Horas, después del plazo, tras las que una sesión sin envío se marca abandonada. 0 desactiva el trabajo.</summary>
    public int AbandonAfterHours { get; set; } = DefaultAbandonAfterHours;

    /// <summary>Minutos entre dos revisiones.</summary>
    public int SweepMinutes { get; set; } = DefaultSweepMinutes;

    /// <summary>
    /// Sustituye los valores que no valen por los de por defecto, para que un error de configuración
    /// no impida arrancar. El 0 de `AbandonAfterHours` es válido: desactiva el trabajo.
    /// </summary>
    public SessionSweepOptions Normalize()
    {
        if (AbandonAfterHours < 0) AbandonAfterHours = DefaultAbandonAfterHours;
        if (SweepMinutes < 1) SweepMinutes = DefaultSweepMinutes;
        return this;
    }
}

public interface ISessionMaintenanceService
{
    /// <summary>
    /// Marca como abandonadas las sesiones en curso, sin resultado, cuyo plazo venció hace más de
    /// `grace`. Devuelve cuántas marcó.
    /// </summary>
    Task<int> MarkAbandonedAsync(DateTime nowUtc, TimeSpan grace, CancellationToken ct = default);
}

/// <summary>
/// Una sesión que nadie envió se queda `InProgress` para siempre: figura como «en curso» en el
/// portal del alumno y cuenta como empezada. Este servicio la marca `Abandoned`, conservando sus
/// respuestas guardadas y sin generar resultado.
/// </summary>
public class SessionMaintenanceService : ISessionMaintenanceService
{
    private readonly IExamTokenRepository _tokenRepo;
    private readonly IRepository<ExamSession> _sessionRepo;

    public SessionMaintenanceService(IExamTokenRepository tokenRepo, IRepository<ExamSession> sessionRepo)
    {
        _tokenRepo = tokenRepo;
        _sessionRepo = sessionRepo;
    }

    public async Task<int> MarkAbandonedAsync(DateTime nowUtc, TimeSpan grace, CancellationToken ct = default)
    {
        var abandoned = 0;

        foreach (var token in await _tokenRepo.GetInProgressAsync(ct))
        {
            ct.ThrowIfCancellationRequested();

            var session = token.ExamSession;
            if (session is null || session.Status != SessionStatus.InProgress) continue;

            // Un envío interrumpido entre el resultado y el cierre deja resultado y InProgress: esa
            // sesión está enviada, no abandonada.
            if (session.ExamResult is not null) continue;

            // El tiempo guardado al empezar, no el actual de la prueba: editarla no cambia el plazo.
            var deadline = session.StartedAt.AddMinutes(session.TimeLimitFor(token.Exam));
            if (nowUtc <= deadline + grace) continue;

            // Cada sesión por separado: el fallo de una no impide marcar las demás.
            try
            {
                session.Status = SessionStatus.Abandoned;
                await _sessionRepo.UpdateAsync(session, ct);
                abandoned++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                session.Status = SessionStatus.InProgress;
            }
        }

        return abandoned;
    }
}
