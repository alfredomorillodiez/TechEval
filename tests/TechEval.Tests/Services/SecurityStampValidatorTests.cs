using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Security;

namespace TechEval.Tests.Services;

/// <summary>
/// Spec authentication — Revocación inmediata de los tokens: el token deja de valer en
/// cuanto el usuario se desactiva, cambia de rol o cambia de sello.
/// </summary>
public class SecurityStampValidatorTests
{
    private static AppDbContext NuevoContexto() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"stamp-{Guid.NewGuid()}")
            .Options);

    private static async Task<(AppDbContext Db, User Usuario)> ConEvaluadorAsync()
    {
        var db = NuevoContexto();
        var usuario = new User
        {
            Id = 7, Email = "laura@test.com", Name = "Laura", PasswordHash = "x",
            Role = UserRole.Evaluador
        };
        db.Users.Add(usuario);
        await db.SaveChangesAsync();
        return (db, usuario);
    }

    private static ClaimsPrincipal TokenDe(int id, string? role, Guid? stamp)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()) };
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
        if (stamp is not null) claims.Add(new Claim(TechEvalClaims.SecurityStamp, stamp.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task Usuario_sin_cambios_pasa()
    {
        var (db, u) = await ConEvaluadorAsync();

        var valido = await new SecurityStampValidator(db)
            .IsValidAsync(TokenDe(u.Id, "Evaluador", u.SecurityStamp));

        valido.Should().BeTrue();
    }

    [Fact]
    public async Task Usuario_desactivado_falla()
    {
        var (db, u) = await ConEvaluadorAsync();
        var token = TokenDe(u.Id, "Evaluador", u.SecurityStamp);
        u.IsActive = false;
        await db.SaveChangesAsync();

        (await new SecurityStampValidator(db).IsValidAsync(token)).Should().BeFalse();
    }

    [Fact]
    public async Task Rol_que_ya_no_corresponde_falla()
    {
        var (db, u) = await ConEvaluadorAsync();

        var valido = await new SecurityStampValidator(db)
            .IsValidAsync(TokenDe(u.Id, "Admin", u.SecurityStamp));

        valido.Should().BeFalse();
    }

    [Fact]
    public async Task Sello_cambiado_falla()
    {
        var (db, u) = await ConEvaluadorAsync();
        var token = TokenDe(u.Id, "Evaluador", u.SecurityStamp);
        u.RotateSecurityStamp();
        await db.SaveChangesAsync();

        (await new SecurityStampValidator(db).IsValidAsync(token)).Should().BeFalse();
    }

    [Fact]
    public async Task Token_sin_sello_de_la_version_anterior_falla()
    {
        var (db, u) = await ConEvaluadorAsync();

        var valido = await new SecurityStampValidator(db)
            .IsValidAsync(TokenDe(u.Id, "Evaluador", stamp: null));

        valido.Should().BeFalse();
    }

    [Fact]
    public async Task Usuario_inexistente_falla()
    {
        var (db, u) = await ConEvaluadorAsync();

        var valido = await new SecurityStampValidator(db)
            .IsValidAsync(TokenDe(999, "Evaluador", u.SecurityStamp));

        valido.Should().BeFalse();
    }
}
