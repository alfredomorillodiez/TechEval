using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using TechEval.Infrastructure.Data;

namespace TechEval.Tests.Services;

/// <summary>
/// El modelo y `scripts/create_database.sql` dicen lo mismo.
///
/// El proyecto no usa migraciones de EF: el esquema lo crean los guiones de `scripts/`.
/// Nada obligaba a llevar al guion una columna nueva del modelo, y el 16·09 se añadieron
/// cuatro a `UserAnswers` sin llevarlas. Una base creada desde cero con el guion completo
/// quedaba sin ellas, y la aplicación fallaba al enviar un examen.
///
/// LO QUE ESTA PRUEBA NO COMPRUEBA: tipos, longitudes, valores por defecto, índices ni
/// claves foráneas. Compara nombres contra el texto del guion. Es una red contra el olvido,
/// no una validación del esquema. Para eso hace falta una base de datos real, y eso es el
/// hallazgo E2.
/// </summary>
public class SchemaDriftTests
{
    private static readonly string Guion = LeerGuion();
    private static readonly IModel Modelo = ConstruirModelo();

    private static IModel ConstruirModelo()
    {
        // Con el proveedor de SQL Server, no con el de memoria: el de memoria no es
        // relacional y `GetTableName()` devuelve el nombre de la entidad en vez del de la
        // tabla. No se conecta a nada; solo se construye el modelo.
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=no-se-conecta;Database=TechEvalDb;")
            .Options;
        using var ctx = new AppDbContext(opciones);
        return ctx.Model;
    }

    private static string LeerGuion()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TechEval.sln")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException("No se encuentra la raíz del repositorio desde el directorio de la prueba.");

        return File.ReadAllText(Path.Combine(dir.FullName, "scripts", "create_database.sql"));
    }

    private static IEnumerable<IEntityType> Entidades =>
        Modelo.GetEntityTypes().Where(e => !e.IsOwned());

    /// <summary>Trozo del guion entre el CREATE TABLE de esa tabla y el siguiente.</summary>
    private static string BloqueDe(string tabla)
    {
        var inicio = Guion.IndexOf($"CREATE TABLE dbo.{tabla}", StringComparison.OrdinalIgnoreCase);
        if (inicio < 0) return "";
        var fin = Guion.IndexOf("CREATE TABLE", inicio + 1, StringComparison.OrdinalIgnoreCase);
        return fin < 0 ? Guion[inicio..] : Guion[inicio..fin];
    }

    [Fact]
    public void CadaEntidadDelModelo_TieneSuTablaEnElGuion()
    {
        var faltan = Entidades
            .Select(e => e.GetTableName()!)
            .Where(t => !Guion.Contains($"CREATE TABLE dbo.{t}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        faltan.Should().BeEmpty(
            "create_database.sql debe crear toda tabla que el modelo persiste; faltan: {0}",
            string.Join(", ", faltan));
    }

    [Fact]
    public void CadaColumnaDelModelo_EstaEnSuTablaDelGuion()
    {
        var faltan = new List<string>();

        foreach (var entidad in Entidades)
        {
            var tabla = entidad.GetTableName()!;
            var bloque = BloqueDe(tabla);
            if (bloque.Length == 0) continue;   // lo cubre la prueba de tablas

            var id = StoreObjectIdentifier.Table(tabla, entidad.GetSchema());

            foreach (var propiedad in entidad.GetProperties())
            {
                var columna = propiedad.GetColumnName(id);
                if (columna is null) continue;

                // Delimitado para que `Id` no case dentro de `ExamSessionId`.
                var patron = new System.Text.RegularExpressions.Regex(
                    $@"(^|[^A-Za-z0-9_]){System.Text.RegularExpressions.Regex.Escape(columna)}([^A-Za-z0-9_]|$)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (!patron.IsMatch(bloque)) faltan.Add($"{tabla}.{columna}");
            }
        }

        faltan.Should().BeEmpty(
            "create_database.sql debe declarar toda columna que el modelo persiste; faltan: {0}",
            string.Join(", ", faltan));
    }

    [Fact]
    public void LasDiezTablasDelGuion_CorrespondenAEntidadesDelModelo()
    {
        // El sentido contrario: una tabla en el guion que ya no exista en el modelo es
        // esquema muerto. Ocurrió con las tablas del pipeline de IA retirado.
        var enElGuion = System.Text.RegularExpressions.Regex
            .Matches(Guion, @"CREATE TABLE dbo\.(\w+)")
            .Select(m => m.Groups[1].Value)
            .ToList();

        var enElModelo = Entidades.Select(e => e.GetTableName()!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sobran = enElGuion.Where(t => !enElModelo.Contains(t)).ToList();

        sobran.Should().BeEmpty(
            "el guion no debe crear tablas que el modelo ya no usa; sobran: {0}",
            string.Join(", ", sobran));
    }
}
