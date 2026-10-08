using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TechEval.API.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechEval.Application.DTOs;
using TechEval.Application.Services;

namespace TechEval.API.Controllers;

/// <summary>Corrección a ciegas de las pruebas asignadas al evaluador</summary>
/// <remarks>
/// Todo lo del evaluador vive aquí, con la política en la clase. Así no hay que abrir ningún
/// endpoint de ResultsController ni de ReviewController, cuyo [Authorize] de clase se
/// combinaría con Y con el de un método.
///
/// Un resultado que no existe, que es de una prueba no asignada o que es del propio
/// evaluador responde 404 en todos los casos. Los 409 (ya corregido, o reservado por otra
/// persona) los produce ErrorHandlingMiddleware.
/// </remarks>
[ApiController]
[Route("api/evaluation")]
[Authorize(Policy = Policies.Evaluacion)]
public class EvaluationController : ControllerBase
{
    private readonly IEvaluationService _service;

    public EvaluationController(IEvaluationService service) => _service = service;

    /// <summary>Pendientes de las pruebas asignadas, del más antiguo al más reciente</summary>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(IReadOnlyList<EvaluatorQueueItemDto>), 200)]
    public async Task<IActionResult> GetQueue(CancellationToken ct)
        => Ok(await _service.GetQueueAsync(CurrentUserId(), ct));

    /// <summary>Detalle de corrección a ciegas. Abrirlo reserva el resultado 30 minutos</summary>
    [HttpGet("{resultId:int}")]
    [ProducesResponseType(typeof(EvaluatorReviewDetailDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> GetDetail(int resultId, CancellationToken ct)
        => Ok(await _service.GetDetailAsync(resultId, CurrentUserId(), ct));

    /// <summary>Renueva la reserva propia por otros 30 minutos</summary>
    [HttpPost("{resultId:int}/reservation")]
    [ProducesResponseType(typeof(ReservationDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> RenewReservation(int resultId, CancellationToken ct)
        => Ok(await _service.RenewReservationAsync(resultId, CurrentUserId(), ct));

    /// <summary>Libera la reserva propia sin corregir</summary>
    [HttpDelete("{resultId:int}/reservation")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ReleaseReservation(int resultId, CancellationToken ct)
    {
        await _service.ReleaseReservationAsync(resultId, CurrentUserId(), ct);
        return NoContent();
    }

    /// <summary>Envía la corrección de todas las respuestas abiertas</summary>
    [HttpPost("{resultId:int}")]
    [ProducesResponseType(typeof(EvaluatorReviewOutcomeDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Submit(int resultId, [FromBody] SubmitReviewDto dto, CancellationToken ct)
        => Ok(await _service.SubmitAsync(resultId, dto, CurrentUserId(), ct));

    /// <summary>Señales de integridad, con el tiempo desde el inicio y sin la hora del reloj</summary>
    [HttpGet("{resultId:int}/integrity")]
    [ProducesResponseType(typeof(IntegrityReportDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetIntegrity(int resultId, CancellationToken ct)
        => Ok(await _service.GetIntegrityAsync(resultId, CurrentUserId(), ct));

    /// <summary>Resultados que corrigió el evaluador, aunque ya no tenga asignada la prueba</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<EvaluatorHistoryItemDto>), 200)]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
        => Ok(await _service.GetHistoryAsync(CurrentUserId(), ct));

    /// <summary>Detalle de solo lectura de una corrección propia</summary>
    [HttpGet("history/{resultId:int}")]
    [ProducesResponseType(typeof(EvaluatorHistoryDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetHistoryDetail(int resultId, CancellationToken ct)
        => Ok(await _service.GetHistoryDetailAsync(resultId, CurrentUserId(), ct));

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
