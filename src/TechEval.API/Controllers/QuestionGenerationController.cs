using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechEval.Application.DTOs;
using TechEval.Application.Services;

namespace TechEval.API.Controllers;

[ApiController]
[Route("api/question-generation")]
[Authorize(Roles = "Admin")]
public class QuestionGenerationController : ControllerBase
{
    private readonly IQuestionGenerationService _service;

    public QuestionGenerationController(IQuestionGenerationService service) => _service = service;

    /// <summary>Solicita la generación en segundo plano de un lote de preguntas por IA</summary>
    [HttpPost("jobs")]
    public async Task<IActionResult> CreateJob([FromBody] CreateQuestionGenerationJobDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _service.CreateJobAsync(dto, userId, ct);
        return Accepted(result);
    }

    /// <summary>Lista los ítems de generación pendientes de revisión (exitosos y fallidos)</summary>
    [HttpGet("pending-items")]
    public async Task<IActionResult> GetPendingItems(CancellationToken ct)
        => Ok(await _service.GetPendingReviewItemsAsync(ct));

    /// <summary>Progreso agregado de los jobs de generación aún con ítems pendientes de atención</summary>
    [HttpGet("jobs/progress")]
    public async Task<IActionResult> GetJobsProgress(CancellationToken ct)
        => Ok(await _service.GetActiveJobsProgressAsync(ct));

    /// <summary>Aprueba una pregunta generada, dejándola disponible para pruebas</summary>
    [HttpPost("items/{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken ct)
        => Ok(await _service.ApproveAsync(id, ct));

    /// <summary>Rechaza una pregunta generada, excluyéndola permanentemente del banco</summary>
    [HttpPost("items/{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, CancellationToken ct)
    {
        var ok = await _service.RejectAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    private int GetCurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
