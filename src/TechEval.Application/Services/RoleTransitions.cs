using TechEval.Domain.Enums;

namespace TechEval.Application.Services;

/// <summary>
/// Qué cambios de rol se permiten. Spec user-management — Cambio de rol.
/// </summary>
/// <remarks>
/// Nadie pasa a Alumno. Para quitarle el acceso a un administrador o a un evaluador se
/// desactiva la cuenta: convertirlo en alumno le dejaría la contraseña, y con ella la
/// entrada al portal. El paso contrario sí se permite, para el empleado que hizo una
/// prueba y ahora corrige.
/// </remarks>
public static class RoleTransitions
{
    public static bool IsAllowed(UserRole from, UserRole to) => (from, to) switch
    {
        (UserRole.Admin, UserRole.Evaluador) => true,
        (UserRole.Evaluador, UserRole.Admin) => true,
        (UserRole.Alumno, UserRole.Admin) => true,
        (UserRole.Alumno, UserRole.Evaluador) => true,
        _ => false
    };
}
