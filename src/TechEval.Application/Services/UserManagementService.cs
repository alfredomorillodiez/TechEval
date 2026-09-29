using System.Net.Mail;
using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Application.Services;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserDto>> ListAsync(
        UserRole? role, bool? active, string? search, CancellationToken ct = default);

    Task<UserActionResultDto> CreateAsync(CreateUserDto dto, string baseUrl, CancellationToken ct = default);

    Task<UserActionResultDto> ChangeRoleAsync(
        int userId, ChangeRoleDto dto, int actingUserId, string baseUrl, CancellationToken ct = default);

    Task<UserDto> DeactivateAsync(int userId, int actingUserId, CancellationToken ct = default);

    Task<UserDto> ActivateAsync(int userId, CancellationToken ct = default);

    Task<UserActionResultDto> ResetAccessAsync(
        int userId, int actingUserId, string baseUrl, CancellationToken ct = default);
}

/// <summary>
/// Alta, rol, estado y acceso de las cuentas. Spec user-management.
/// </summary>
/// <remarks>
/// Tres reglas protegen la consola de quedarse sin dueño: un administrador no se cambia el
/// rol, no se desactiva y no se restablece el acceso a sí mismo; y siempre queda al menos
/// un administrador activo. Las dos primeras bastan cuando se actúa solo. La tercera cubre
/// a dos administradores que actúan a la vez, cada uno sobre el otro.
///
/// Toda operación que debe retirar el acceso cambia el sello de seguridad, y con eso los
/// tokens ya emitidos dejan de valer en la siguiente petición.
/// </remarks>
public class UserManagementService : IUserManagementService
{
    private readonly IRepository<User> _userRepo;
    private readonly PasswordSetupService _passwordSetup;
    private readonly IAdminCountLock _adminLock;
    private readonly IUnitOfWork _unitOfWork;

    public UserManagementService(
        IRepository<User> userRepo,
        PasswordSetupService passwordSetup,
        IAdminCountLock adminLock,
        IUnitOfWork unitOfWork)
    {
        _userRepo = userRepo;
        _passwordSetup = passwordSetup;
        _adminLock = adminLock;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(
        UserRole? role, bool? active, string? search, CancellationToken ct = default)
    {
        var text = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var users = await _userRepo.FindAsync(u =>
            (role == null || u.Role == role) &&
            (active == null || u.IsActive == active) &&
            (text == null || u.Name.Contains(text) || u.Email.Contains(text)), ct);

        return users
            .OrderBy(u => u.Role)
            .ThenBy(u => u.Name)
            .Select(ToDto)
            .ToList();
    }

    public async Task<UserActionResultDto> CreateAsync(
        CreateUserDto dto, string baseUrl, CancellationToken ct = default)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        var email = dto.Email?.Trim() ?? string.Empty;

        if (name.Length == 0) throw new ValidationException("El nombre es obligatorio.");
        if (!IsValidEmail(email)) throw new ValidationException("El email no tiene un formato válido.");
        if (dto.Role is not (UserRole.Admin or UserRole.Evaluador))
            throw new ValidationException(
                "Solo se crean administradores y evaluadores. Los alumnos nacen al abrir una invitación.");

        if ((await _userRepo.FindAsync(u => u.Email == email, ct)).Count > 0)
            throw new ConflictException($"Ya existe un usuario con el email {email}.");

        var user = new User
        {
            Name = name,
            Email = email,
            Role = dto.Role,
            // Sin contraseña: la fija la propia persona con el enlace. El administrador
            // nunca la conoce.
            PasswordHash = string.Empty,
            IsActive = true
        };

        IssuedPasswordLink link = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            await _userRepo.AddAsync(user, tx);
            link = await _passwordSetup.IssueAsync(user, tx);
        }, ct);

        var sent = await _passwordSetup.TrySendAsync(user, link, baseUrl, ct);
        return new UserActionResultDto(ToDto(user), sent);
    }

    public async Task<UserActionResultDto> ChangeRoleAsync(
        int userId, ChangeRoleDto dto, int actingUserId, string baseUrl, CancellationToken ct = default)
    {
        var user = await GetAsync(userId, ct);
        if (userId == actingUserId)
            throw new ConflictException("No puedes cambiar tu propio rol.");
        if (!RoleTransitions.IsAllowed(user.Role, dto.Role))
            throw new ValidationException(
                $"No se puede pasar de {user.Role} a {dto.Role}. " +
                "Para retirar el acceso a una cuenta, desactívala.");

        IssuedPasswordLink? link = null;
        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            if (user is { Role: UserRole.Admin, IsActive: true })
                await EnsureAnotherActiveAdminAsync(tx);

            user.Role = dto.Role;
            user.RotateSecurityStamp();
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user, tx);

            // El alumno que pasa a otro rol no tiene contraseña: su acceso era la invitación.
            if (user.PasswordHash.Length == 0)
                link = await _passwordSetup.IssueAsync(user, tx);
        }, ct);

        bool? sent = link is null ? null : await _passwordSetup.TrySendAsync(user, link, baseUrl, ct);
        return new UserActionResultDto(ToDto(user), sent);
    }

    public async Task<UserDto> DeactivateAsync(int userId, int actingUserId, CancellationToken ct = default)
    {
        var user = await GetAsync(userId, ct);
        if (userId == actingUserId)
            throw new ConflictException("No puedes desactivar tu propia cuenta.");
        if (!user.IsActive) return ToDto(user);

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            if (user.Role == UserRole.Admin)
                await EnsureAnotherActiveAdminAsync(tx);

            user.IsActive = false;
            // Imprescindible aunque la API ya compruebe IsActive: sin sello nuevo, reactivar
            // la cuenta devolvería la validez a los tokens emitidos antes de desactivarla.
            user.RotateSecurityStamp();
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user, tx);
        }, ct);

        return ToDto(user);
    }

    public async Task<UserDto> ActivateAsync(int userId, CancellationToken ct = default)
    {
        var user = await GetAsync(userId, ct);
        if (user.IsActive) return ToDto(user);

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepo.UpdateAsync(user, ct);
        return ToDto(user);
    }

    public async Task<UserActionResultDto> ResetAccessAsync(
        int userId, int actingUserId, string baseUrl, CancellationToken ct = default)
    {
        var user = await GetAsync(userId, ct);
        if (userId == actingUserId)
            throw new ConflictException("No puedes restablecer tu propio acceso.");
        if (user.Role == UserRole.Alumno)
            throw new ValidationException(
                "Un alumno no tiene contraseña que restablecer: su acceso es el enlace de la invitación.");

        IssuedPasswordLink link = null!;
        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            user.PasswordHash = string.Empty;
            user.RotateSecurityStamp();
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user, tx);
            link = await _passwordSetup.IssueAsync(user, tx);
        }, ct);

        var sent = await _passwordSetup.TrySendAsync(user, link, baseUrl, ct);
        return new UserActionResultDto(ToDto(user), sent);
    }

    /// <summary>
    /// Se llama antes de retirar un administrador activo. El bloqueo pone en fila a quien
    /// haga lo mismo a la vez, y el recuento se lee ya dentro de él.
    /// </summary>
    private async Task EnsureAnotherActiveAdminAsync(CancellationToken ct)
    {
        await _adminLock.AcquireAsync(ct);
        var activeAdmins = await _userRepo.CountAsync(u => u.Role == UserRole.Admin && u.IsActive, ct);
        if (activeAdmins <= 1)
            throw new ConflictException("Debe quedar al menos un administrador activo.");
    }

    private async Task<User> GetAsync(int userId, CancellationToken ct)
        => await _userRepo.GetByIdAsync(userId, ct)
           ?? throw new NotFoundException("Usuario no encontrado.");

    private static bool IsValidEmail(string email)
        => MailAddress.TryCreate(email, out var parsed) && parsed.Address == email;

    private static UserDto ToDto(User u) => new(
        u.Id, u.Name, u.Email, u.Role, u.IsActive, u.CreatedAt,
        AccessPending: u.Role != UserRole.Alumno && u.PasswordHash.Length == 0);
}
