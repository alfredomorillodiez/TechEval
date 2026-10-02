using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Infrastructure.Email;

/// <summary>
/// Envío por Microsoft Graph, el mismo mecanismo que `EmailService365` de iECS.
/// </summary>
/// <remarks>
/// Sustituye a SMTP. Allí la cuenta que se autenticaba y el `From` podían no coincidir, y
/// Exchange lo trataba como suplantación. Aquí el remitente es, por construcción, el buzón en
/// cuyo nombre se publica el mensaje. Tampoco viaja ninguna contraseña de buzón: la
/// aplicación se identifica ante Entra ID y Graph valida el permiso `Mail.Send`.
/// </remarks>
public class GraphEmailService : IEmailService
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

    private readonly HttpClient _http;
    private readonly IGraphAccessTokenProvider _tokens;
    private readonly EmailSettings _settings;
    private readonly ILogger<GraphEmailService> _logger;

    public GraphEmailService(
        HttpClient http, IGraphAccessTokenProvider tokens,
        IOptions<EmailSettings> settings, ILogger<GraphEmailService> logger)
    {
        _http = http;
        _tokens = tokens;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendExamInvitationAsync(
        string toEmail, string toName, string examTitle,
        string examLink, DateTime expiresAt, CancellationToken ct = default)
    {
        var subject = $"Invitación a una prueba técnica: {examTitle}";
        var body = BuildInvitationHtml(toName, examTitle, examLink, expiresAt);
        await SendAsync(toEmail, toName, subject, body, ct);
    }

    public async Task SendExamResultAsync(
        string toEmail, string toName, string examTitle,
        decimal scorePercentage, bool passed, CancellationToken ct = default)
    {
        var subject = $"Resultado de tu prueba: {examTitle}";
        var body = BuildResultHtml(toName, examTitle, scorePercentage, passed);
        await SendAsync(toEmail, toName, subject, body, ct);
    }

    public async Task SendExamPendingReviewAsync(
        string toEmail, string toName, string examTitle, CancellationToken ct = default)
    {
        var subject = $"Hemos recibido tu prueba: {examTitle}";
        var body = BuildPendingReviewHtml(toName, examTitle);
        await SendAsync(toEmail, toName, subject, body, ct);
    }

    public async Task SendPasswordSetupAsync(
        string toEmail, string toName, string setupLink, DateTime expiresAt, CancellationToken ct = default)
    {
        const string subject = "Fija tu contraseña de TechEval";
        var body = BuildPasswordSetupHtml(toName, setupLink, expiresAt);
        await SendAsync(toEmail, toName, subject, body, ct);
    }

    private async Task SendAsync(
        string toEmail, string toName, string subject, string body, CancellationToken ct)
    {
        try
        {
            await PostSendMailAsync(toEmail, toName, subject, body, ct);
            _logger.LogInformation("Email enviado a {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando email a {Email}", toEmail);
            throw;
        }
    }

    private async Task PostSendMailAsync(
        string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
    {
        var accessToken = await _tokens.GetAccessTokenAsync(ct);

        var payload = new
        {
            message = new
            {
                subject,
                body = new { contentType = "HTML", content = htmlBody },
                toRecipients = new[] { new { emailAddress = new { address = toEmail, name = toName } } }
            },
            saveToSentItems = true
        };

        // La cabecera va en cada petición y no en el HttpClient: el cliente lo comparten
        // envíos simultáneos, y el token caduca.
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{GraphBaseUrl}/users/{Uri.EscapeDataString(_settings.FromEmail)}/sendMail")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return;

        // Graph explica el rechazo en el cuerpo (buzón inexistente, permiso sin conceder...).
        // No lleva secretos, y sin él el error del log no dice nada útil.
        var detail = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"Microsoft Graph rechazó el envío ({(int)response.StatusCode} {response.StatusCode}): {detail}",
            null, response.StatusCode);
    }

    private static string BuildInvitationHtml(
        string name, string examTitle, string link, DateTime expiresAt) => $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
            <div style="background: #1e40af; color: white; padding: 20px; border-radius: 8px 8px 0 0;">
                <h1 style="margin: 0;">TechEval Platform</h1>
            </div>
            <div style="background: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px;">
                <h2>Hola, {name}</h2>
                <p>Has sido invitado a completar la siguiente prueba técnica:</p>
                <h3 style="color: #1e40af;">{examTitle}</h3>
                <p>Haz clic en el siguiente botón para comenzar:</p>
                <a href="{link}" style="display: inline-block; background: #1e40af; color: white;
                   padding: 12px 24px; border-radius: 6px; text-decoration: none; font-weight: bold;">
                    Comenzar prueba
                </a>
                <p style="margin-top: 20px; color: #6b7280; font-size: 14px;">
                    ⚠️ Este enlace expira el {expiresAt:dd/MM/yyyy HH:mm} UTC y solo puede usarse una vez.
                </p>
                <p style="color: #6b7280; font-size: 12px;">
                    Si el botón no funciona, copia este enlace: <br>{link}
                </p>
            </div>
        </body>
        </html>
        """;

    private static string BuildResultHtml(
        string name, string examTitle, decimal score, bool passed) => $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
            <div style="background: #1e40af; color: white; padding: 20px; border-radius: 8px 8px 0 0;">
                <h1 style="margin: 0;">TechEval Platform</h1>
            </div>
            <div style="background: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px;">
                <h2>Hola, {name}</h2>
                <p>Has completado la prueba <strong>{examTitle}</strong>.</p>
                <div style="text-align: center; padding: 20px; background: {(passed ? "#dcfce7" : "#fee2e2")};
                     border-radius: 8px; margin: 20px 0;">
                    <div style="font-size: 48px; font-weight: bold; color: {(passed ? "#16a34a" : "#dc2626")};">
                        {score}%
                    </div>
                    <div style="font-size: 24px; color: {(passed ? "#16a34a" : "#dc2626")}; font-weight: bold;">
                        {(passed ? "✅ APROBADO" : "❌ NO APROBADO")}
                    </div>
                </div>
                <p>Gracias por participar en el proceso de evaluación.</p>
            </div>
        </body>
        </html>
        """;

    // El nombre lo escribe un administrador en el alta, así que se codifica antes de
    // insertarlo en el HTML.
    private static string BuildPasswordSetupHtml(string name, string link, DateTime expiresAt) => $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
            <div style="background: #1e40af; color: white; padding: 20px; border-radius: 8px 8px 0 0;">
                <h1 style="margin: 0;">TechEval Platform</h1>
            </div>
            <div style="background: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px;">
                <h2>Hola, {WebUtility.HtmlEncode(name)}</h2>
                <p>Tienes una cuenta en TechEval. Para entrar, primero fija tu contraseña:</p>
                <a href="{link}" style="display: inline-block; background: #1e40af; color: white;
                   padding: 12px 24px; border-radius: 6px; text-decoration: none; font-weight: bold;">
                    Fijar contraseña
                </a>
                <p style="margin-top: 20px; color: #6b7280; font-size: 14px;">
                    ⚠️ Este enlace expira el {expiresAt:dd/MM/yyyy HH:mm} UTC y solo puede usarse una vez.
                    Si caduca, pide uno nuevo al administrador.
                </p>
                <p style="color: #6b7280; font-size: 12px;">
                    Si no esperabas este correo, ignóralo: sin el enlace nadie puede fijar la contraseña.
                </p>
                <p style="color: #6b7280; font-size: 12px;">
                    Si el botón no funciona, copia este enlace: <br>{link}
                </p>
            </div>
        </body>
        </html>
        """;

    // Sin puntuación ni veredicto por diseño: la prueba contiene preguntas que aún nadie
    // ha corregido, y adelantar una cifra parcial sería comunicar una nota falsa.
    private static string BuildPendingReviewHtml(string name, string examTitle) => $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
            <div style="background: #1e40af; color: white; padding: 20px; border-radius: 8px 8px 0 0;">
                <h1 style="margin: 0;">TechEval Platform</h1>
            </div>
            <div style="background: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px;">
                <h2>Hola, {name}</h2>
                <p>Hemos recibido correctamente tu prueba <strong>{examTitle}</strong>.</p>
                <div style="padding: 20px; background: #e0e7ff; border-radius: 8px; margin: 20px 0;">
                    <p style="margin: 0;">
                        Esta prueba incluye preguntas de respuesta abierta que requieren
                        <strong>corrección manual</strong> por parte del equipo evaluador.
                    </p>
                </div>
                <p>Te enviaremos el resultado por email en cuanto la corrección esté finalizada.</p>
                <p>Gracias por participar en el proceso de evaluación.</p>
            </div>
        </body>
        </html>
        """;
}

/// <summary>Token de aplicación para llamar a Microsoft Graph.</summary>
public interface IGraphAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct);
}

/// <summary>
/// Credenciales de cliente con MSAL. Va como singleton: MSAL guarda el token en la instancia
/// y lo reutiliza hasta poco antes de que caduque, así que no se pide uno por correo.
/// </summary>
/// <remarks>
/// La aplicación de MSAL se construye en el primer envío y no al arrancar. En desarrollo,
/// sin credenciales, la API arranca igual y cada envío falla con su error en el log.
/// </remarks>
public class MsalGraphAccessTokenProvider : IGraphAccessTokenProvider
{
    private static readonly string[] Scopes = ["https://graph.microsoft.com/.default"];

    private readonly Lazy<IConfidentialClientApplication> _app;

    public MsalGraphAccessTokenProvider(IOptions<EmailSettings> settings)
    {
        var office365 = settings.Value.Office365;
        _app = new Lazy<IConfidentialClientApplication>(() => ConfidentialClientApplicationBuilder
            .Create(office365.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, office365.TenantId)
            .WithClientSecret(office365.ClientSecret)
            .Build());
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var result = await _app.Value.AcquireTokenForClient(Scopes).ExecuteAsync(ct);
        return result.AccessToken;
    }
}
