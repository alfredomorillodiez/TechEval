using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Infrastructure.Email;

public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "TechEval Platform";
    public bool EnableSsl { get; set; } = true;
}

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendExamInvitationAsync(
        string toEmail, string toName, string examTitle,
        string examLink, DateTime expiresAt, CancellationToken ct = default)
    {
        var subject = $"Invitación a examen técnico: {examTitle}";
        var body = BuildInvitationHtml(toName, examTitle, examLink, expiresAt);
        await SendAsync(toEmail, toName, subject, body, ct);
    }

    public async Task SendExamResultAsync(
        string toEmail, string toName, string examTitle,
        decimal scorePercentage, bool passed, CancellationToken ct = default)
    {
        var subject = $"Resultado de tu examen: {examTitle}";
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

    private async Task SendAsync(
        string toEmail, string toName, string subject, string body, CancellationToken ct)
    {
        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.UserName, _settings.Password),
                EnableSsl = _settings.EnableSsl
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail, toName));

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Email enviado a {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando email a {Email}", toEmail);
            throw;
        }
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
                <p>Has sido invitado a completar el siguiente examen técnico:</p>
                <h3 style="color: #1e40af;">{examTitle}</h3>
                <p>Haz clic en el siguiente botón para comenzar:</p>
                <a href="{link}" style="display: inline-block; background: #1e40af; color: white;
                   padding: 12px 24px; border-radius: 6px; text-decoration: none; font-weight: bold;">
                    Comenzar Examen
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
                <p>Has completado el examen <strong>{examTitle}</strong>.</p>
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
