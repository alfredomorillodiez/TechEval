using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TechEval.Infrastructure.Data;

namespace TechEval.Infrastructure.Security;

/// <summary>
/// Comprueba, en cada petición autenticada, que el token sigue describiendo al usuario:
/// que existe, que está activo, que conserva el rol y que su sello no ha cambiado.
/// </summary>
/// <remarks>
/// Un JWT firmado vale hasta que caduca, pase lo que pase con la cuenta. Sin esta
/// comprobación, desactivar a alguien o quitarle un rol no tenía efecto hasta ocho horas
/// después. El sello cambia en cada operación que debe revocar el acceso, así que basta
/// compararlo para invalidar a la vez todos los tokens del usuario.
///
/// Es una lectura por clave primaria de tres columnas. No hay caché a propósito: una caché
/// reabre una ventana de revocación igual a su vida.
/// </remarks>
public class SecurityStampValidator
{
    private readonly AppDbContext _context;

    public SecurityStampValidator(AppDbContext context) => _context = context;

    public async Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        if (!int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return false;

        // Sin sello no hay nada que comparar: es un token de la versión anterior.
        if (!Guid.TryParse(principal.FindFirst(TechEvalClaims.SecurityStamp)?.Value, out var stamp))
            return false;

        var role = principal.FindFirst(ClaimTypes.Role)?.Value;

        var current = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.Role, u.SecurityStamp })
            .FirstOrDefaultAsync(ct);

        return current is not null
            && current.IsActive
            && current.SecurityStamp == stamp
            && current.Role.ToString() == role;
    }
}
