using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechEval.Application.DTOs;
using TechEval.Application.Services;

namespace TechEval.API.Controllers;

/// <summary>Endpoints de examen accesibles con el token del enlace, sin requerir login previo</summary>
[ApiController]
[Route("api/exam")]
public class ExamSessionController : ControllerBase
{
    private readonly IExamTokenService _tokenService;

    public ExamSessionController(IExamTokenService tokenService) => _tokenService = tokenService;

    /// <summary>Valida el token, aprovisiona/reutiliza la cuenta del alumno y devuelve un JWT de auto-login</summary>
    [HttpGet("validate/{token}")]
    [EnableRateLimiting(RateLimiting.ExamLinkPolicy)]
    [ProducesResponseType(429)]
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
    /// <remarks>
    /// Exige el JWT de alumno que devuelve la validación del enlace. El identificador de
    /// sesión es secuencial, así que por sí solo nunca puede valer como prueba de propiedad.
    /// </remarks>
    [HttpPost("answer/{sessionId:int}")]
    [Authorize(Roles = "Alumno")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> SaveAnswer(
        int sessionId, [FromBody] SubmitAnswerDto dto, CancellationToken ct)
    {
        // Los 403 y 409 los produce ErrorHandlingMiddleware, que es el único sitio donde una
        // excepción se convierte en código de estado.
        await _tokenService.SaveDraftAnswerAsync(sessionId, CurrentUserId(), dto, ct);
        return Ok();
    }

    /// <summary>Envía el examen completo y devuelve los resultados</summary>
    [HttpPost("submit")]
    [Authorize(Roles = "Alumno")]
    [ProducesResponseType(typeof(ExamSubmissionReceiptDto), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Submit([FromBody] SubmitExamDto dto, CancellationToken ct)
    {
        var result = await _tokenService.SubmitExamAsync(dto, CurrentUserId(), ct);
        return Ok(result);
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
