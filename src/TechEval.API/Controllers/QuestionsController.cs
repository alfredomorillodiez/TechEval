using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Enums;

namespace TechEval.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class QuestionsController : ControllerBase
{
    private readonly IQuestionService _service;

    public QuestionsController(IQuestionService service) => _service = service;

    /// <summary>Listar preguntas con filtros opcionales</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? categoryId,
        [FromQuery] DifficultyLevel? difficulty,
        [FromQuery] QuestionType? type,
        CancellationToken ct)
        => Ok(await _service.GetAllAsync(categoryId, difficulty, type, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuestionDto dto, CancellationToken ct)
    {
        var result = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateQuestionDto dto, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, dto, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _service.DeleteAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
