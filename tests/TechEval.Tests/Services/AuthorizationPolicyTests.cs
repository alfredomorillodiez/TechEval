using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TechEval.API.Authorization;
using TechEval.API.Controllers;
using TechEval.Domain.Enums;

namespace TechEval.Tests.Services;

/// <summary>
/// Spec authentication — autorización por rol en gestión y en el portal.
///
/// Dos mitades. Una evalúa las políticas de verdad, con el servicio de autorización de
/// ASP.NET. La otra comprueba por reflexión que cada controlador nombra la política que le
/// toca: un controlador nuevo sin atributo, o con el equivocado, rompe la prueba. La
/// respuesta 403 por HTTP se comprueba contra la API en marcha.
/// </summary>
public class AuthorizationPolicyTests
{
    private static readonly IAuthorizationService Autorizacion = new ServiceCollection()
        .AddLogging()
        .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
        .AddTechEvalAuthorization()
        .BuildServiceProvider()
        .GetRequiredService<IAuthorizationService>();

    private static ClaimsPrincipal Con(UserRole rol) => new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.Role, rol.ToString()) }, "test"));

    [Theory]
    [InlineData(UserRole.Admin, Policies.Gestion, true)]
    [InlineData(UserRole.Evaluador, Policies.Gestion, false)]
    [InlineData(UserRole.Alumno, Policies.Gestion, false)]
    [InlineData(UserRole.Alumno, Policies.Alumno, true)]
    [InlineData(UserRole.Evaluador, Policies.Alumno, false)]
    [InlineData(UserRole.Admin, Policies.Alumno, false)]
    public async Task Cada_rol_pasa_solo_su_politica(UserRole rol, string politica, bool pasa)
    {
        var resultado = await Autorizacion.AuthorizeAsync(Con(rol), politica);

        resultado.Succeeded.Should().Be(pasa);
    }

    public static TheoryData<Type> ControladoresDeGestion => new()
    {
        typeof(CategoriesController),
        typeof(QuestionsController),
        typeof(ExamsController),
        typeof(ResultsController),
        typeof(ReviewController),
        typeof(UsersController),
    };

    [Theory]
    [MemberData(nameof(ControladoresDeGestion))]
    public void Los_controladores_de_gestion_exigen_Gestion(Type controlador)
    {
        var atributo = controlador.GetCustomAttribute<AuthorizeAttribute>();

        atributo.Should().NotBeNull();
        atributo!.Policy.Should().Be(Policies.Gestion);
        atributo.Roles.Should().BeNull("los roles se nombran en Policies, no en cada controlador");
    }

    [Fact]
    public void El_portal_exige_Alumno()
    {
        typeof(StudentPortalController).GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(Policies.Alumno);
    }

    [Fact]
    public void La_resolucion_de_la_prueba_exige_Alumno_en_cada_metodo_protegido()
    {
        var protegidos = typeof(ExamSessionController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.GetCustomAttribute<AuthorizeAttribute>())
            .Where(a => a is not null)
            .ToList();

        protegidos.Should().NotBeEmpty();
        protegidos.Should().OnlyContain(a => a!.Policy == Policies.Alumno && a.Roles == null);
    }

    [Fact]
    public void Ningun_controlador_nombra_roles_sueltos()
    {
        var conRoles = typeof(ReviewController).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("Controller"))
            .SelectMany(t => t.GetMethods().Cast<MemberInfo>().Prepend(t))
            .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>().Select(a => (m, a)))
            .Where(x => x.a.Roles is not null)
            .Select(x => x.m.Name)
            .ToList();

        conRoles.Should().BeEmpty();
    }
}
