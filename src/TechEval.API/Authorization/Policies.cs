using TechEval.Domain.Enums;

namespace TechEval.API.Authorization;

/// <summary>
/// La matriz de permisos de la API, en un solo sitio. Los controladores nombran una política
/// y no una lista de roles, así que cambiar quién puede qué no obliga a editarlos uno a uno.
/// </summary>
/// <remarks>
/// OJO al añadir una política a un método: ASP.NET combina con Y lógico el `[Authorize]` de la
/// clase y el del método. Un método con una política más abierta dentro de una clase con
/// `Gestion` sigue exigiendo `Gestion`. Para abrir un endpoint, hay que sacarlo de la clase
/// o pasar la clase a atributos por método.
/// </remarks>
public static class Policies
{
    /// <summary>Consola de administración: banco, pruebas, resultados, corrección y usuarios.</summary>
    public const string Gestion = "Gestion";

    /// <summary>Portal del alumno y resolución de la prueba.</summary>
    public const string Alumno = "Alumno";

    public static IServiceCollection AddTechEvalAuthorization(this IServiceCollection services)
        => services.AddAuthorization(o =>
        {
            o.AddPolicy(Gestion, p => p.RequireRole(nameof(UserRole.Admin)));
            o.AddPolicy(Alumno, p => p.RequireRole(nameof(UserRole.Alumno)));
        });
}
