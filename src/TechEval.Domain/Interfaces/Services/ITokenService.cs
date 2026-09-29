using TechEval.Domain.Entities;

namespace TechEval.Domain.Interfaces.Services;

public interface ITokenService
{
    string GenerateSecureToken();

    /// <summary>
    /// JWT con el rol y el sello de seguridad del usuario. La API rechaza el token en cuanto
    /// cualquiera de los dos deja de coincidir con la base de datos.
    /// </summary>
    string GenerateJwtToken(User user);
}
