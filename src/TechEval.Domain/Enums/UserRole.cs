namespace TechEval.Domain.Enums;

/// <summary>
/// Un solo rol por usuario. Los nombres son también los valores del claim de rol del JWT
/// (`Role.ToString()`), así que renombrar un miembro invalida los tokens y las políticas.
/// </summary>
public enum UserRole
{
    Admin = 1,
    Evaluador = 2,
    Alumno = 3
}
