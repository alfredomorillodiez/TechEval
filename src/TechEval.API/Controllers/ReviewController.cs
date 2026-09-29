using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TechEval.API.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechEval.Application.DTOs;
using TechEval.Application.Services;

namespace TechEval.API.Controllers;

/// <summary>Corrección manual de las preguntas abiertas de una prueba</summary>
[ApiController]
[Route("api/review")]
[Authorize(Policy = Policies.Gestion)]
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
        // Abrir el detalle reserva el resultado. Los 409 (ya corregido, o reservado por otra
        // persona) los produce ErrorHandlingMiddleware.
        var detail = await _service.GetDetailAsync(resultId, GetCurrentUserId(), ct);
        return detail is null ? NotFound() : Ok(detail);
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
        var result = await _service.SubmitReviewAsync(resultId, dto, GetCurrentUserId(), ct);
        return Ok(result);
    }

    /// <summary>Renueva la reserva de quien la tiene, por otros 30 minutos</summary>
    [HttpPost("{resultId:int}/reservation")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> RenewReservation(int resultId, CancellationToken ct)
        => Ok(new { reservedUntil = await _service.RenewReservationAsync(resultId, GetCurrentUserId(), ct) });

    /// <summary>Libera la reserva, sea de quien sea</summary>
    [HttpDelete("{resultId:int}/reservation")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ReleaseReservation(int resultId, CancellationToken ct)
    {
        await _service.ReleaseReservationAsync(resultId, ct);
        return NoContent();
    }

    private int GetCurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
