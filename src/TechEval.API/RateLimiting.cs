using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace TechEval.API;

/// <summary>
/// Límite de ritmo de los dos endpoints que un desconocido puede llamar sin credencial
/// previa (hallazgo S5).
///
/// El motivo no es la adivinanza de contraseñas: eso lo cerraron S2 y S3. Es el coste.
/// Desde que el hash es PBKDF2 con 600 000 iteraciones, verificar una contraseña cuesta
/// cientos de milisegundos de CPU. Unas pocas peticiones simultáneas ocupan todos los
/// hilos y el resto de la aplicación deja de responder, sin acertar ni una.
/// </summary>
public static class RateLimiting
{
    /// <summary>Inicio de sesión: caro de servir y de uso legítimo escaso.</summary>
    public const string LoginPolicy = "login";

    /// <summary>Validación del enlace de examen: barata y de uso legítimo frecuente.</summary>
    public const string ExamLinkPolicy = "exam-link";

    public const string ProblemContentType = "application/problem+json";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private const int LoginPermitsPerWindow = 10;

    // Más ancho a propósito: el candidato lo llama al abrir el enlace, al recargar y al
    // volver a su prueba. Un cupo estrecho aquí echa de su examen a quien no ha hecho nada.
    private const int ExamLinkPermitsPerWindow = 60;

    public static IServiceCollection AddTechEvalRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(LoginPolicy, http => FixedWindowFor(http, LoginPermitsPerWindow));
            options.AddPolicy(ExamLinkPolicy, http => FixedWindowFor(http, ExamLinkPermitsPerWindow));

            options.OnRejected = async (context, ct) =>
            {
                // Si la ventana es fija, el limitador sabe cuánto falta. Decirlo evita que
                // el cliente reintente en bucle y empeore justo lo que se quiere evitar.
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after)
                    ? after
                    : Window;

                var problem = WriteRejection(context.HttpContext, retryAfter);

                // El tipo va aquí explícitamente: `WriteAsJsonAsync` sin él pisa el que fijó
                // `WriteRejection` y devuelve `application/json`.
                await context.HttpContext.Response.WriteAsJsonAsync(
                    problem, options: null, contentType: ProblemContentType, cancellationToken: ct);
            };
        });

        return services;
    }

    /// <summary>
    /// Deja la respuesta lista para el rechazo y devuelve el cuerpo. El rechazo ocurre antes
    /// de llegar a <c>ErrorHandlingMiddleware</c>, así que la forma se escribe aquí; debe ser
    /// la misma <c>ProblemDetails</c> que el resto de errores de la API.
    /// </summary>
    public static ProblemDetails WriteRejection(HttpContext http, TimeSpan retryAfter)
    {
        http.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        http.Response.ContentType = ProblemContentType;
        http.Response.Headers.RetryAfter =
            ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);

        return new ProblemDetails
        {
            Status = (int)HttpStatusCode.TooManyRequests,
            Title = "Demasiadas peticiones",
            // Sin decir si la contraseña era correcta: el rechazo llega antes de mirarla.
            Detail = "Has hecho demasiadas peticiones seguidas. Espera unos segundos y vuelve a intentarlo.",
            Instance = http.Request.Path
        };
    }

    private static RateLimitPartition<string> FixedWindowFor(HttpContext http, int permits)
        => RateLimitPartition.GetFixedWindowLimiter(
            OriginOf(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = Window,
                QueueLimit = 0   // Encolar mantendría ocupado justo lo que se quiere liberar.
            });

    /// <summary>
    /// Dirección de origen, o una clave común si no se puede determinar. Agrupar lo
    /// desconocido bajo una sola clave es deliberado: sin ella, cada petición sin dirección
    /// tendría su propio cupo y el límite no existiría para quien sepa provocarlo.
    /// </summary>
    public static string OriginOf(HttpContext http)
        => http.Connection.RemoteIpAddress?.ToString() ?? "desconocido";
}
