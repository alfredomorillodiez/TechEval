namespace TechEval.Domain.Entities;

/// <summary>
/// Un evaluador asignado a una prueba: corrige los resultados pendientes de esa prueba.
/// </summary>
/// <remarks>
/// La clave es la pareja (prueba, usuario), así que la base impide la asignación duplicada
/// sin código. Quitar la asignación no toca las correcciones hechas: esas las guarda
/// <see cref="ExamResult.ReviewedByUserId"/>.
/// </remarks>
public class ExamEvaluator
{
    public int ExamId { get; set; }
    public int UserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public int AssignedByUserId { get; set; }

    public Exam Exam { get; set; } = null!;
    public User User { get; set; } = null!;
}
