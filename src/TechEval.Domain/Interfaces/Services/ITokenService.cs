namespace TechEval.Domain.Interfaces.Services;

public interface ITokenService
{
    string GenerateSecureToken();
    string GenerateJwtToken(int userId, string email, bool isAdmin);
    (int userId, string email, bool isAdmin)? ValidateJwtToken(string token);
}
