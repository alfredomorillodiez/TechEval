namespace TechEval.Application;

/// <summary>
/// Excepciones que expresan cómo termina una operación de negocio.
/// </summary>
/// <remarks>
/// Expresan **intención**, no implementación: el pipeline de la API las traduce a códigos de
/// estado en un solo sitio. Aquí no se menciona HTTP a propósito — la intención es de esta
/// capa, el número no.
///
/// Todo lo que no herede de estas cuatro es un fallo que la aplicación no previó, y sale
/// como 500 con mensaje genérico. Antes cualquier `InvalidOperationException` salía como 400
/// con su texto, así que un error de EF Core llegaba al navegador del candidato como si
/// fuera culpa suya, y con el detalle interno dentro.
/// </remarks>
public abstract class ApplicationException : Exception
{
    protected ApplicationException(string message) : base(message) { }
}

/// <summary>La petición incumple una regla de negocio. Lo que pide no es válido.</summary>
public class ValidationException : ApplicationException
{
    public ValidationException(string message) : base(message) { }
}

/// <summary>Lo que la petición referencia no existe.</summary>
public class NotFoundException : ApplicationException
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>El estado actual del sistema no permite la operación.</summary>
public class ConflictException : ApplicationException
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Quien pide no tiene permiso sobre lo que pide.</summary>
public class ForbiddenException : ApplicationException
{
    public ForbiddenException(string message) : base(message) { }
}
