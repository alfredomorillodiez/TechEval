using Microsoft.AspNetCore.Authorization;
using TechEval.API.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Common;

namespace TechEval.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.Gestion)]
public class ResultsController : ControllerBase
{
    private readonly IResultService _service;
    private readonly IExamIntegrityService _integrityService;

    public ResultsController(IResultService service, IExamIntegrityService integrityService)
    {
        _service = service;
        _integrityService = integrityService;
    }

    /// <summary>
    /// Una página de resultados. <c>from</c> es inclusivo y <c>to</c> exclusivo, en UTC.
    /// <c>outcome</c> es <c>pending</c>, <c>passed</c> o <c>failed</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ExamResultSummaryDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PageQuery paging,
        [FromQuery] int? examId, [FromQuery] string? outcome,
        [FromQuery] int? scoreMin, [FromQuery] int? scoreMax,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
        => Ok(await _service.GetPagedAsync(
            new ResultFilter(examId, outcome, scoreMin, scoreMax, from?.UtcDateTime, to?.UtcDateTime), paging, ct));

    /// <summary>Lo mismo que la lista general, fijada a una prueba</summary>
    [HttpGet("exam/{examId:int}")]
    [ProducesResponseType(typeof(PagedResult<ExamResultSummaryDto>), 200)]
    public async Task<IActionResult> GetByExam(
        int examId, [FromQuery] PageQuery paging,
        [FromQuery] string? outcome, [FromQuery] int? scoreMin, [FromQuery] int? scoreMax,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
        => Ok(await _service.GetPagedAsync(
            new ResultFilter(examId, outcome, scoreMin, scoreMax, from?.UtcDateTime, to?.UtcDateTime), paging, ct));

    /// <summary>Cifras de todos los resultados, o de los de una prueba. No dependen de ningún filtro</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ResultStatsDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetStats([FromQuery] int? examId, CancellationToken ct)
        => Ok(await _service.GetStatsAsync(examId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id, CancellationToken ct)
    {
        var result = await _service.GetDetailAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Señales de integridad de la sesión del resultado, para el corrector</summary>
    [HttpGet("{id:int}/integrity")]
    [ProducesResponseType(typeof(IntegrityReportDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetIntegrity(int id, CancellationToken ct)
        => Ok(await _integrityService.GetReportAsync(id, ct));

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
        => Ok(await _service.GetDashboardStatsAsync(ct));
}
