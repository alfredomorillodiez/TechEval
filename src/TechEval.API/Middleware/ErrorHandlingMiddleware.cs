using System.Net;
using Microsoft.AspNetCore.Mvc;
using ApplicationException = TechEval.Application.ApplicationException;

namespace TechEval.API.Middleware;

/// <summary>
/// Único punto donde una excepción se convierte en código de estado HTTP.
/// </summary>
/// <remarks>
/// Mapea solo la jerarquía de excepciones de la capa de aplicación, que expresa intención.
/// Todo lo demás es un fallo que la aplicación no previó: sale como 500 con mensaje genérico
/// y su detalle se queda en el log.
///
/// Antes se traducía cualquier `InvalidOperationException` a 400 con su mensaje. Esa
/// excepción la lanzan también EF Core y media biblioteca del ecosistema, así que un fallo
/// de infraestructura llegaba al navegador del candidato como error suyo, y con el detalle
/// interno dentro.
/// </remarks>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApplicationException ex)
        {
            // Errores de negocio: previstos, y su mensaje está escrito para quien lo lee.
            _logger.LogInformation(
                "Operación rechazada en {Path}: {Message}", context.Request.Path, ex.Message);
            await WriteAsync(context, Map(ex), Title(ex), ex.Message);
        }
        catch (Exception ex)
        {
            // Todo lo demás: no previsto. El detalle se queda aquí.
            _logger.LogError(ex, "Excepción no controlada en {Path}", context.Request.Path);
            await WriteAsync(
                context,
                HttpStatusCode.InternalServerError,
                "Error interno",
                "Ha ocurrido un error interno. Contacte al administrador.");
        }
    }

    private static HttpStatusCode Map(ApplicationException ex) => ex switch
    {
        Application.ValidationException => HttpStatusCode.BadRequest,
        Application.NotFoundException => HttpStatusCode.NotFound,
        Application.ConflictException => HttpStatusCode.Conflict,
        Application.ForbiddenException => HttpStatusCode.Forbidden,

        // Una excepción de aplicación sin mapa es un descuido al añadirla, no un fallo del
        // cliente. Se trata como interna para que se note y se corrija.
        _ => HttpStatusCode.InternalServerError
    };

    private static string Title(ApplicationException ex) => ex switch
    {
        Application.ValidationException => "Petición no válida",
        Application.NotFoundException => "No encontrado",
        Application.ConflictException => "Conflicto con el estado actual",
        Application.ForbiddenException => "Operación no permitida",
        _ => "Error interno"
    };

    private static async Task WriteAsync(
        HttpContext context, HttpStatusCode status, string title, string detail)
    {
        if (context.Response.HasStarted) return;

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.Clear();
        context.Response.StatusCode = problem.Status.Value;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
