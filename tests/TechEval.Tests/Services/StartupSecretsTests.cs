using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;
using TechEval.API;

namespace TechEval.Tests.Services;

/// <summary>
/// Validación de los secretos al arrancar. Fuera de desarrollo, una configuración
/// incompleta tiene que parar el arranque en vez de caer en un valor conocido.
/// </summary>
public class StartupSecretsTests
{
    private const string ClaveDeDesarrollo = "TechEval_Dev_Only_NotASecret_ChangeInProduction";

    private static IConfiguration Configuracion(
        string? jwt = "clave-larga-y-aleatoria-de-produccion",
        string? admin = "contraseña-de-produccion",
        string? conexion = "Server=db;Database=TechEvalDb;")
    {
        var valores = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = jwt,
            ["AdminPassword"] = admin,
            ["ConnectionStrings:DefaultConnection"] = conexion
        };

        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    [Fact]
    public void ConfiguracionCompleta_NoSeQueja()
    {
        StartupSecrets.Check(Configuracion()).Should().BeEmpty();

        var act = () => StartupSecrets.Validate(Configuracion());
        act.Should().NotThrow();
    }

    [Fact]
    public void FaltaLaClaveDeFirma_SeDetieneYLaNombra()
    {
        var problemas = StartupSecrets.Check(Configuracion(jwt: null));

        problemas.Should().ContainSingle();
        problemas[0].Should().Contain("Jwt:SecretKey");
    }

    [Fact]
    public void FaltaLaContrasenaDeAdministrador_SeDetieneYLaNombra()
    {
        var problemas = StartupSecrets.Check(Configuracion(admin: "   "));

        problemas.Should().ContainSingle();
        problemas[0].Should().Contain("AdminPassword");
    }

    [Fact]
    public void FaltaLaCadenaDeConexion_SeDetieneYLaNombra()
    {
        var problemas = StartupSecrets.Check(Configuracion(conexion: ""));

        problemas.Should().ContainSingle();
        problemas[0].Should().Contain("ConnectionStrings:DefaultConnection");
    }

    [Fact]
    public void ClaveDeFirmaConElValorDeDesarrollo_SeRechaza()
    {
        // Copiar appsettings.Development.json a producción dejaría una clave pública
        // haciendo de secreto, y una comprobación de "no está vacío" la daría por buena.
        var problemas = StartupSecrets.Check(Configuracion(jwt: ClaveDeDesarrollo));

        problemas.Should().ContainSingle();
        problemas[0].Should().Contain("valor de desarrollo");
    }

    [Fact]
    public void ContrasenaDeAdministradorConElValorDeDesarrollo_SeRechaza()
    {
        var problemas = StartupSecrets.Check(Configuracion(admin: "Admin@123!"));

        problemas.Should().ContainSingle();
        problemas[0].Should().Contain("valor de desarrollo");
    }

    [Fact]
    public void VariosProblemas_SeInformanTodosDeUnaVez()
    {
        // Quien despliega arregla los tres a la vez, no de uno en uno.
        var problemas = StartupSecrets.Check(Configuracion(jwt: null, admin: null, conexion: null));

        problemas.Should().HaveCount(3);
    }

    [Fact]
    public void Validate_ConProblemas_LanzaConTodosLosNombres()
    {
        var act = () => StartupSecrets.Validate(Configuracion(jwt: null, admin: "Admin@123!"));

        act.Should().Throw<InvalidOperationException>()
            .Where(e => e.Message.Contains("Jwt:SecretKey") && e.Message.Contains("AdminPassword"));
    }
}
