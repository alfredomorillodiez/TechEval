using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;
using TechEval.Application.Services;

namespace TechEval.Tests.Services;

/// <summary>
/// Derivación de contraseñas. SHA-256 sin sal es rápido a propósito, y eso lo hace mal
/// candidato: una tabla precalculada rompe las contraseñas comunes en segundos.
/// </summary>
public class PasswordHasherTests
{
    private static string Sha256Hex(string password)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();

    [Fact]
    public void Hash_MismaContrasena_ProduceValoresDistintos()
    {
        var uno = PasswordHasher.Hash("Admin@123!");
        var otro = PasswordHasher.Hash("Admin@123!");

        uno.Should().NotBe(otro, "cada hash lleva su propia sal");
    }

    [Fact]
    public void Hash_LlevaSusPropiosParametros()
    {
        var hash = PasswordHasher.Hash("Admin@123!");

        hash.Should().StartWith("pbkdf2.sha256.");
        hash.Split('.').Should().HaveCount(5, "algoritmo, variante, iteraciones, sal y hash");
    }

    [Fact]
    public void Verify_ContrasenaCorrecta_EsValida()
    {
        var hash = PasswordHasher.Hash("Admin@123!");

        var (isValid, needsUpgrade) = PasswordHasher.Verify("Admin@123!", hash);

        isValid.Should().BeTrue();
        needsUpgrade.Should().BeFalse();
    }

    [Fact]
    public void Verify_ContrasenaIncorrecta_NoEsValida()
    {
        var hash = PasswordHasher.Hash("Admin@123!");

        PasswordHasher.Verify("otra cosa", hash).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_FormatoAntiguo_EsValidoYPideMigracion()
    {
        var antiguo = Sha256Hex("Admin@123!");

        var (isValid, needsUpgrade) = PasswordHasher.Verify("Admin@123!", antiguo);

        isValid.Should().BeTrue();
        needsUpgrade.Should().BeTrue();
    }

    [Fact]
    public void Verify_FormatoAntiguoConContrasenaIncorrecta_NoEsValido()
    {
        var antiguo = Sha256Hex("Admin@123!");

        PasswordHasher.Verify("otra cosa", antiguo).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_HashVacio_SeRechazaSiempre(string almacenado)
    {
        // Un hash vacío significa "cuenta sin contraseña utilizable".
        PasswordHasher.Verify("lo que sea", almacenado).IsValid.Should().BeFalse();
        PasswordHasher.Verify("", almacenado).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_HashCorrupto_NoEsValidoYNoLanza()
    {
        var act = () => PasswordHasher.Verify("Admin@123!", "pbkdf2.sha256.600000.no-es-base64.tampoco");

        act.Should().NotThrow();
        PasswordHasher.Verify("Admin@123!", "pbkdf2.sha256.600000.xx.yy").IsValid.Should().BeFalse();
    }

    [Fact]
    public void Hash_YVerify_SoportanContrasenasConAcentosYSimbolos()
    {
        const string contrasena = "Contraseña con ñ, tildes áéí y símbolos €#@";

        var hash = PasswordHasher.Hash(contrasena);

        PasswordHasher.Verify(contrasena, hash).IsValid.Should().BeTrue();
    }
}
