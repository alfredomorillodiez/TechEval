using TechEval.Domain.Common;

namespace TechEval.Domain.Entities;

public class Category : AuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
