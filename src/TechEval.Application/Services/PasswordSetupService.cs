using System.Security.Cryptography;
using System.Text;
using TechEval.Application.DTOs;
using TechEval.Domain.Entities;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Application.Services;

public interface IPasswordSetupService
{
    /// <summary>Nombre y email del dueño de un enlace vigente.</summary>
    /// <exception cref="NotFoundException">El enlace no vale, por el motivo que sea.</exception>
    Task<PasswordSetupInfoDto> CheckAsync(string token, CancellationToken ct = default);

    /// <exception cref="NotFoundException">El enlace no vale, por el motivo que sea.</exception>
    /// <exception cref="ValidationException">La contraseña no cumple la longitud.</exception>
    Task SetPasswordAsync(SetPasswordDto dto, CancellationToken ct = default);
}

/// <summary>Un enlace recién emitido. El token solo existe aquí y en el correo.</summary>
public record IssuedPasswordLink(string Token, DateTime ExpiresAt);

/// <summary>
/// Enlaces de un solo uso para que un administrador o un evaluador fije su contraseña.
/// </summary>
/// <remarks>
/// Un enlace inexistente, caducado, usado o de una cuenta desactivada dan la misma
/// respuesta. Distinguirlos solo le sirve a quien prueba enlaces que no son suyos.
/// </remarks>
public class PasswordSetupService : IPasswordSetupService
{
    public const int ExpirationHours = 48;
    public const int MinPasswordLength = 12;
    public const int MaxPasswordLength = 128;

    private const string InvalidLink = "El enlace no es válido o ha caducado. Pide uno nuevo al administrador.";

    private readonly IRepository<PasswordSetupToken> _linkRepo;
    private readonly IRepository<User> _userRepo;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public PasswordSetupService(
        IRepository<PasswordSetupToken> linkRepo,
        IRepository<User> userRepo,
        ITokenService tokenService,
        IEmailService emailService,
        IUnitOfWork unitOfWork)
    {
        _linkRepo = linkRepo;
        _userRepo = userRepo;
        _tokenService = tokenService;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    public static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>
    /// Emite un enlace nuevo y borra los no usados del mismo usuario, que dejan de valer.
    /// Se llama dentro de la transacción de quien lo pide; el correo va después, fuera.
    /// </summary>
    public async Task<IssuedPasswordLink> IssueAsync(User user, CancellationToken ct = default)
    {
        var pending = await _linkRepo.FindAsync(l => l.UserId == user.Id && l.UsedAt == null, ct);
        foreach (var old in pending)
            await _linkRepo.DeleteAsync(old, ct);

        var token = _tokenService.GenerateSecureToken();
        var now = DateTime.UtcNow;
        await _linkRepo.AddAsync(new PasswordSetupToken
        {
            UserId = user.Id,
            TokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = now.AddHours(ExpirationHours)
        }, ct);

        return new IssuedPasswordLink(token, now.AddHours(ExpirationHours));
    }

    /// <summary>Envía el enlace. False si el correo no salió; el fallo ya queda en el log.</summary>
    public async Task<bool> TrySendAsync(
        User user, IssuedPasswordLink link, string baseUrl, CancellationToken ct = default)
    {
        try
        {
            await _emailService.SendPasswordSetupAsync(
                user.Email, user.Name, $"{baseUrl}/fijar-contrasena/{link.Token}", link.ExpiresAt, ct);
            return true;
        }
        catch
        {
            // Silenciado a propósito: la cuenta ya está escrita y es la que manda. El
            // administrador ve EmailSent = false y puede restablecer el acceso.
            return false;
        }
    }

    public async Task<PasswordSetupInfoDto> CheckAsync(string token, CancellationToken ct = default)
    {
        var (_, user) = await FindUsableAsync(token, ct);
        return new PasswordSetupInfoDto(user.Name, user.Email);
    }

    public async Task SetPasswordAsync(SetPasswordDto dto, CancellationToken ct = default)
    {
        // El enlace antes que la contraseña: un error de longitud solo lo ve quien tiene un
        // enlace válido.
        var (link, user) = await FindUsableAsync(dto.Token, ct);

        var length = dto.Password?.Length ?? 0;
        if (length < MinPasswordLength || length > MaxPasswordLength)
            throw new ValidationException(
                $"La contraseña debe tener entre {MinPasswordLength} y {MaxPasswordLength} caracteres.");

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            user.PasswordHash = PasswordHasher.Hash(dto.Password!);
            user.RotateSecurityStamp();
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user, tx);

            link.UsedAt = DateTime.UtcNow;
            await _linkRepo.UpdateAsync(link, tx);
        }, ct);
    }

    private async Task<(PasswordSetupToken Link, User User)> FindUsableAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new NotFoundException(InvalidLink);

        var hash = HashToken(token);
        var link = (await _linkRepo.FindAsync(l => l.TokenHash == hash, ct)).FirstOrDefault();
        if (link is null || !link.IsUsable(DateTime.UtcNow)) throw new NotFoundException(InvalidLink);

        var user = await _userRepo.GetByIdAsync(link.UserId, ct);
        if (user is null || !user.IsActive) throw new NotFoundException(InvalidLink);

        return (link, user);
    }
}
