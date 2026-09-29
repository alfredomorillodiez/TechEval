using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TechEval.API.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Enums;

namespace TechEval.API.Controllers;

/// <summary>Gestión de las cuentas: alta, rol, estado y acceso</summary>
/// <remarks>
/// Los 400, 404 y 409 los produce ErrorHandlingMiddleware a partir de las excepciones del
/// servicio. Un 409 aquí significa casi siempre una operación sobre uno mismo o una que
/// dejaría el sistema sin administradores activos.
/// </remarks>
[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.Gestion)]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _service;
    private readonly IConfiguration _configuration;

    public UsersController(IUserManagementService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    /// <summary>Usuarios, filtrados por rol, estado y texto sobre el nombre o el email</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), 200)]
    public async Task<IActionResult> List(
        [FromQuery] UserRole? role, [FromQuery] bool? active, [FromQuery] string? q, CancellationToken ct)
        => Ok(await _service.ListAsync(role, active, q, ct));

    /// <summary>Crea un administrador o un evaluador y le envía el enlace para fijar la contraseña</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserActionResultDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto, CancellationToken ct)
    {
        var result = await _service.CreateAsync(dto, FrontendBaseUrl(), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Cambia el rol. Los tokens anteriores del usuario dejan de valer</summary>
    [HttpPut("{id:int}/role")]
    [ProducesResponseType(typeof(UserActionResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleDto dto, CancellationToken ct)
        => Ok(await _service.ChangeRoleAsync(id, dto, CurrentUserId(), FrontendBaseUrl(), ct));

    /// <summary>Desactiva la cuenta. Sus tokens dejan de valer en la siguiente petición</summary>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
        => Ok(await _service.DeactivateAsync(id, CurrentUserId(), ct));

    /// <summary>Reactiva la cuenta. Los tokens anteriores a la desactivación siguen sin valer</summary>
    [HttpPost("{id:int}/activate")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
        => Ok(await _service.ActivateAsync(id, ct));

    /// <summary>Quita la contraseña, invalida tokens y enlaces, y envía un enlace nuevo</summary>
    [HttpPost("{id:int}/reset-access")]
    [ProducesResponseType(typeof(UserActionResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> ResetAccess(int id, CancellationToken ct)
        => Ok(await _service.ResetAccessAsync(id, CurrentUserId(), FrontendBaseUrl(), ct));

    private string FrontendBaseUrl()
        => _configuration["FrontendBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";

    private int CurrentUserId()
        => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
