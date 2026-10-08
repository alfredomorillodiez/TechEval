using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TechEval.API.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TechEval.Application.DTOs;
using TechEval.Application.Services;

namespace TechEval.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.Gestion)]
public class ExamsController : ControllerBase
{
    private readonly IExamService _examService;
    private readonly IExamTokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IExamEvaluatorService _evaluators;

    public ExamsController(
        IExamService examService, IExamTokenService tokenService, IConfiguration configuration,
        IExamEvaluatorService evaluators)
    {
        _evaluators = evaluators;
        _examService = examService;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _examService.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _examService.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExamDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _examService.CreateAsync(dto, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Genera una prueba automáticamente con preguntas aleatorias</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateExamDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _examService.GenerateAsync(dto, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExamDto dto, CancellationToken ct)
    {
        var result = await _examService.UpdateAsync(id, dto, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _examService.DeleteAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Envía la prueba por email al candidato generando un token único</summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendExam([FromBody] SendExamDto dto, CancellationToken ct)
    {
        var baseUrl = _configuration["FrontendBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var token = await _tokenService.SendExamAsync(dto, baseUrl, ct);
        return Ok(new { token, message = $"Prueba enviada correctamente a {dto.CandidateEmail}" });
    }

    /// <summary>Envía la misma prueba a múltiples candidatos en una sola operación</summary>
    [HttpPost("send-bulk")]
    public async Task<IActionResult> SendExamBulk([FromBody] BulkSendExamDto dto, CancellationToken ct)
    {
        if (dto.Candidates is null || dto.Candidates.Count == 0)
            return BadRequest("La lista de candidatos no puede estar vacía.");

        var baseUrl = _configuration["FrontendBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var result = await _tokenService.SendExamBulkAsync(dto, baseUrl, ct);
        return Ok(result);
    }

    /// <summary>Evaluadores asignados a la prueba</summary>
    [HttpGet("{id:int}/evaluators")]
    [ProducesResponseType(typeof(IReadOnlyList<ExamEvaluatorDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetEvaluators(int id, CancellationToken ct)
        => Ok(await _evaluators.ListAsync(id, ct));

    /// <summary>Asigna un evaluador activo a la prueba. Repetir la asignación no hace nada</summary>
    [HttpPost("{id:int}/evaluators/{userId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<ExamEvaluatorDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AssignEvaluator(int id, int userId, CancellationToken ct)
        => Ok(await _evaluators.AssignAsync(id, userId, GetCurrentUserId(), ct));

    /// <summary>Quita un evaluador de la prueba. Sus correcciones hechas no cambian</summary>
    [HttpDelete("{id:int}/evaluators/{userId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<ExamEvaluatorDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UnassignEvaluator(int id, int userId, CancellationToken ct)
        => Ok(await _evaluators.UnassignAsync(id, userId, ct));

    private int GetCurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
