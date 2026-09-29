using TechEval.Domain.Common;
using TechEval.Domain.Enums;

namespace TechEval.Domain.Entities;

public class User : AuditableEntity
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Alumno;
    public bool IsActive { get; set; } = true;

    // Viaja en el JWT y la API lo compara en cada petición. Cambiarlo invalida en el acto
    // todos los tokens del usuario, sin esperar a que caduquen.
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    public ICollection<Exam> CreatedExams { get; set; } = new List<Exam>();

    public void RotateSecurityStamp() => SecurityStamp = Guid.NewGuid();
}
