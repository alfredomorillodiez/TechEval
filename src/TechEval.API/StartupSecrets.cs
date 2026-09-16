namespace TechEval.API;

/// <summary>
/// Comprueba que los secretos obligatorios llegan por configuración antes de arrancar.
/// </summary>
/// <remarks>
/// Para en seco en vez de avisar. Un aviso en el log de arranque de producción lo lee nadie,
/// y la aplicación quedaría sirviendo con la clave que está publicada en el repositorio.
///
/// Rechaza además los valores de desarrollo: copiar `appsettings.Development.json` a
/// producción dejaría una clave pública haciendo de secreto, y una comprobación que solo
/// mirase "no está vacío" la daría por buena.
/// </remarks>
public static class StartupSecrets
{
    /// <summary>Valores publicados en `appsettings.Development.json`. Públicos por definición.</summary>
    private static readonly string[] ValoresDeDesarrollo =
    [
        "TechEval_Dev_Only_NotASecret_ChangeInProduction",
        "Admin@123!"
    ];

    private static readonly (string Clave, string Descripcion)[] Obligatorios =
    [
        ("Jwt:SecretKey", "clave de firma de los tokens de sesión"),
        ("AdminPassword", "contraseña del administrador que se siembra al arrancar"),
        ("ConnectionStrings:DefaultConnection", "cadena de conexión a la base de datos")
    ];

    /// <summary>
    /// Lanza si la configuración no sirve para producción. Devuelve en silencio si sirve.
    /// </summary>
    public static void Validate(IConfiguration configuration)
    {
        var problemas = Check(configuration);
        if (problemas.Count == 0) return;

        throw new InvalidOperationException(
            "La aplicación no puede arrancar fuera de desarrollo con esta configuración:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, problemas.Select(p => "  - " + p))
            + Environment.NewLine
            + "Define esos valores por variable de entorno o por el almacén de secretos del destino. "
            + "Los nombres están en .env.example.");
    }

    /// <summary>Lista legible de lo que falla. Vacía si la configuración sirve.</summary>
    public static IReadOnlyList<string> Check(IConfiguration configuration)
    {
        var problemas = new List<string>();

        foreach (var (clave, descripcion) in Obligatorios)
        {
            var valor = configuration[clave];

            if (string.IsNullOrWhiteSpace(valor))
            {
                problemas.Add($"falta `{clave}` ({descripcion})");
                continue;
            }

            if (ValoresDeDesarrollo.Contains(valor))
                problemas.Add(
                    $"`{clave}` conserva su valor de desarrollo, que es público y no sirve como secreto");
        }

        return problemas;
    }
}
