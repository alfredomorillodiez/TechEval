using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechEval.Application.DTOs;
using TechEval.Application.Services;

namespace TechEval.API.Controllers;

/// <summary>Corrección manual de las preguntas abiertas de una prueba</summary>
[ApiController]
[Route("api/review")]
[Authorize(Roles = "Admin")]
public class ReviewController : ControllerBase
{
    private readonly IOpenQuestionReviewService _service;

    public ReviewController(IOpenQuestionReviewService service) => _service = service;

    /// <summary>Cola de resultados pendientes de corrección, del más antiguo al más reciente</summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingReviewSummaryDto>), 200)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
        => Ok(await _service.GetPendingAsync(ct));

    /// <summary>Detalle de corrección de un resultado pendiente, con la respuesta de referencia</summary>
    [HttpGet("{resultId:int}")]
    [ProducesResponseType(typeof(PendingReviewDetailDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> GetDetail(int resultId, CancellationToken ct)
    {
        try
        {
            var detail = await _service.GetDetailAsync(resultId, ct);
            return detail is null ? NotFound() : Ok(detail);
        }
        catch (AlreadyReviewedException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Corrige de una sola vez todas las respuestas abiertas del resultado, recalcula la
    /// nota, cierra el resultado y notifica al candidato. No admite corrección parcial.
    /// </summary>
    [HttpPost("{resultId:int}")]
    [ProducesResponseType(typeof(ExamResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> SubmitReview(
        int resultId, [FromBody] SubmitReviewDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _service.SubmitReviewAsync(resultId, dto, GetCurrentUserId(), ct);
            return Ok(result);
        }
        catch (AlreadyReviewedException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidReviewException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private int GetCurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
