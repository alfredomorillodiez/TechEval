using Microsoft.AspNetCore.Mvc;
using TechEval.Application.DTOs;
using TechEval.Application.Services;

namespace TechEval.API.Controllers;

/// <summary>Endpoints públicos para candidatos — sin autenticación JWT</summary>
[ApiController]
[Route("api/exam")]
public class ExamSessionController : ControllerBase
{
    private readonly IExamTokenService _tokenService;

    public ExamSessionController(IExamTokenService tokenService) => _tokenService = tokenService;

    /// <summary>Valida si el token es válido antes de mostrar la UI</summary>
    [HttpGet("validate/{token}")]
    public async Task<IActionResult> Validate(string token, CancellationToken ct)
        => Ok(await _tokenService.ValidateTokenAsync(token, ct));

    /// <summary>Inicia la sesión de examen (marca el token como usado)</summary>
    [HttpPost("start/{token}")]
    public async Task<IActionResult> Start(string token, CancellationToken ct)
    {
        var session = await _tokenService.StartSessionAsync(token, ct);
        return session is null
            ? BadRequest(new { error = "Token inválido o expirado." })
            : Ok(session);
    }

    /// <summary>Guarda una respuesta parcial (auto-guardado)</summary>
    [HttpPost("answer/{sessionId:int}")]
    public async Task<IActionResult> SaveAnswer(
        int sessionId, [FromBody] SubmitAnswerDto dto, CancellationToken ct)
    {
        await _tokenService.SaveDraftAnswerAsync(sessionId, dto, ct);
        return Ok();
    }

    /// <summary>Envía el examen completo y devuelve los resultados</summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitExamDto dto, CancellationToken ct)
    {
        var result = await _tokenService.SubmitExamAsync(dto, ct);
        return Ok(result);
    }
}
