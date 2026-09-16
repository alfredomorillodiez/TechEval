using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using TechEval.API;

namespace TechEval.Tests.Services;

/// <summary>
/// Forma del rechazo por ritmo (S5).
///
/// Lo que estas pruebas cubren es la respuesta, no el reparto de cupos: el limitador de la
/// plataforma no se prueba aquí, se comprueba contra la API real. Lo que sí es código
/// propio, y puede romperse sin que nadie se entere, es que el rechazo tenga la misma forma
/// que el resto de errores y que diga cuándo reintentar.
/// </summary>
public class RateLimitRejectionTests
{
    private static DefaultHttpContext Context(string path = "/api/Auth/login")
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Response.Body = new MemoryStream();
        return http;
    }

    [Fact]
    public void Rechazo_Responde429ConFormaDeProblemDetails()
    {
        var http = Context();

        var problem = RateLimiting.WriteRejection(http, TimeSpan.FromSeconds(60));

        http.Response.StatusCode.Should().Be((int)HttpStatusCode.TooManyRequests);
        http.Response.ContentType.Should().Be("application/problem+json");
        problem.Status.Should().Be(429);
        problem.Title.Should().NotBeNullOrWhiteSpace();
        problem.Detail.Should().NotBeNullOrWhiteSpace();
        problem.Instance.Should().Be("/api/Auth/login");
    }

    [Fact]
    public void Rechazo_DiceCuandoSePuedeReintentar()
    {
        var http = Context();

        RateLimiting.WriteRejection(http, TimeSpan.FromSeconds(45));

        http.Response.Headers.RetryAfter.ToString().Should().Be("45");
    }

    [Fact]
    public void Rechazo_NoRevelaSiLaContrasenaEraCorrecta()
    {
        var http = Context();

        var problem = RateLimiting.WriteRejection(http, TimeSpan.FromSeconds(60));

        // El rechazo ocurre antes de mirar la contraseña, así que no puede saberlo. Si algún
        // día el mensaje lo insinuara, el límite se convertiría en un oráculo de cuentas.
        problem.Detail.Should().NotContainAny("contraseña", "credencial", "usuario", "existe");
    }

    [Fact]
    public void Origen_SinDireccionConocida_CaeEnUnaClaveComun()
    {
        // Sin esto, cada petición sin dirección tendría su propio cupo y el límite dejaría
        // de existir para quien supiera provocar esa situación.
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = null;

        RateLimiting.OriginOf(http).Should().Be("desconocido");
    }

    [Fact]
    public void Origen_ConDireccion_UsaLaDireccion()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        RateLimiting.OriginOf(http).Should().Be("203.0.113.7");
    }

    [Fact]
    public void Origen_DosDireccionesDistintas_NoCompartenCupo()
    {
        var uno = new DefaultHttpContext();
        uno.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        var otro = new DefaultHttpContext();
        otro.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.8");

        RateLimiting.OriginOf(uno).Should().NotBe(RateLimiting.OriginOf(otro));
    }
}
