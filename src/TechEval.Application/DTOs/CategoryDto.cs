namespace TechEval.Application.DTOs;

/// <summary>`IsActive` va al final y con valor por defecto: las construcciones anteriores no cambian.</summary>
public record CategoryDto(int Id, string Name, string Description, int QuestionCount, bool IsActive = true);

public record CreateCategoryDto(string Name, string Description);

/// <summary>`IsActive` nulo conserva el estado; con valor, lo cambia (así se reactiva una categoría).</summary>
public record UpdateCategoryDto(string Name, string Description, bool? IsActive = null);
