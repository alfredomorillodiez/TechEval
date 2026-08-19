using System.Security.Cryptography;
using System.Text;

namespace TechEval.Application.Services;

public static class PasswordHasher
{
    public static string Hash(string password)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
}
