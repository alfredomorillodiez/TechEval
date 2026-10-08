using FluentAssertions;
using Moq;
using Xunit;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// Edición de una pregunta ya usada. Las opciones que un candidato eligió no pueden
/// desaparecer: `UserAnswer.SelectedAnswerId` las referencia y perderlas destruiría el
/// registro de lo que ese candidato respondió.
/// </summary>
public class QuestionEditTests
{
    private readonly Mock<IQuestionRepository> _repo = new();
    private readonly QuestionService _sut;

    private Question _question = null!;

    public QuestionEditTests() => _sut = new QuestionService(_repo.Object);

    /// <summary>Pregunta tipo test con cuatro opciones de identificadores 21 a 24.</summary>
    private Question ConfigurarPregunta(params int[] opcionesReferenciadas)
    {
        _question = new Question
        {
            Id = 6,
            Text = "Enunciado original",
            Type = QuestionType.MultipleChoice,
            Difficulty = DifficultyLevel.Basic,
            CategoryId = 3,
            Points = 1,
            IsActive = true,
            Category = new Category { Name = "APIs REST" },
            Answers = new List<Answer>
            {
                new() { Id = 21, QuestionId = 6, Text = "POST",  IsCorrect = false, Order = 1 },
                new() { Id = 22, QuestionId = 6, Text = "PUT",   IsCorrect = false, Order = 2 },
                new() { Id = 23, QuestionId = 6, Text = "GET",   IsCorrect = false, Order = 3 },
                new() { Id = 24, QuestionId = 6, Text = "PATCH", IsCorrect = true,  Order = 4 }
            }
        };

        _repo.Setup(r => r.GetWithAnswersAsync(6, default)).ReturnsAsync(_question);
        _repo.Setup(r => r.GetReferencedAnswerIdsAsync(6, default))
            .ReturnsAsync(opcionesReferenciadas.ToList());

        return _question;
    }

    private static UpdateQuestionDto Edicion(string texto, params UpdateAnswerDto[] opciones)
        => new(texto, QuestionType.MultipleChoice, DifficultyLevel.Basic,
               CategoryId: 3, Points: 1, IsActive: true, SampleAnswer: null,
               Answers: opciones.ToList());

    private static UpdateAnswerDto Opcion(int? id, string texto, bool correcta, int orden)
        => new(id, texto, correcta, orden);

    [Fact]
    public async Task Editar_PreguntaYaRespondida_ConservaLosIdentificadoresDeSusOpciones()
    {
        var question = ConfigurarPregunta(opcionesReferenciadas: 24);

        await _sut.UpdateAsync(6, Edicion(
            "Enunciado corregido",
            Opcion(21, "POST", false, 1),
            Opcion(22, "PUT", false, 2),
            Opcion(23, "GET", false, 3),
            Opcion(24, "PATCH", true, 4)));

        question.Text.Should().Be("Enunciado corregido");
        question.Answers.Select(a => a.Id).Should().BeEquivalentTo(new[] { 21, 22, 23, 24 });
        question.Answers.Should().HaveCount(4);
    }

    [Fact]
    public async Task Editar_PreguntaYaRespondida_ActualizaElTextoDeLaOpcionEnSuSitio()
    {
        var question = ConfigurarPregunta(opcionesReferenciadas: 24);

        await _sut.UpdateAsync(6, Edicion(
            "Enunciado original",
            Opcion(21, "POST", false, 1),
            Opcion(22, "PUT", false, 2),
            Opcion(23, "GET", false, 3),
            Opcion(24, "PATCH (parcial)", true, 4)));

        var patch = question.Answers.Single(a => a.Id == 24);
        patch.Text.Should().Be("PATCH (parcial)");
        patch.Id.Should().Be(24);
    }

    [Fact]
    public async Task Editar_CambiarLaOpcionCorrecta_FuncionaSobreUnaPreguntaRespondida()
    {
        var question = ConfigurarPregunta(opcionesReferenciadas: 24);

        await _sut.UpdateAsync(6, Edicion(
            "Enunciado original",
            Opcion(21, "POST", false, 1),
            Opcion(22, "PUT", true, 2),
            Opcion(23, "GET", false, 3),
            Opcion(24, "PATCH", false, 4)));

        question.Answers.Single(a => a.Id == 22).IsCorrect.Should().BeTrue();
        question.Answers.Single(a => a.Id == 24).IsCorrect.Should().BeFalse();
        question.Answers.Select(a => a.Id).Should().BeEquivalentTo(new[] { 21, 22, 23, 24 });
    }

    [Fact]
    public async Task Editar_OpcionNueva_SeAnadeSinTocarLasExistentes()
    {
        var question = ConfigurarPregunta(opcionesReferenciadas: 24);

        var dto = new UpdateQuestionDto(
            "Enunciado original", QuestionType.OpenEnded, DifficultyLevel.Basic,
            CategoryId: 3, Points: 1, IsActive: true, SampleAnswer: null,
            Answers: new List<UpdateAnswerDto>
            {
                Opcion(21, "POST", false, 1),
                Opcion(22, "PUT", false, 2),
                Opcion(23, "GET", false, 3),
                Opcion(24, "PATCH", true, 4),
                Opcion(null, "HEAD", false, 5)
            });

        await _sut.UpdateAsync(6, dto);

        question.Answers.Should().HaveCount(5);
        question.Answers.Count(a => a.Id == 0).Should().Be(1);
        question.Answers.Single(a => a.Id == 0).Text.Should().Be("HEAD");
    }

    [Fact]
    public async Task Editar_QuitarUnaOpcionQueNadieEligio_LaBorra()
    {
        // Solo la 24 está referenciada; la 23 se puede retirar sin romper nada.
        var question = ConfigurarPregunta(opcionesReferenciadas: 24);

        var dto = new UpdateQuestionDto(
            "Enunciado original", QuestionType.OpenEnded, DifficultyLevel.Basic,
            CategoryId: 3, Points: 1, IsActive: true, SampleAnswer: null,
            Answers: new List<UpdateAnswerDto>
            {
                Opcion(21, "POST", false, 1),
                Opcion(22, "PUT", false, 2),
                Opcion(24, "PATCH", true, 3)
            });

        await _sut.UpdateAsync(6, dto);

        question.Answers.Select(a => a.Id).Should().BeEquivalentTo(new[] { 21, 22, 24 });
    }

    [Fact]
    public async Task Editar_QuitarUnaOpcionYaElegida_SeRechaza()
    {
        ConfigurarPregunta(opcionesReferenciadas: 24);

        var dto = new UpdateQuestionDto(
            "Enunciado original", QuestionType.OpenEnded, DifficultyLevel.Basic,
            CategoryId: 3, Points: 1, IsActive: true, SampleAnswer: null,
            Answers: new List<UpdateAnswerDto>());

        var act = () => _sut.UpdateAsync(6, dto);

        await act.Should().ThrowAsync<AnswerInUseException>()
            .WithMessage("*algún candidato ya las eligió*");
    }

    [Fact]
    public async Task Editar_RechazoPorOpcionEnUso_NoDejaLaPreguntaAMedioEditar()
    {
        var question = ConfigurarPregunta(opcionesReferenciadas: 24);

        var dto = new UpdateQuestionDto(
            "Enunciado que no debe guardarse", QuestionType.OpenEnded, DifficultyLevel.Basic,
            CategoryId: 99, Points: 7, IsActive: false, SampleAnswer: "otra cosa",
            Answers: new List<UpdateAnswerDto>());

        var act = () => _sut.UpdateAsync(6, dto);
        await act.Should().ThrowAsync<AnswerInUseException>();

        question.Text.Should().Be("Enunciado original");
        question.CategoryId.Should().Be(3);
        question.Points.Should().Be(1);
        question.IsActive.Should().BeTrue();
        question.UpdatedAt.Should().BeNull();
        question.Answers.Should().HaveCount(4);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Question>(), default), Times.Never);
    }

    [Fact]
    public async Task Editar_PreguntaInexistente_DevuelveNull()
    {
        _repo.Setup(r => r.GetWithAnswersAsync(999, default)).ReturnsAsync((Question?)null);

        var result = await _sut.UpdateAsync(999, Edicion(
            "Da igual",
            Opcion(1, "A", true, 1), Opcion(2, "B", false, 2),
            Opcion(3, "C", false, 3), Opcion(4, "D", false, 4)));

        result.Should().BeNull();
        _repo.Verify(r => r.GetReferencedAnswerIdsAsync(It.IsAny<int>(), default), Times.Never);
    }
}
