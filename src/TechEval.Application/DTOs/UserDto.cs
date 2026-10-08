using TechEval.Domain.Enums;

namespace TechEval.Application.DTOs;

/// <summary>
/// Un usuario, visto por un administrador. Sin hash, sin sello y sin enlaces a propósito:
/// nada de esto sirve para gestionar la cuenta y todo sirve para suplantarla.
/// </summary>
/// <param name="AccessPending">
/// Un administrador o un evaluador que todavía no ha fijado su contraseña. Un alumno nunca
/// está pendiente: su acceso es el enlace de la invitación.
/// </param>
public record UserDto(
    int Id,
    string Name,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    bool AccessPending);

public record CreateUserDto(string Name, string Email, UserRole Role);

public record ChangeRoleDto(UserRole Role);

/// <summary>
/// Respuesta de las operaciones que envían un enlace para fijar la contraseña.
/// </summary>
/// <param name="EmailSent">
/// False si el correo no salió. La operación queda hecha igualmente; el administrador puede
/// restablecer el acceso para reenviar el enlace. Null si la operación no envía correo, como
/// el paso de evaluador a administrador, que conserva la contraseña.
/// </param>
public record UserActionResultDto(UserDto User, bool? EmailSent);

public record PasswordSetupInfoDto(string Name, string Email);

public record SetPasswordDto(string Token, string Password);
