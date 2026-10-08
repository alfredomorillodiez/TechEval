using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TechEval.API.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechEval.Application.DTOs;
using TechEval.Application.Services;

namespace TechEval.API.Controllers;

/// <summary>Endpoints de la prueba accesibles con el token del enlace, sin requerir login previo</summary>
[ApiController]
[Route("api/exam")]
public class ExamSessionController : ControllerBase
{
    private readonly IExamTokenService _tokenService;
    private readonly IExamIntegrityService _integrityService;

    public ExamSessionController(IExamTokenService tokenService, IExamIntegrityService integrityService)
    {
        _tokenService = tokenService;
        _integrityService = integrityService;
    }

    /// <summary>Valida el token, aprovisiona/reutiliza la cuenta del alumno y devuelve un JWT de auto-login</summary>
    [HttpGet("validate/{token}")]
    [EnableRateLimiting(RateLimiting.ExamLinkPolicy)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> Validate(string token, CancellationToken ct)
        => Ok(await _tokenService.ValidateTokenAsync(token, ct));

    /// <summary>Inicia la sesión de la prueba (marca el token como usado)</summary>
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
    [Authorize(Policy = Policies.Alumno)]
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

    /// <summary>Envía la prueba completa y devuelve los resultados</summary>
    [HttpPost("submit")]
    [Authorize(Policy = Policies.Alumno)]
    [ProducesResponseType(typeof(ExamSubmissionReceiptDto), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Submit([FromBody] SubmitExamDto dto, CancellationToken ct)
    {
        var result = await _tokenService.SubmitExamAsync(dto, CurrentUserId(), ct);
        return Ok(result);
    }

    /// <summary>Registra una señal de integridad: salida de la página, vuelta o pegado</summary>
    /// <remarks>
    /// Mismas reglas de propiedad que el auto-guardado. Pasado el tope de señales por sesión
    /// responde 200 sin guardar: el candidato no debe notar nada.
    /// </remarks>
    [HttpPost("integrity/{sessionId:int}")]
    [Authorize(Policy = Policies.Alumno)]
    [EnableRateLimiting(RateLimiting.IntegrityPolicy)]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(409)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> RecordIntegrityEvent(
        int sessionId, [FromBody] IntegrityEventInputDto dto, CancellationToken ct)
    {
        await _integrityService.RecordAsync(sessionId, CurrentUserId(), dto, ct);
        return Ok();
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
