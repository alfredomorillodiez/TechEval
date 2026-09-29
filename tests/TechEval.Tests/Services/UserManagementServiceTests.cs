using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using TechEval.Application;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Services;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Repositories;
using TechEval.Infrastructure.Security;

namespace TechEval.Tests.Services;

/// <summary>
/// Spec user-management. La concurrencia entre dos administradores no se prueba aquí: la
/// base en memoria no tiene transacciones ni bloqueos. Se prueba contra SQL Server.
/// </summary>
public class UserManagementServiceTests
{
    private const string Web = "http://web";

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"users-{Guid.NewGuid()}").Options);
    private readonly Mock<IEmailService> _email = new();
    private readonly UserManagementService _sut;

    private readonly User _admin;
    private readonly User _evaluador;
    private readonly User _alumno;

    public UserManagementServiceTests()
    {
        var users = new BaseRepository<User>(_db);
        var passwordSetup = new PasswordSetupService(
            new BaseRepository<PasswordSetupToken>(_db), users,
            new TokenService(Options.Create(new JwtSettings())), _email.Object, new FakeUnitOfWork());
        _sut = new UserManagementService(users, passwordSetup, new AdminCountLock(_db), new FakeUnitOfWork(),
            new BaseRepository<ExamEvaluator>(_db), new ExamResultRepository(_db));

        _admin = Nuevo("admin@test.com", UserRole.Admin, "hash-admin");
        _evaluador = Nuevo("laura@test.com", UserRole.Evaluador, "hash-laura");
        _alumno = Nuevo("ana@test.com", UserRole.Alumno, string.Empty);
        _db.SaveChanges();
    }

    private User Nuevo(string email, UserRole rol, string hash)
    {
        var u = new User { Email = email, Name = email.Split('@')[0], Role = rol, PasswordHash = hash };
        _db.Users.Add(u);
        return u;
    }

    private void CorreoCaido() => _email
        .Setup(e => e.SendPasswordSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("SMTP caído"));

    private void CorreoEnviado(string email, Times veces) => _email.Verify(e => e.SendPasswordSetupAsync(
        email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), veces);

    // ---- Tabla de transiciones ------------------------------------------------------

    [Theory]
    [InlineData(UserRole.Admin, UserRole.Evaluador, true)]
    [InlineData(UserRole.Evaluador, UserRole.Admin, true)]
    [InlineData(UserRole.Alumno, UserRole.Admin, true)]
    [InlineData(UserRole.Alumno, UserRole.Evaluador, true)]
    [InlineData(UserRole.Admin, UserRole.Alumno, false)]
    [InlineData(UserRole.Evaluador, UserRole.Alumno, false)]
    [InlineData(UserRole.Admin, UserRole.Admin, false)]
    [InlineData(UserRole.Evaluador, UserRole.Evaluador, false)]
    [InlineData(UserRole.Alumno, UserRole.Alumno, false)]
    public void Transiciones_de_rol(UserRole desde, UserRole hacia, bool permitida)
        => RoleTransitions.IsAllowed(desde, hacia).Should().Be(permitida);

    // ---- Listado ---------------------------------------------------------------------

    [Fact]
    public async Task Listado_sin_filtros_trae_a_todos()
        => (await _sut.ListAsync(null, null, null)).Should().HaveCount(3);

    [Fact]
    public async Task Filtro_por_rol()
        => (await _sut.ListAsync(UserRole.Evaluador, null, null)).Should().ContainSingle()
            .Which.Email.Should().Be("laura@test.com");

    [Fact]
    public async Task Filtro_por_texto_en_nombre_o_email()
        => (await _sut.ListAsync(null, null, "ana")).Should().ContainSingle()
            .Which.Role.Should().Be(UserRole.Alumno);

    [Fact]
    public async Task El_alumno_sin_contrasena_no_tiene_acceso_pendiente()
    {
        var lista = await _sut.ListAsync(null, null, null);

        lista.Single(u => u.Role == UserRole.Alumno).AccessPending.Should().BeFalse();
        lista.Single(u => u.Role == UserRole.Evaluador).AccessPending.Should().BeFalse();
    }

    // ---- Alta ------------------------------------------------------------------------

    [Fact]
    public async Task Alta_de_un_evaluador_nace_activa_pendiente_y_con_enlace_enviado()
    {
        var r = await _sut.CreateAsync(new CreateUserDto("Luis Gil", "luis@test.com", UserRole.Evaluador), Web);

        r.EmailSent.Should().BeTrue();
        r.User.Should().Match<UserDto>(u => u.IsActive && u.AccessPending && u.Role == UserRole.Evaluador);
        _db.PasswordSetupTokens.Should().ContainSingle(t => t.UserId == r.User.Id);
        CorreoEnviado("luis@test.com", Times.Once());
    }

    [Fact]
    public async Task Alta_con_email_repetido_es_409()
        => await FluentActions.Invoking(() =>
                _sut.CreateAsync(new CreateUserDto("Otra", "ana@test.com", UserRole.Evaluador), Web))
            .Should().ThrowAsync<ConflictException>();

    [Fact]
    public async Task Alta_con_rol_Alumno_es_400()
        => await FluentActions.Invoking(() =>
                _sut.CreateAsync(new CreateUserDto("Luis", "luis@test.com", UserRole.Alumno), Web))
            .Should().ThrowAsync<ValidationException>();

    [Theory]
    [InlineData("", "luis@test.com")]
    [InlineData("Luis", "")]
    [InlineData("Luis", "no-es-un-email")]
    [InlineData("Luis", "Luis <luis@test.com>")]
    public async Task Alta_sin_datos_validos_es_400(string nombre, string email)
        => await FluentActions.Invoking(() =>
                _sut.CreateAsync(new CreateUserDto(nombre, email, UserRole.Evaluador), Web))
            .Should().ThrowAsync<ValidationException>();

    [Fact]
    public async Task Fallo_del_correo_en_el_alta_deja_la_cuenta_creada()
    {
        CorreoCaido();

        var r = await _sut.CreateAsync(new CreateUserDto("Luis", "luis@test.com", UserRole.Evaluador), Web);

        r.EmailSent.Should().BeFalse();
        _db.Users.Should().Contain(u => u.Email == "luis@test.com");
    }

    // ---- Cambio de rol ---------------------------------------------------------------

    [Fact]
    public async Task Evaluador_ascendido_cambia_de_sello_y_no_recibe_correo()
    {
        var sello = _evaluador.SecurityStamp;

        var r = await _sut.ChangeRoleAsync(_evaluador.Id, new ChangeRoleDto(UserRole.Admin), _admin.Id, Web);

        r.User.Role.Should().Be(UserRole.Admin);
        r.EmailSent.Should().BeNull();
        _evaluador.SecurityStamp.Should().NotBe(sello);
    }

    [Fact]
    public async Task Alumno_convertido_en_evaluador_recibe_el_enlace()
    {
        var r = await _sut.ChangeRoleAsync(_alumno.Id, new ChangeRoleDto(UserRole.Evaluador), _admin.Id, Web);

        r.User.AccessPending.Should().BeTrue();
        r.EmailSent.Should().BeTrue();
        CorreoEnviado("ana@test.com", Times.Once());
    }

    [Fact]
    public async Task Paso_a_Alumno_es_400_y_el_rol_no_cambia()
    {
        await FluentActions.Invoking(() =>
                _sut.ChangeRoleAsync(_evaluador.Id, new ChangeRoleDto(UserRole.Alumno), _admin.Id, Web))
            .Should().ThrowAsync<ValidationException>();
        _evaluador.Role.Should().Be(UserRole.Evaluador);
    }

    [Fact]
    public async Task Cambio_del_propio_rol_es_409()
        => await FluentActions.Invoking(() =>
                _sut.ChangeRoleAsync(_admin.Id, new ChangeRoleDto(UserRole.Evaluador), _admin.Id, Web))
            .Should().ThrowAsync<ConflictException>();

    [Fact]
    public async Task Retirar_el_rol_al_ultimo_administrador_activo_es_409()
    {
        // Un segundo administrador inactivo no cuenta.
        var otro = Nuevo("otro@test.com", UserRole.Admin, "h");
        otro.IsActive = false;
        await _db.SaveChangesAsync();

        // Actúa un administrador que no es él mismo; el caso real es la concurrencia, en el
        // que el otro ya ha dejado de serlo.
        await FluentActions.Invoking(() =>
                _sut.ChangeRoleAsync(_admin.Id, new ChangeRoleDto(UserRole.Evaluador), otro.Id, Web))
            .Should().ThrowAsync<ConflictException>();
        _admin.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task Usuario_inexistente_es_404()
        => await FluentActions.Invoking(() =>
                _sut.ChangeRoleAsync(999, new ChangeRoleDto(UserRole.Admin), _admin.Id, Web))
            .Should().ThrowAsync<NotFoundException>();

    // ---- Desactivación y reactivación ------------------------------------------------

    [Fact]
    public async Task Desactivar_cambia_el_sello_y_reactivar_no_devuelve_el_anterior()
    {
        var original = _evaluador.SecurityStamp;

        await _sut.DeactivateAsync(_evaluador.Id, _admin.Id);
        var trasDesactivar = _evaluador.SecurityStamp;
        await _sut.ActivateAsync(_evaluador.Id);

        trasDesactivar.Should().NotBe(original);
        _evaluador.IsActive.Should().BeTrue();
        _evaluador.SecurityStamp.Should().NotBe(original, "los tokens anteriores a la desactivación siguen sin valer");
    }

    [Fact]
    public async Task Desactivacion_propia_es_409()
    {
        await FluentActions.Invoking(() => _sut.DeactivateAsync(_admin.Id, _admin.Id))
            .Should().ThrowAsync<ConflictException>();
        _admin.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Desactivar_al_ultimo_administrador_activo_es_409()
    {
        var otro = Nuevo("otro@test.com", UserRole.Admin, "h");
        otro.IsActive = false;
        await _db.SaveChangesAsync();

        await FluentActions.Invoking(() => _sut.DeactivateAsync(_admin.Id, otro.Id))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Desactivar_a_un_administrador_con_otro_activo_se_permite()
    {
        var otro = Nuevo("otro@test.com", UserRole.Admin, "h");
        await _db.SaveChangesAsync();

        var r = await _sut.DeactivateAsync(otro.Id, _admin.Id);

        r.IsActive.Should().BeFalse();
    }

    // ---- Restablecimiento del acceso ------------------------------------------------

    [Fact]
    public async Task Restablecer_deja_sin_contrasena_cambia_el_sello_y_envia_un_enlace_nuevo()
    {
        var sello = _evaluador.SecurityStamp;

        var r = await _sut.ResetAccessAsync(_evaluador.Id, _admin.Id, Web);

        _evaluador.PasswordHash.Should().BeEmpty();
        _evaluador.SecurityStamp.Should().NotBe(sello);
        r.User.AccessPending.Should().BeTrue();
        r.EmailSent.Should().BeTrue();
    }

    [Fact]
    public async Task Reenvio_tras_fallo_del_correo_invalida_el_enlace_del_alta()
    {
        CorreoCaido();
        var alta = await _sut.CreateAsync(new CreateUserDto("Luis", "luis@test.com", UserRole.Evaluador), Web);
        var enlaceDelAlta = _db.PasswordSetupTokens.Single(t => t.UserId == alta.User.Id).TokenHash;

        await _sut.ResetAccessAsync(alta.User.Id, _admin.Id, Web);

        _db.PasswordSetupTokens.Where(t => t.UserId == alta.User.Id).Should().ContainSingle()
            .Which.TokenHash.Should().NotBe(enlaceDelAlta);
    }

    [Fact]
    public async Task Restablecer_a_un_alumno_es_400_sin_correo()
    {
        await FluentActions.Invoking(() => _sut.ResetAccessAsync(_alumno.Id, _admin.Id, Web))
            .Should().ThrowAsync<ValidationException>();
        _email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Restablecimiento_propio_es_409()
    {
        await FluentActions.Invoking(() => _sut.ResetAccessAsync(_admin.Id, _admin.Id, Web))
            .Should().ThrowAsync<ConflictException>();
        _admin.PasswordHash.Should().Be("hash-admin");
    }
}
