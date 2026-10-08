namespace TechEval.Infrastructure.Email;

public class EmailSettings
{
    /// <summary>
    /// Buzón que envía y remitente a la vez: Graph publica en `/users/{FromEmail}/sendMail`.
    /// El destinatario ve el nombre que el buzón tiene en Exchange.
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    public Office365Settings Office365 { get; set; } = new();
}

/// <summary>
/// Aplicación de Entra ID con el permiso de aplicación `Mail.Send` de Microsoft Graph.
/// </summary>
public class Office365Settings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
