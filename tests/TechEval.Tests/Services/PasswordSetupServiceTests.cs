using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
/// Spec user-management — Enlace de un solo uso para fijar la contraseña, y Fijación de la
/// contraseña.
/// </summary>
public class PasswordSetupServiceTests
{
    private const string Valida = "caballo-bateria-grapa";

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"pwd-{Guid.NewGuid()}").Options);
    private readonly Mock<IEmailService> _email = new();
    private readonly PasswordSetupService _sut;
    private readonly User _laura;

    public PasswordSetupServiceTests()
    {
        // El generador real: la prueba de que no se guarda en claro necesita tokens de verdad.
        var tokens = new TokenService(Microsoft.Extensions.Options.Options.Create(new JwtSettings()));
        _sut = new PasswordSetupService(
            new BaseRepository<PasswordSetupToken>(_db), new BaseRepository<User>(_db),
            tokens, _email.Object, new FakeUnitOfWork());

        _laura = new User { Email = "laura@test.com", Name = "Laura", Role = UserRole.Evaluador };
        _db.Users.Add(_laura);
        _db.SaveChanges();
    }

    [Fact]
    public async Task El_token_no_se_guarda_en_claro()
    {
        var enlace = await _sut.IssueAsync(_laura);

        var guardado = _db.PasswordSetupTokens.Single();
        guardado.TokenHash.Should().NotBe(enlace.Token);
        guardado.TokenHash.Should().Be(PasswordSetupService.HashToken(enlace.Token));
        guardado.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(48), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Comprobacion_de_un_enlace_vigente_devuelve_nombre_y_email()
    {
        var enlace = await _sut.IssueAsync(_laura);

        var info = await _sut.CheckAsync(enlace.Token);

        info.Should().Be(new PasswordSetupInfoDto("Laura", "laura@test.com"));
    }

    [Fact]
    public async Task Un_enlace_nuevo_invalida_el_anterior()
    {
        var primero = await _sut.IssueAsync(_laura);
        var segundo = await _sut.IssueAsync(_laura);

        await FluentActions.Invoking(() => _sut.CheckAsync(primero.Token))
            .Should().ThrowAsync<NotFoundException>();
        (await _sut.CheckAsync(segundo.Token)).Email.Should().Be("laura@test.com");
    }

    [Fact]
    public async Task Enlace_caducado_da_la_respuesta_generica()
    {
        var enlace = await _sut.IssueAsync(_laura);
        _db.PasswordSetupTokens.Single().ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        await FluentActions.Invoking(() => _sut.SetPasswordAsync(new SetPasswordDto(enlace.Token, Valida)))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Enlace_inexistente_da_la_misma_respuesta_generica()
    {
        var enlace = await _sut.IssueAsync(_laura);
        _db.PasswordSetupTokens.Single().ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        var caducado = await Capturar(() => _sut.CheckAsync(enlace.Token));
        var inexistente = await Capturar(() => _sut.CheckAsync("no-existe"));

        inexistente.Message.Should().Be(caducado.Message);
    }

    [Fact]
    public async Task Enlace_ya_usado_no_vale_otra_vez_y_la_contrasena_no_cambia()
    {
        var enlace = await _sut.IssueAsync(_laura);
        await _sut.SetPasswordAsync(new SetPasswordDto(enlace.Token, Valida));
        var hash = _laura.PasswordHash;

        await FluentActions.Invoking(() => _sut.SetPasswordAsync(new SetPasswordDto(enlace.Token, "otra-contrasena-larga")))
            .Should().ThrowAsync<NotFoundException>();
        _laura.PasswordHash.Should().Be(hash);
    }

    [Fact]
    public async Task Enlace_de_una_cuenta_desactivada_no_vale()
    {
        var enlace = await _sut.IssueAsync(_laura);
        _laura.IsActive = false;
        await _db.SaveChangesAsync();

        await FluentActions.Invoking(() => _sut.SetPasswordAsync(new SetPasswordDto(enlace.Token, Valida)))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Fijar_la_contrasena_guarda_el_hash_marca_el_enlace_y_cambia_el_sello()
    {
        var enlace = await _sut.IssueAsync(_laura);
        var selloAnterior = _laura.SecurityStamp;

        await _sut.SetPasswordAsync(new SetPasswordDto(enlace.Token, Valida));

        PasswordHasher.Verify(Valida, _laura.PasswordHash).IsValid.Should().BeTrue();
        _laura.SecurityStamp.Should().NotBe(selloAnterior);
        _db.PasswordSetupTokens.Single().UsedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(11)]
    [InlineData(129)]
    public async Task Contrasena_fuera_de_longitud_se_rechaza_y_el_enlace_sigue_valiendo(int longitud)
    {
        var enlace = await _sut.IssueAsync(_laura);

        await FluentActions.Invoking(() => _sut.SetPasswordAsync(new SetPasswordDto(enlace.Token, new string('a', longitud))))
            .Should().ThrowAsync<ValidationException>();
        _laura.PasswordHash.Should().BeEmpty();
        (await _sut.CheckAsync(enlace.Token)).Email.Should().Be("laura@test.com");
    }

    [Fact]
    public async Task Sin_reglas_de_composicion()
    {
        var enlace = await _sut.IssueAsync(_laura);

        await _sut.SetPasswordAsync(new SetPasswordDto(enlace.Token, "todo minusculas y espacios"));

        _laura.PasswordHash.Should().NotBeEmpty();
    }

    [Fact]
    public async Task El_fallo_del_correo_devuelve_false_sin_lanzar()
    {
        _email.Setup(e => e.SendPasswordSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP caído"));
        var enlace = await _sut.IssueAsync(_laura);

        var enviado = await _sut.TrySendAsync(_laura, enlace, "http://web");

        enviado.Should().BeFalse();
    }

    [Fact]
    public async Task El_correo_lleva_el_enlace_con_la_ruta_de_la_pagina()
    {
        var enlace = await _sut.IssueAsync(_laura);

        (await _sut.TrySendAsync(_laura, enlace, "http://web")).Should().BeTrue();

        _email.Verify(e => e.SendPasswordSetupAsync("laura@test.com", "Laura",
            $"http://web/fijar-contrasena/{enlace.Token}", enlace.ExpiresAt, It.IsAny<CancellationToken>()));
    }

    private static async Task<Exception> Capturar(Func<Task> accion)
    {
        try { await accion(); }
        catch (Exception ex) { return ex; }
        throw new InvalidOperationException("Se esperaba una excepción.");
    }
}
