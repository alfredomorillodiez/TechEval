using TechEval.Domain.Common;

namespace TechEval.Domain.Entities;

public class User : AuditableEntity
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Exam> CreatedExams { get; set; } = new List<Exam>();
}
