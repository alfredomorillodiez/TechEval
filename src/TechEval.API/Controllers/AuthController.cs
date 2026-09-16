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

    public AuthController(AppDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    /// <summary>Autenticación de administradores y alumnos</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResultDto), 200)]
    [ProducesResponseType(401)]
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

        var token = _tokenService.GenerateJwtToken(user.Id, user.Email, user.IsAdmin);
        return Ok(new AuthResultDto(token, user.Name, user.Email, user.IsAdmin));
    }
}
