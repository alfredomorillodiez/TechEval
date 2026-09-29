namespace TechEval.Domain.Entities;

/// <summary>
/// Enlace de un solo uso para que un administrador o un evaluador fije su contraseña.
/// </summary>
/// <remarks>
/// Solo se guarda el SHA-256 del token, nunca el token: quien lea la base no puede usar un
/// enlace pendiente. Basta un hash rápido porque el token son 48 bytes aleatorios, sin
/// diccionario que probar, y así se puede buscar por índice.
/// </remarks>
public class PasswordSetupToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;

    public bool IsUsable(DateTime now) => UsedAt is null && now <= ExpiresAt;
}
