using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Xunit;
using TechEval.Application;
using TechEval.Application.DTOs;
using TechEval.Application.Services;
using TechEval.Domain.Entities;
using TechEval.Domain.Interfaces.Repositories;

namespace TechEval.Tests.Services;

/// <summary>
/// Gestión de categorías (validación, duplicados, reactivar, filtrar las activas) y el orden por
/// columna de los listados. Antes un nombre repetido daba 500 y nada validaba la longitud.
/// </summary>
public class CategoryAndSortTests
{
    // ---------- Categorías ----------

    private readonly List<Category> _db = new();
    private readonly Mock<IRepository<Category>> _repo = new();
    private readonly Mock<IQuestionRepository> _questions = new();
    private readonly CategoryService _sut;

    public CategoryAndSortTests()
    {
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _db.ToList());
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => _db.FirstOrDefault(c => c.Id == id));
        _repo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Category, bool>> p, CancellationToken _) => _db.Where(p.Compile()).ToList());
        _repo.Setup(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category c, CancellationToken _) => { c.Id = _db.Count + 1; _db.Add(c); return c; });
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _questions.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Question, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        _sut = new CategoryService(_repo.Object, _questions.Object);
    }

    private Category Existente(int id, string nombre, bool activa = true)
    {
        var c = new Category { Id = id, Name = nombre, IsActive = activa };
        _db.Add(c);
        return c;
    }

    [Fact]
    public async Task Listado_PorDefecto_SoloLasActivas()
    {
        Existente(1, "Activa");
        Existente(2, "De baja", activa: false);

        var lista = await _sut.GetAllAsync();

        lista.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Name = "Activa", IsActive = true });
    }

    [Fact]
    public async Task Listado_ConInactivas_DevuelveTodasConSuEstado()
    {
        Existente(1, "Activa");
        Existente(2, "De baja", activa: false);

        var lista = await _sut.GetAllAsync(includeInactive: true);

        lista.Select(c => (c.Name, c.IsActive)).Should().Equal(("Activa", true), ("De baja", false));
        lista.Should().OnlyContain(c => c.QuestionCount == 4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Crear_SinNombre_SeRechaza(string nombre)
    {
        var act = () => _sut.CreateAsync(new CreateCategoryDto(nombre, ""));

        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().Contain("obligatorio");
        _repo.Verify(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_ConNombreDe101Caracteres_SeRechaza()
    {
        var act = () => _sut.CreateAsync(new CreateCategoryDto(new string('n', 101), ""));

        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().Contain("100");
    }

    [Fact]
    public async Task Crear_ConNombreDe100Caracteres_Pasa()
        => (await _sut.CreateAsync(new CreateCategoryDto(new string('n', 100), ""))).Name.Should().HaveLength(100);

    [Fact]
    public async Task Crear_ConDescripcionDe501Caracteres_SeRechaza()
    {
        var act = () => _sut.CreateAsync(new CreateCategoryDto("Nueva", new string('d', 501)));

        (await act.Should().ThrowAsync<ValidationException>()).Which.Message.Should().Contain("500");
    }

    [Fact]
    public async Task Crear_GuardaElNombreRecortadoYActiva()
    {
        var creada = await _sut.CreateAsync(new CreateCategoryDto("  Kubernetes  ", "  Orquestación  "));

        creada.Name.Should().Be("Kubernetes");
        creada.IsActive.Should().BeTrue();
        creada.QuestionCount.Should().Be(0);
        _db.Single().Description.Should().Be("Orquestación");
    }

    [Theory]
    [InlineData("backend")]
    [InlineData("  BACKEND  ")]
    public async Task Crear_ConUnNombreRepetido_Da409(string nombre)
    {
        Existente(1, "Backend");

        var act = () => _sut.CreateAsync(new CreateCategoryDto(nombre, ""));

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("Backend");
        _db.Should().ContainSingle();
    }

    [Fact]
    public async Task Renombrar_AUnNombreUsado_Da409YNoCambia()
    {
        Existente(1, "Backend");
        var frontend = Existente(2, "Frontend");

        var act = () => _sut.UpdateAsync(2, new UpdateCategoryDto("Backend", ""));

        await act.Should().ThrowAsync<ConflictException>();
        frontend.Name.Should().Be("Frontend");
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Editar_SoloLaDescripcion_NoChocaConsigoMisma()
    {
        var backend = Existente(1, "Backend");

        var editada = await _sut.UpdateAsync(1, new UpdateCategoryDto("Backend", "Servicios"));

        editada!.Description.Should().Be("Servicios");
        backend.Description.Should().Be("Servicios");
    }

    [Fact]
    public async Task Editar_SinIsActive_ConservaElEstado()
    {
        var baja = Existente(1, "De baja", activa: false);

        await _sut.UpdateAsync(1, new UpdateCategoryDto("De baja", "x"));

        baja.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Editar_ConIsActiveTrue_Reactiva()
    {
        var baja = Existente(1, "De baja", activa: false);

        var editada = await _sut.UpdateAsync(1, new UpdateCategoryDto("De baja", "x", IsActive: true));

        baja.IsActive.Should().BeTrue();
        editada!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Editar_UnaCategoriaInexistente_DevuelveNulo()
        => (await _sut.UpdateAsync(99, new UpdateCategoryDto("X", ""))).Should().BeNull();

    [Fact]
    public async Task Editar_ConNombreInvalido_NoLeeLaCategoria()
    {
        var act = () => _sut.UpdateAsync(1, new UpdateCategoryDto("", ""));

        await act.Should().ThrowAsync<ValidationException>();
        _repo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Baja_DesactivaSinBorrar()
    {
        var c = Existente(1, "Backend");

        (await _sut.DeleteAsync(1)).Should().BeTrue();

        c.IsActive.Should().BeFalse();
        _db.Should().ContainSingle();
    }

    // ---------- Orden por columna ----------

    private sealed record Fila(string? Nombre, int? Nota, DateTime? Fecha = null);

    private static readonly IReadOnlyDictionary<string, Func<Fila, object?>> Columnas =
        new Dictionary<string, Func<Fila, object?>>
        {
            ["nombre"] = f => f.Nombre,
            ["nota"] = f => f.Nota,
            ["fecha"] = f => f.Fecha
        };

    private static string[] Orden(IEnumerable<Fila> filas, string? clave, bool descendente = false)
        => ListSort.Apply(filas, new SortState { Key = clave, Descending = descendente }, Columnas)
            .Select(f => f.Nombre ?? "(nulo)").ToArray();

    [Fact]
    public void Orden_AlternaDireccionAlPulsarLaMismaColumna()
    {
        var s = new SortState();

        s.Toggle("nota");
        (s.Key, s.Descending).Should().Be(("nota", false));
        s.Toggle("nota");
        (s.Key, s.Descending).Should().Be(("nota", true));
        s.Toggle("nota");
        s.Descending.Should().BeFalse();
    }

    [Fact]
    public void Orden_CambiarDeColumnaEmpiezaAscendente()
    {
        var s = new SortState { Key = "nota", Descending = true };

        s.Toggle("nombre");

        (s.Key, s.Descending).Should().Be(("nombre", false));
    }

    [Fact]
    public void Orden_AriaSort()
    {
        var s = new SortState { Key = "nota", Descending = true };

        s.AriaSort("nota").Should().Be("descending");
        s.AriaSort("nombre").Should().Be("none");
        new SortState { Key = "nota" }.AriaSort("nota").Should().Be("ascending");
    }

    [Fact]
    public void Orden_SinClaveOClaveDesconocida_DejaLaListaTalCual()
    {
        var filas = new[] { new Fila("B", 1), new Fila("A", 2) };

        Orden(filas, null).Should().Equal("B", "A");
        Orden(filas, "no-existe").Should().Equal("B", "A");
    }

    [Fact]
    public void Orden_TextosConTildesSegunElCastellano()
    {
        var filas = new[] { new Fila("Zoe", 1), new Fila("Álvaro", 1), new Fila("Andrés", 1), new Fila("alba", 1) };

        Orden(filas, "nombre").Should().Equal("alba", "Álvaro", "Andrés", "Zoe");
    }

    [Fact]
    public void Orden_NoDistingueMayusculas()
    {
        var filas = new[] { new Fila("beta", 1), new Fila("Alfa", 1), new Fila("CHARLIE", 1) };

        Orden(filas, "nombre").Should().Equal("Alfa", "beta", "CHARLIE");
    }

    [Fact]
    public void Orden_LosNulosYVaciosVanAlFinalEnLasDosDirecciones()
    {
        var filas = new[] { new Fila("Ana", 50), new Fila("Luis", null), new Fila("Eva", 90), new Fila("Pau", null) };

        Orden(filas, "nota").Should().Equal("Ana", "Eva", "Luis", "Pau");
        Orden(filas, "nota", descendente: true).Should().Equal("Eva", "Ana", "Luis", "Pau");
    }

    [Fact]
    public void Orden_LasCadenasVaciasTambienVanAlFinal()
    {
        var filas = new[] { new Fila("", 1), new Fila("Beta", 1), new Fila(null, 1), new Fila("Alfa", 1) };

        ListSort.Apply(filas, new SortState { Key = "nombre" }, Columnas)
            .Select(f => f.Nombre).Should().Equal("Alfa", "Beta", "", null);
    }

    [Fact]
    public void Orden_NumerosNoComoTexto()
    {
        var filas = new[] { new Fila("a", 100), new Fila("b", 20), new Fila("c", 3) };

        Orden(filas, "nota").Should().Equal("c", "b", "a");
    }

    [Fact]
    public void Orden_Fechas()
    {
        var filas = new[]
        {
            new Fila("tarde", 1, new DateTime(2026, 10, 9)), new Fila("pronto", 1, new DateTime(2026, 1, 2))
        };

        Orden(filas, "fecha").Should().Equal("pronto", "tarde");
        Orden(filas, "fecha", descendente: true).Should().Equal("tarde", "pronto");
    }

    [Fact]
    public void Orden_EmpatesConservanElOrdenOriginal()
    {
        var filas = new[] { new Fila("primero", 5), new Fila("segundo", 5), new Fila("tercero", 5) };

        Orden(filas, "nota").Should().Equal("primero", "segundo", "tercero");
        Orden(filas, "nota", descendente: true).Should().Equal("primero", "segundo", "tercero");
    }

    [Fact]
    public void Orden_NoModificaLaListaOriginal()
    {
        var filas = new List<Fila> { new("B", 1), new("A", 2) };

        _ = ListSort.Apply(filas, new SortState { Key = "nombre" }, Columnas).ToList();

        filas.Select(f => f.Nombre).Should().Equal("B", "A");
    }

    [Fact]
    public void Orden_ClonarElEstadoNoLoComparte()
    {
        var original = new SortState { Key = "nota", Descending = true };
        var copia = original.Clone();

        original.Toggle("nombre");

        (copia.Key, copia.Descending).Should().Be(("nota", true));
    }
}
