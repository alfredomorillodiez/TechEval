using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using TechEval.Domain.Entities;
using TechEval.Domain.Enums;

namespace TechEval.Infrastructure.Data;

public static class DbSeeder
{
    /// <summary>
    /// Siembra lo mínimo para que la aplicación sea utilizable. No crea el esquema.
    /// </summary>
    /// <param name="seedSampleContent">
    /// Categorías y preguntas de muestra. Solo en desarrollo: en el banco de un cliente
    /// son basura, y una vez dentro cuesta distinguirlas de las suyas.
    /// </param>
    public static async Task SeedAsync(
        AppDbContext context, string adminPasswordHash, bool seedSampleContent)
    {
        await EnsureSchemaExistsAsync(context);

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

        if (seedSampleContent && !await context.Categories.AnyAsync())
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

    /// <summary>
    /// El esquema lo crean los guiones de <c>scripts/</c>, no la aplicación.
    ///
    /// Antes había aquí un <c>EnsureCreatedAsync()</c>. Creaba el esquema desde el modelo,
    /// y con ello dejaba las migraciones de EF permanentemente inservibles: su tabla de
    /// historial no llega a existir, así que la primera migración falla. A la vez, el
    /// esquema de desarrollo salía del modelo y el de producción del guion, y los dos
    /// divergían en silencio.
    ///
    /// Si el esquema no está, se para aquí. Sin esta comprobación el fallo llegaría como
    /// un error de SQL en mitad de la primera petición, sin decir qué hacer.
    /// </summary>
    private static async Task EnsureSchemaExistsAsync(AppDbContext context)
    {
        var creator = context.Database.GetService<IRelationalDatabaseCreator>();

        if (!await creator.ExistsAsync())
            throw new InvalidOperationException(
                "La base de datos no existe. Créala con `scripts/create_database.sql` antes de arrancar la API.");

        if (!await creator.HasTablesAsync())
            throw new InvalidOperationException(
                "La base de datos existe pero no tiene esquema. Ejecuta `scripts/create_database.sql`. "
                + "La aplicación ya no crea el esquema al arrancar.");
    }
}
