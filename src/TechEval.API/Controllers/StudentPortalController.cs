using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechEval.Application.Services;

namespace TechEval.API.Controllers;

/// <summary>Portal del alumno: sus propias pruebas pendientes y realizadas</summary>
[ApiController]
[Route("api/student")]
[Authorize(Roles = "Alumno")]
public class StudentPortalController : ControllerBase
{
    private readonly IStudentPortalService _service;

    public StudentPortalController(IStudentPortalService service) => _service = service;

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken ct)
        => Ok(await _service.GetPendingAsync(CurrentUserId(), ct));

    [HttpGet("completed")]
    public async Task<IActionResult> GetCompleted(CancellationToken ct)
        => Ok(await _service.GetCompletedAsync(CurrentUserId(), ct));

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
