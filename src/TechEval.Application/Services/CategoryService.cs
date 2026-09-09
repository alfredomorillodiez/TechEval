using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Application.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default);
    Task<CategoryDto?> UpdateAsync(int id, UpdateCategoryDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class CategoryService : ICategoryService
{
    private readonly IRepository<Category> _repo;
    private readonly IQuestionRepository _questionRepo;

    public CategoryService(IRepository<Category> repo, IQuestionRepository questionRepo)
    {
        _repo = repo;
        _questionRepo = questionRepo;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _repo.GetAllAsync(ct);
        var result = new List<CategoryDto>(categories.Count);
        foreach (var c in categories)
        {
            var questionCount = await _questionRepo.CountAsync(q => q.CategoryId == c.Id && q.IsActive, ct);
            result.Add(new CategoryDto(c.Id, c.Name, c.Description, questionCount));
        }
        return result;
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var c = await _repo.GetByIdAsync(id, ct);
        if (c is null) return null;
        var questionCount = await _questionRepo.CountAsync(q => q.CategoryId == c.Id && q.IsActive, ct);
        return new CategoryDto(c.Id, c.Name, c.Description, questionCount);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default)
    {
        var entity = new Category { Name = dto.Name, Description = dto.Description };
        await _repo.AddAsync(entity, ct);
        return new CategoryDto(entity.Id, entity.Name, entity.Description, 0);
    }

    public async Task<CategoryDto?> UpdateAsync(int id, UpdateCategoryDto dto, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null) return null;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);
        var questionCount = await _questionRepo.CountAsync(q => q.CategoryId == entity.Id && q.IsActive, ct);
        return new CategoryDto(entity.Id, entity.Name, entity.Description, questionCount);
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
}
