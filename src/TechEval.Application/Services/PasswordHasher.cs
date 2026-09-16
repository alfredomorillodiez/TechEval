using System.Security.Cryptography;
using System.Text;

namespace TechEval.Application.Services;

/// <summary>
/// Derivación de contraseñas con PBKDF2-HMAC-SHA256.
/// </summary>
/// <remarks>
/// El valor almacenado lleva sus propios parámetros — `pbkdf2.sha256.iteraciones.sal.hash` —
/// para poder subir el coste dentro de unos años sin invalidar los hashes existentes: los
/// viejos siguen verificando con los suyos y se reescriben al entrar.
///
/// Se conserva la verificación del SHA-256 anterior por compatibilidad. Un script no puede
/// convertirlo: haría falta la contraseña en claro, y la única ocasión en que el sistema la
/// ve es el login. Por eso `Verify` informa de si el hash necesita migrarse.
/// </remarks>
public static class PasswordHasher
{
    private const string Prefix = "pbkdf2.sha256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>Recomendación vigente de OWASP para PBKDF2-HMAC-SHA256.</summary>
    private const int Iterations = 600_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, Iterations);

        return string.Join('.',
            Prefix,
            Iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Comprueba la contraseña contra el valor almacenado.
    /// </summary>
    /// <returns>
    /// `IsValid`: si la contraseña es correcta.
    /// `NeedsUpgrade`: si el valor almacenado está en el formato antiguo y conviene
    /// reescribirlo. Solo tiene sentido cuando `IsValid` es cierto.
    /// </returns>
    public static (bool IsValid, bool NeedsUpgrade) Verify(string password, string storedHash)
    {
        // Un hash vacío significa "esta cuenta no tiene contraseña utilizable". La
        // comprobación vive aquí y no en el controlador para que ningún camino de
        // autenticación futuro pueda saltársela por olvido.
        if (string.IsNullOrWhiteSpace(storedHash)) return (false, false);

        if (storedHash.StartsWith(Prefix + ".", StringComparison.Ordinal))
            return (VerifyPbkdf2(password, storedHash), false);

        return (VerifyLegacySha256(password, storedHash), NeedsUpgrade: true);
    }

    private static bool VerifyPbkdf2(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 5) return false;
        if (!int.TryParse(parts[2], out var iterations) || iterations <= 0) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, iterations, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool VerifyLegacySha256(string password, string storedHash)
    {
        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)))
            .ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actual),
            Encoding.UTF8.GetBytes(storedHash.ToLowerInvariant()));
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashBytes)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, length);
}
