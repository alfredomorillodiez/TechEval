using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Xunit;
using TechEval.Application;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;
using TechEval.Domain.Interfaces.Services;

namespace TechEval.Tests.Services;

/// <summary>
/// Spec exam-delivery — Las invitaciones son solo para alumnos; y spec authentication —
/// Aprovisionamiento automático de cuenta de alumno, con correos de otros roles o de
/// alumnos desactivados.
/// </summary>
public class InvitationRoleTests
{
    private readonly Mock<IExamTokenRepository> _tokenRepo = new();
    private readonly Mock<IExamRepository> _examRepo = new();
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly List<User> _usuarios = new();
    private readonly ExamTokenService _sut;

    public InvitationRoleTests()
    {
        _userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<User, bool>> p, CancellationToken _) =>
                _usuarios.Where(p.Compile()).ToList());
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => { u.Id = 100 + _usuarios.Count; _usuarios.Add(u); return u; });

        _examRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Exam { Id = 7, Title = "Prueba" });
        _tokens.Setup(t => t.GenerateSecureToken()).Returns("tok");
        _tokens.Setup(t => t.GenerateJwtToken(It.IsAny<User>())).Returns("jwt");

        _sut = new ExamTokenService(
            _tokenRepo.Object, _examRepo.Object,
            new Mock<IRepository<ExamSession>>().Object, new Mock<IRepository<UserAnswer>>().Object,
            new Mock<IExamResultRepository>().Object, _userRepo.Object,
            _email.Object, _tokens.Object, new FakeUnitOfWork());
    }

    private void ConUsuario(string email, UserRole rol, bool activo = true) => _usuarios.Add(new User
    {
        Id = _usuarios.Count + 1, Email = email, Name = email, Role = rol, IsActive = activo
    });

    private void ConInvitacionPara(string email) =>
        _tokenRepo.Setup(r => r.GetWithExamAndSessionAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExamToken
            {
                Id = 1, Token = "tok", ExamId = 7, Exam = new Exam { Id = 7, Title = "Prueba" },
                CandidateName = "Laura", CandidateEmail = email,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });

    [Theory]
    [InlineData(UserRole.Evaluador)]
    [InlineData(UserRole.Admin)]
    public async Task Invitacion_individual_a_otro_rol_se_rechaza_sin_token_ni_correo(UserRole rol)
    {
        ConUsuario("laura@test.com", rol);

        var enviar = () => _sut.SendExamAsync(new SendExamDto(7, "Laura", "laura@test.com"), "http://web");

        await enviar.Should().ThrowAsync<ValidationException>();
        _tokenRepo.Verify(r => r.AddAsync(It.IsAny<ExamToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Envio_masivo_rechaza_solo_al_candidato_de_otro_rol()
    {
        ConUsuario("admin@test.com", UserRole.Admin);
        var lista = new List<BulkCandidateDto>
        {
            new("Ana", "ana@test.com"), new("Admin", "admin@test.com"), new("Luis", "luis@test.com")
        };

        var resultado = await _sut.SendExamBulkAsync(new BulkSendExamDto(7, lista), "http://web");

        resultado.Sent.Should().Be(2);
        resultado.Failed.Should().Be(1);
        resultado.Results[1].Success.Should().BeFalse();
        resultado.Results[1].Error.Should().Contain("no de alumno");
        _email.Verify(e => e.SendExamInvitationAsync(
            "admin@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invitacion_a_un_email_nuevo_se_envia_con_normalidad()
    {
        await _sut.SendExamAsync(new SendExamDto(7, "Ana", "ana@test.com"), "http://web");

        _tokenRepo.Verify(r => r.AddAsync(It.IsAny<ExamToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Abrir_una_invitacion_de_un_email_de_evaluador_no_emite_token()
    {
        ConUsuario("laura@test.com", UserRole.Evaluador);
        ConInvitacionPara("laura@test.com");

        var validacion = await _sut.ValidateTokenAsync("tok");

        validacion.IsValid.Should().BeFalse();
        validacion.AuthToken.Should().BeNull();
        _tokens.Verify(t => t.GenerateJwtToken(It.IsAny<User>()), Times.Never);
        _usuarios.Single().Role.Should().Be(UserRole.Evaluador, "el usuario no se modifica");
    }

    [Fact]
    public async Task Abrir_una_invitacion_de_un_alumno_desactivado_no_emite_token()
    {
        ConUsuario("ana@test.com", UserRole.Alumno, activo: false);
        ConInvitacionPara("ana@test.com");

        var validacion = await _sut.ValidateTokenAsync("tok");

        validacion.IsValid.Should().BeFalse();
        _tokens.Verify(t => t.GenerateJwtToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Primera_apertura_crea_el_alumno_con_rol_Alumno()
    {
        ConInvitacionPara("nuevo@test.com");

        var validacion = await _sut.ValidateTokenAsync("tok");

        validacion.IsValid.Should().BeTrue();
        _usuarios.Should().ContainSingle(u => u.Email == "nuevo@test.com" && u.Role == UserRole.Alumno
            && u.PasswordHash == string.Empty);
    }
}
