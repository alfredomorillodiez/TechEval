using FluentAssertions;
using Xunit;
using TechEval.Application;

namespace TechEval.Tests.Services;

/// <summary>
/// Importación de candidatos del envío masivo. Un CSV de Excel en castellano separa con punto y
/// coma, y antes importaba 0 candidatos sin decir por qué.
/// </summary>
public class CandidateCsvParserTests
{
    [Theory]
    [InlineData("Juan García;juan@empresa.com")]
    [InlineData("Juan García,juan@empresa.com")]
    [InlineData("Juan García\tjuan@empresa.com")]
    public void AceptaPuntoYComaComaYTabulador(string linea)
    {
        var r = CandidateCsvParser.Parse(linea);

        r.Issues.Should().BeEmpty();
        r.Candidates.Should().ContainSingle().Which.Should()
            .BeEquivalentTo(new { Name = "Juan García", Email = "juan@empresa.com" });
    }

    [Fact]
    public void ImportaVariasLineasConFinesDeLineaDeWindows()
    {
        var r = CandidateCsvParser.Parse("Ana;ana@x.com\r\nLuis;luis@x.com\r\n");

        r.Candidates.Select(c => c.Email).Should().Equal("ana@x.com", "luis@x.com");
        r.Issues.Should().BeEmpty();
    }

    [Fact]
    public void IgnoraElEncabezadoSinContarloComoError()
    {
        var r = CandidateCsvParser.Parse("Nombre;Email\nAna;ana@x.com");

        r.Candidates.Should().ContainSingle();
        r.Issues.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Nombre,Correo")]
    [InlineData("Nombre;E-mail")]
    public void ReconocePalabrasHabitualesDeEncabezado(string cabecera)
        => CandidateCsvParser.Parse($"{cabecera}\nAna;ana@x.com").Issues.Should().BeEmpty();

    [Fact]
    public void IgnoraLasLineasVaciasYQuitaElBOM()
    {
        var r = CandidateCsvParser.Parse("﻿\n\nAna;ana@x.com\n\n   \n");

        r.Candidates.Should().ContainSingle();
        r.Issues.Should().BeEmpty();
    }

    [Fact]
    public void NombreConComaEntreComillas()
    {
        var r = CandidateCsvParser.Parse("\"García, Juan\",juan@empresa.com");

        r.Issues.Should().BeEmpty();
        r.Candidates.Single().Name.Should().Be("García, Juan");
        r.Candidates.Single().Email.Should().Be("juan@empresa.com");
    }

    [Fact]
    public void QuitaLasComillasDelCorreo()
    {
        var r = CandidateCsvParser.Parse("\"Ana\";\"ana@x.com\"");

        r.Candidates.Should().ContainSingle().Which.Email.Should().Be("ana@x.com");
    }

    [Fact]
    public void UnCorreoInvalidoSeIgnoraConSuNumeroDeLinea()
    {
        var r = CandidateCsvParser.Parse("Ana;ana@x.com\nLuis;luis-sin-arroba\nMaría;maria@x.com");

        r.Candidates.Select(c => c.Name).Should().Equal("Ana", "María");
        r.Issues.Should().ContainSingle().Which.Line.Should().Be(2);
        r.Issues.Single().Reason.Should().Contain("luis-sin-arroba").And.Contain("no es válido");
    }

    [Fact]
    public void UnaLineaSinSeparadorDiceQueFalta()
    {
        var r = CandidateCsvParser.Parse("solo-un-texto");

        r.Candidates.Should().BeEmpty();
        r.Issues.Should().ContainSingle().Which.Reason.Should().Contain("separador");
    }

    [Fact]
    public void UnaLineaSinNombreDiceQueFalta()
    {
        var r = CandidateCsvParser.Parse(";ana@x.com");

        r.Issues.Should().ContainSingle().Which.Reason.Should().Contain("nombre");
    }

    [Fact]
    public void UnCorreoRepetidoSeAvisaYSeConservaElPrimero()
    {
        var r = CandidateCsvParser.Parse("Ana;ana@x.com\nAna López;ANA@x.com");

        r.Candidates.Should().ContainSingle().Which.Name.Should().Be("Ana");
        r.Issues.Should().ContainSingle().Which.Reason.Should().Contain("línea 1");
    }

    [Fact]
    public void LaNumeracionCuentaLasLineasVacias()
    {
        var r = CandidateCsvParser.Parse("Ana;ana@x.com\n\n\nmal;mal");

        r.Issues.Should().ContainSingle().Which.Line.Should().Be(4);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void UnTextoVacioNoImportaNadaNiDaErrores(string? texto)
    {
        var r = CandidateCsvParser.Parse(texto);

        r.Candidates.Should().BeEmpty();
        r.Issues.Should().BeEmpty();
    }
}
