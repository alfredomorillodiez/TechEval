using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;
using TechEval.Infrastructure.Data;
using TechEval.Infrastructure.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// Consulta de opciones ya elegidas. Es la que decide si una edición puede borrar una
/// opción o tiene que rechazarse.
/// </summary>
public class QuestionRepositoryTests
{
    private static async Task<AppDbContext> ContextoConRespuestasAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"preguntas-{Guid.NewGuid()}")
            .Options;
        var db = new AppDbContext(options);

        db.Categories.Add(new Category { Id = 3, Name = "APIs REST" });
        db.Questions.Add(new Question
        {
            Id = 6, Text = "Pregunta usada", Type = QuestionType.MultipleChoice,
            CategoryId = 3, Points = 1,
            Answers = new List<Answer>
            {
                new() { Id = 21, Text = "POST", IsCorrect = false, Order = 1 },
                new() { Id = 22, Text = "PUT", IsCorrect = false, Order = 2 },
                new() { Id = 24, Text = "PATCH", IsCorrect = true, Order = 4 }
            }
        });
        db.Questions.Add(new Question
        {
            Id = 7, Text = "Pregunta nueva", Type = QuestionType.MultipleChoice,
            CategoryId = 3, Points = 1,
            Answers = new List<Answer> { new() { Id = 31, Text = "A", IsCorrect = true, Order = 1 } }
        });

        var session = new ExamSession { Id = 1, ExamTokenId = 1, Status = SessionStatus.Completed };
        db.ExamSessions.Add(session);

        // Dos candidatos eligieron la 24; nadie eligió la 21 ni la 22.
        db.UserAnswers.AddRange(
            new UserAnswer { Id = 1, ExamSessionId = 1, QuestionId = 6, SelectedAnswerId = 24 },
            new UserAnswer { Id = 2, ExamSessionId = 1, QuestionId = 6, SelectedAnswerId = 24 },
            // Respuesta abierta: sin opción elegida, no referencia ninguna Answer.
            new UserAnswer { Id = 3, ExamSessionId = 1, QuestionId = 6, OpenAnswer = "texto" });

        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task OpcionesReferenciadas_DevuelveSoloLasElegidas()
    {
        using var db = await ContextoConRespuestasAsync();
        var repo = new QuestionRepository(db);

        var ids = await repo.GetReferencedAnswerIdsAsync(6);

        ids.Should().BeEquivalentTo(new[] { 24 });
    }

    [Fact]
    public async Task OpcionesReferenciadas_NoRepiteLaMismaOpcionElegidaVariasVeces()
    {
        using var db = await ContextoConRespuestasAsync();
        var repo = new QuestionRepository(db);

        var ids = await repo.GetReferencedAnswerIdsAsync(6);

        ids.Should().HaveCount(1);
    }

    [Fact]
    public async Task OpcionesReferenciadas_PreguntaSinRespuestas_DevuelveListaVacia()
    {
        using var db = await ContextoConRespuestasAsync();
        var repo = new QuestionRepository(db);

        var ids = await repo.GetReferencedAnswerIdsAsync(7);

        ids.Should().BeEmpty();
    }
}
