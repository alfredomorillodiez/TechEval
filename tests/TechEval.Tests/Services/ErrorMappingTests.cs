using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TechEval.API.Middleware;
using TechEval.Application;
using TechEval.Application.Services;
using AppException = TechEval.Application.ApplicationException;

namespace TechEval.Tests.Services;

/// <summary>
/// Traducción de excepciones a HTTP. Antes cualquier `InvalidOperationException` salía como
/// 400 con su mensaje, así que un fallo de EF Core llegaba al cliente como error suyo y con
/// el detalle interno dentro.
/// </summary>
public class ErrorMappingTests
{
    private static async Task<(int Status, string Body)> EjecutarAsync(Exception aLanzar)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/algo";
        context.Response.Body = new MemoryStream();

        var middleware = new ErrorHandlingMiddleware(
            _ => throw aLanzar, NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, body);
    }

    [Theory]
    [InlineData(typeof(ValidationException), (int)HttpStatusCode.BadRequest)]
    [InlineData(typeof(NotFoundException), (int)HttpStatusCode.NotFound)]
    [InlineData(typeof(ConflictException), (int)HttpStatusCode.Conflict)]
    [InlineData(typeof(ForbiddenException), (int)HttpStatusCode.Forbidden)]
    public async Task CadaIntencion_ProduceSuCodigo(Type tipo, int esperado)
    {
        var ex = (AppException)Activator.CreateInstance(tipo, "un motivo legible")!;

        var (status, body) = await EjecutarAsync(ex);

        status.Should().Be(esperado);
        body.Should().Contain("un motivo legible", "el mensaje de negocio está escrito para quien lo lee");
    }

    [Theory]
    [InlineData(typeof(InvalidReviewException), (int)HttpStatusCode.BadRequest)]
    [InlineData(typeof(AlreadyReviewedException), (int)HttpStatusCode.Conflict)]
    [InlineData(typeof(AnswerInUseException), (int)HttpStatusCode.Conflict)]
    [InlineData(typeof(SessionAccessDeniedException), (int)HttpStatusCode.Forbidden)]
    [InlineData(typeof(ExamTimeExpiredException), (int)HttpStatusCode.Conflict)]
    public async Task LasEspecificas_HeredanElCodigoDeSuBase(Type tipo, int esperado)
    {
        var ex = (AppException)Activator.CreateInstance(tipo, "motivo")!;

        var (status, _) = await EjecutarAsync(ex);

        status.Should().Be(esperado);
    }

    [Fact]
    public async Task ExcepcionAjena_Produce500SinFiltrarSuMensaje()
    {
        // Este es el caso que motiva el cambio: antes salía como 400 con este texto.
        var interna = new InvalidOperationException(
            "The instance of entity type 'ExamResult' cannot be tracked — tabla dbo.ExamResults");

        var (status, body) = await EjecutarAsync(interna);

        status.Should().Be((int)HttpStatusCode.InternalServerError);
        body.Should().NotContain("ExamResult");
        body.Should().NotContain("dbo.");
        body.Should().Contain("error interno");
    }

    [Fact]
    public async Task ExcepcionDeBaseDeDatos_TampocoLlegaAlCliente()
    {
        var (status, body) = await EjecutarAsync(
            new TimeoutException("Timeout expired. Server=sqlserver;Database=TechEvalDb"));

        status.Should().Be((int)HttpStatusCode.InternalServerError);
        body.Should().NotContain("sqlserver");
        body.Should().NotContain("TechEvalDb");
    }

    [Fact]
    public async Task LaRespuestaTieneFormaDeProblemDetails()
    {
        var (_, body) = await EjecutarAsync(new NotFoundException("Examen no encontrado."));

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("title", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("status", out var status).Should().BeTrue();
        doc.RootElement.TryGetProperty("detail", out var detail).Should().BeTrue();

        status.GetInt32().Should().Be(404);
        detail.GetString().Should().Be("Examen no encontrado.");
    }

    [Fact]
    public async Task SinExcepcion_NoSeTocaLaRespuesta()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ErrorHandlingMiddleware(
            _ => Task.CompletedTask, NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(200);
    }
}
