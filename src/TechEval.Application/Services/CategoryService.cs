using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Application.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<CategoryDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default);
    Task<CategoryDto?> UpdateAsync(int id, UpdateCategoryDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class CategoryService : ICategoryService
{
    /// <summary>Tamaños de las columnas `Name` y `Description`. Nada los comprobaba.</summary>
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private readonly IRepository<Category> _repo;
    private readonly IQuestionRepository _questionRepo;

    public CategoryService(IRepository<Category> repo, IQuestionRepository questionRepo)
    {
        _repo = repo;
        _questionRepo = questionRepo;
    }

    /// <summary>
    /// Por defecto, solo las activas: es lo que ofrecen los desplegables de alta. La pantalla de
    /// categorías y el filtro del listado de preguntas piden también las desactivadas.
    /// </summary>
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(
        bool includeInactive = false, CancellationToken ct = default)
    {
        var categories = await _repo.GetAllAsync(ct);
        var result = new List<CategoryDto>(categories.Count);
        foreach (var c in categories.Where(c => includeInactive || c.IsActive))
            result.Add(await ToDtoAsync(c, ct));
        return result;
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var c = await _repo.GetByIdAsync(id, ct);
        return c is null ? null : await ToDtoAsync(c, ct);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default)
    {
        var (name, description) = Validate(dto.Name, dto.Description);
        await EnsureNameIsFreeAsync(name, exceptId: null, ct);

        var entity = new Category { Name = name, Description = description };
        await _repo.AddAsync(entity, ct);
        return new CategoryDto(entity.Id, entity.Name, entity.Description, 0, entity.IsActive);
    }

    public async Task<CategoryDto?> UpdateAsync(int id, UpdateCategoryDto dto, CancellationToken ct = default)
    {
        // Se valida antes de leer: una edición inválida no debe ni consultar la categoría.
        var (name, description) = Validate(dto.Name, dto.Description);

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null) return null;

        await EnsureNameIsFreeAsync(name, exceptId: id, ct);

        entity.Name = name;
        entity.Description = description;
        if (dto.IsActive is { } active) entity.IsActive = active;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);
        return await ToDtoAsync(entity, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null) return false;
        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);
        return true;
    }

    private async Task<CategoryDto> ToDtoAsync(Category c, CancellationToken ct)
    {
        var questionCount = await _questionRepo.CountAsync(q => q.CategoryId == c.Id && q.IsActive, ct);
        return new CategoryDto(c.Id, c.Name, c.Description, questionCount, c.IsActive);
    }

    private static (string Name, string Description) Validate(string? name, string? description)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new ValidationException("El nombre de la categoría es obligatorio.");
        if (trimmed.Length > NameMaxLength)
            throw new ValidationException($"El nombre no puede pasar de {NameMaxLength} caracteres.");

        var text = description?.Trim() ?? string.Empty;
        if (text.Length > DescriptionMaxLength)
            throw new ValidationException($"La descripción no puede pasar de {DescriptionMaxLength} caracteres.");

        return (trimmed, text);
    }

    /// <summary>
    /// Un nombre usado por otra categoría (sin distinguir mayúsculas) es un 409 con mensaje, no un
    /// fallo del índice único que salía como 500. La propia categoría no cuenta contra sí misma.
    /// </summary>
    private async Task EnsureNameIsFreeAsync(string name, int? exceptId, CancellationToken ct)
    {
        var lower = name.ToLower();
        var clash = await _repo.FindAsync(c => c.Name.ToLower() == lower && (exceptId == null || c.Id != exceptId), ct);
        if (clash.Count > 0)
            throw new ConflictException($"Ya existe una categoría llamada «{clash[0].Name}».");
    }
}
