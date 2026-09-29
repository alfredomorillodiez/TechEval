using Microsoft.AspNetCore.Authorization;
using TechEval.API.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechEval.Application.DTOs;
using TechEval.Application.Services;

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

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("exam/{examId:int}")]
    public async Task<IActionResult> GetByExam(int examId, CancellationToken ct)
        => Ok(await _service.GetByExamAsync(examId, ct));

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
