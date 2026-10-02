using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using TechEval.Domain.Interfaces.Services;
using TechEval.Infrastructure;
using TechEval.Infrastructure.Email;

namespace TechEval.Tests.Services;

/// <summary>
/// Envío por Microsoft Graph, como `EmailService365` de iECS: el remitente es el buzón que
/// publica, y no viaja contraseña de buzón.
/// </summary>
public class GraphEmailServiceTests
{
    private const string Buzon = "techeval@pronet-ise.com";

    private readonly CapturingHandler _graph = new();
    private readonly GraphEmailService _sut;

    public GraphEmailServiceTests()
    {
        var tokens = new Mock<IGraphAccessTokenProvider>();
        tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token-de-prueba");

        _sut = new GraphEmailService(
            new HttpClient(_graph), tokens.Object,
            Options.Create(new EmailSettings { FromEmail = Buzon }),
            NullLogger<GraphEmailService>.Instance);
    }

    [Fact]
    public async Task Publica_en_el_buzon_del_remitente_con_el_token()
    {
        await _sut.SendExamPendingReviewAsync("ana@example.test", "Ana", "C# básico");

        _graph.Request!.Method.Should().Be(HttpMethod.Post);
        _graph.Request.RequestUri!.ToString().Should()
            .Be("https://graph.microsoft.com/v1.0/users/techeval%40pronet-ise.com/sendMail");
        _graph.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        _graph.Request.Headers.Authorization.Parameter.Should().Be("token-de-prueba");
    }

    [Fact]
    public async Task El_mensaje_lleva_asunto_destinatario_y_el_HTML_sin_tocar()
    {
        await _sut.SendExamPendingReviewAsync("ana@example.test", "Ana", "C# básico");

        var message = JsonDocument.Parse(_graph.Body!).RootElement.GetProperty("message");
        message.GetProperty("subject").GetString().Should().Be("Hemos recibido tu prueba: C# básico");
        message.GetProperty("body").GetProperty("contentType").GetString().Should().Be("HTML");

        // EmailService365 cambia los saltos de línea por <br>. Aquí no: el cuerpo ya es HTML.
        var html = message.GetProperty("body").GetProperty("content").GetString();
        html.Should().StartWith("<!DOCTYPE html>").And.NotContain("<br>");

        var to = message.GetProperty("toRecipients")[0].GetProperty("emailAddress");
        to.GetProperty("address").GetString().Should().Be("ana@example.test");
        to.GetProperty("name").GetString().Should().Be("Ana");
    }

    [Fact]
    public async Task Si_Graph_rechaza_el_envio_lanza_con_su_explicacion()
    {
        _graph.Response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("""{"error":{"code":"ErrorAccessDenied","message":"Access is denied."}}""")
        };

        var act = () => _sut.SendExamPendingReviewAsync("ana@example.test", "Ana", "C# básico");

        (await act.Should().ThrowAsync<HttpRequestException>())
            .Which.Message.Should().Contain("403").And.Contain("ErrorAccessDenied");
    }

    [Fact]
    public void Sin_credenciales_la_API_resuelve_el_servicio_igual()
    {
        // En desarrollo nadie tiene por qué tener la aplicación de Entra ID. Si construir el
        // servicio fallase, fallaría con él cada operación que envía correo, no solo el envío.
        var configuration = new ConfigurationBuilder().Build();

        using var services = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
        using var scope = services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IEmailService>().Should().BeOfType<GraphEmailService>();
    }

    [Fact]
    public async Task Sin_credenciales_el_envio_falla_y_no_llega_a_Graph()
    {
        var sut = new GraphEmailService(
            new HttpClient(_graph),
            new MsalGraphAccessTokenProvider(Options.Create(new EmailSettings())),
            Options.Create(new EmailSettings { FromEmail = Buzon }),
            NullLogger<GraphEmailService>.Instance);

        var act = () => sut.SendExamPendingReviewAsync("ana@example.test", "Ana", "C# básico");

        await act.Should().ThrowAsync<Exception>();
        _graph.Request.Should().BeNull();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.Accepted);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Response;
        }
    }
}
