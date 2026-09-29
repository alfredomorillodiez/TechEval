using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Interfaces.Services;
using TechEval.Infrastructure.Data;

namespace TechEval.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IPasswordSetupService _passwordSetup;

    public AuthController(
        AppDbContext context, ITokenService tokenService, IPasswordSetupService passwordSetup)
    {
        _context = context;
        _tokenService = tokenService;
        _passwordSetup = passwordSetup;
    }

    /// <summary>Autenticación con email (o usuario) y contraseña, para cualquier rol</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimiting.LoginPolicy)]
    [ProducesResponseType(typeof(AuthResultDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(
                u => (u.Email == dto.Email || u.Username == dto.Email) && u.IsActive, ct);

        if (user is null) return Unauthorized(new { error = "Credenciales incorrectas." });

        var (isValid, needsUpgrade) = PasswordHasher.Verify(dto.Password, user.PasswordHash);
        if (!isValid) return Unauthorized(new { error = "Credenciales incorrectas." });

        // El login es la única ocasión en que el sistema ve la contraseña en claro, así que
        // es el único momento en que un hash del formato antiguo puede migrarse.
        if (needsUpgrade)
        {
            user.PasswordHash = PasswordHasher.Hash(dto.Password);
            await _context.SaveChangesAsync(ct);
        }

        var token = _tokenService.GenerateJwtToken(user);
        return Ok(new AuthResultDto(token, user.Name, user.Email, user.Role));
    }

    /// <summary>Comprueba un enlace para fijar la contraseña y devuelve de quién es</summary>
    /// <remarks>
    /// Un enlace inexistente, caducado, usado o de una cuenta desactivada responden igual
    /// (404), para no decirle a nadie cuál de las condiciones falló.
    /// </remarks>
    [HttpGet("password-setup/{token}")]
    [EnableRateLimiting(RateLimiting.PasswordSetupPolicy)]
    [ProducesResponseType(typeof(PasswordSetupInfoDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> CheckPasswordSetup(string token, CancellationToken ct)
        => Ok(await _passwordSetup.CheckAsync(token, ct));

    /// <summary>Fija la contraseña con un enlace de un solo uso. No inicia la sesión</summary>
    [HttpPost("password-setup")]
    [EnableRateLimiting(RateLimiting.PasswordSetupPolicy)]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordDto dto, CancellationToken ct)
    {
        await _passwordSetup.SetPasswordAsync(dto, ct);
        return NoContent();
    }
}
