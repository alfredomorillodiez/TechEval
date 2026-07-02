using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechEval.Application.DTOs;
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

    /// <summary>Autenticación de administradores</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResultDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive && u.IsAdmin, ct);

        if (user is null || !VerifyPassword(dto.Password, user.PasswordHash))
            return Unauthorized(new { error = "Credenciales incorrectas." });

        var token = _tokenService.GenerateJwtToken(user.Id, user.Email, user.IsAdmin);
        return Ok(new AuthResultDto(token, user.Name, user.Email));
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var computed = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLower();
        return computed == hash.ToLower();
    }
}
