using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using TechEval.Domain.Interfaces.Services;
using TechEval.Infrastructure.Email;

namespace TechEval.Tests.Services;

/// <summary>
/// La fecha de expiración de los correos va en hora local con su zona. Antes decía «12:30 UTC» y
/// el candidato la leía en su reloj local.
/// </summary>
public class EmailDateFormatTests
{
    private static string Madrid(DateTime utc)
        => EmailDateFormat.Format(utc, EmailDateFormat.Resolve("Europe/Madrid", out _), "Europe/Madrid");

    [Fact]
    public void HorarioDeVerano_MasDosHoras()
        => Madrid(new DateTime(2026, 10, 9, 10, 30, 0, DateTimeKind.Utc))
            .Should().Be("09/10/2026 12:30 (hora de Madrid, UTC+02:00)");

    [Fact]
    public void HorarioDeInvierno_MasUnaHora()
        => Madrid(new DateTime(2026, 12, 9, 10, 30, 0, DateTimeKind.Utc))
            .Should().Be("09/12/2026 11:30 (hora de Madrid, UTC+01:00)");

    [Fact]
    public void LaEntradaSinKindSeTrataComoUtc()
        => Madrid(new DateTime(2026, 12, 9, 10, 30, 0, DateTimeKind.Unspecified))
            .Should().Be("09/12/2026 11:30 (hora de Madrid, UTC+01:00)");

    [Fact]
    public void CambiaElDiaSiLaZonaCruzaLaMedianoche()
        => Madrid(new DateTime(2026, 12, 9, 23, 30, 0, DateTimeKind.Utc))
            .Should().StartWith("10/12/2026 00:30");

    [Fact]
    public void ZonaConDesfaseNegativo()
    {
        var zone = EmailDateFormat.Resolve("America/New_York", out var found);
        found.Should().BeTrue();

        EmailDateFormat.Format(new DateTime(2026, 12, 9, 15, 0, 0, DateTimeKind.Utc), zone, "America/New_York")
            .Should().Be("09/12/2026 10:00 (hora de New York, UTC-05:00)");
    }

    [Fact]
    public void ZonaDesconocida_CaeAUtcYLoDice()
    {
        var zone = EmailDateFormat.Resolve("Marte/Olympus", out var found);

        found.Should().BeFalse();
        EmailDateFormat.Format(new DateTime(2026, 10, 9, 10, 30, 0, DateTimeKind.Utc), zone)
            .Should().Be("09/10/2026 10:30 (UTC)");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SinZonaConfigurada_UsaMadrid(string? id)
    {
        EmailDateFormat.Resolve(id, out var found).Id.Should().Contain("Madrid");
        found.Should().BeTrue();
    }

    [Fact]
    public async Task LaInvitacionYElEnlaceDeContrasenaLlevanLaHoraLocal()
    {
        var capturas = new List<string>();
        var handler = new DelegatingStub(async r => capturas.Add(await r.Content!.ReadAsStringAsync()));
        var tokens = new Mock<IGraphAccessTokenProvider>();
        tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("t");
        var sut = new GraphEmailService(
            new HttpClient(handler), tokens.Object,
            Options.Create(new EmailSettings { FromEmail = "b@x.com", TimeZoneId = "Europe/Madrid" }),
            NullLogger<GraphEmailService>.Instance);
        var expira = new DateTime(2026, 10, 9, 10, 30, 0, DateTimeKind.Utc);

        await sut.SendExamInvitationAsync("a@x.com", "Ana", "Prueba", "http://x/exam/t", expira);
        await sut.SendPasswordSetupAsync("a@x.com", "Ana", "http://x/fijar/t", expira);

        foreach (var cuerpo in capturas)
        {
            var html = JsonDocument.Parse(cuerpo).RootElement.GetProperty("message")
                .GetProperty("body").GetProperty("content").GetString()!;
            html.Should().Contain("09/10/2026 12:30 (hora de Madrid, UTC+02:00)");
            html.Should().NotContain(" UTC y solo", "ya no se dice «UTC» como si fuera la hora local");
        }
        capturas.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConUnaZonaDesconocida_ElCorreoSaleEnUtcSinFallar()
    {
        var capturas = new List<string>();
        var handler = new DelegatingStub(async r => capturas.Add(await r.Content!.ReadAsStringAsync()));
        var tokens = new Mock<IGraphAccessTokenProvider>();
        tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("t");
        var sut = new GraphEmailService(
            new HttpClient(handler), tokens.Object,
            Options.Create(new EmailSettings { FromEmail = "b@x.com", TimeZoneId = "Marte/Olympus" }),
            NullLogger<GraphEmailService>.Instance);

        await sut.SendExamInvitationAsync("a@x.com", "Ana", "Prueba", "http://x/exam/t",
            new DateTime(2026, 10, 9, 10, 30, 0, DateTimeKind.Utc));

        capturas.Single().Should().Contain("09/10/2026 10:30 (UTC)");
    }

    private sealed class DelegatingStub : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task> _capture;
        public DelegatingStub(Func<HttpRequestMessage, Task> capture) => _capture = capture;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _capture(request);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
