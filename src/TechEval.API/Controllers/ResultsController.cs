using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechEval.Application.Services;

namespace TechEval.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class ResultsController : ControllerBase
{
    private readonly IResultService _service;

    public ResultsController(IResultService service) => _service = service;

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

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
        => Ok(await _service.GetDashboardStatsAsync(ct));
}
