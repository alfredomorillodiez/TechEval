using Microsoft.EntityFrameworkCore;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;

namespace TechEval.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, string adminPasswordHash)
    {
        await context.Database.EnsureCreatedAsync();

        if (!await context.Users.AnyAsync())
        {
            context.Users.Add(new User
            {
                Email = "admin@techeval.com",
                Name = "Administrador",
                PasswordHash = adminPasswordHash,
                IsAdmin = true
            });
            await context.SaveChangesAsync();
        }

        if (!await context.Categories.AnyAsync())
        {
            var categories = new[]
            {
                new Category { Name = "SQL", Description = "Consultas, diseño de BD, optimización" },
                new Category { Name = "C#", Description = "Programación orientada a objetos, LINQ, async" },
                new Category { Name = "APIs REST", Description = "Diseño de APIs, HTTP, autenticación" },
                new Category { Name = "Arquitectura", Description = "Patrones de diseño, Clean Architecture, SOLID" },
                new Category { Name = "DevOps", Description = "Docker, CI/CD, despliegue" }
            };
            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();

            // Preguntas de ejemplo
            var sqlCat = categories[0];
            var csharpCat = categories[1];

            context.Questions.AddRange(
                new Question
                {
                    Text = "¿Qué cláusula SQL se usa para filtrar grupos de registros?",
                    Type = QuestionType.MultipleChoice,
                    Difficulty = DifficultyLevel.Basic,
                    CategoryId = sqlCat.Id,
                    Points = 1,
                    Answers = new List<Answer>
                    {
                        new() { Text = "WHERE", IsCorrect = false, Order = 1 },
                        new() { Text = "HAVING", IsCorrect = true, Order = 2 },
                        new() { Text = "GROUP BY", IsCorrect = false, Order = 3 },
                        new() { Text = "ORDER BY", IsCorrect = false, Order = 4 }
                    }
                },
                new Question
                {
                    Text = "¿Cuál es la diferencia entre INNER JOIN y LEFT JOIN?",
                    Type = QuestionType.OpenEnded,
                    Difficulty = DifficultyLevel.Intermediate,
                    CategoryId = sqlCat.Id,
                    Points = 3,
                    SampleAnswer = "INNER JOIN devuelve solo las filas que tienen coincidencia en ambas tablas. LEFT JOIN devuelve todas las filas de la tabla izquierda y las coincidencias de la derecha (NULL si no hay)."
                },
                new Question
                {
                    Text = "¿Qué palabra clave de C# permite ejecutar código de forma asíncrona sin bloquear el hilo?",
                    Type = QuestionType.MultipleChoice,
                    Difficulty = DifficultyLevel.Basic,
                    CategoryId = csharpCat.Id,
                    Points = 1,
                    Answers = new List<Answer>
                    {
                        new() { Text = "parallel", IsCorrect = false, Order = 1 },
                        new() { Text = "await", IsCorrect = true, Order = 2 },
                        new() { Text = "async only", IsCorrect = false, Order = 3 },
                        new() { Text = "thread", IsCorrect = false, Order = 4 }
                    }
                }
            );
            await context.SaveChangesAsync();
        }
    }
}
