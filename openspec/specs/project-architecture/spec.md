# project-architecture Specification

## Purpose
TBD - created by archiving change scaffolding-clean-architecture. Update Purpose after archive.
## Requirements
### Requirement: Separación en capas de la solución
La solución SHALL organizarse en proyectos independientes por capa (`TechEval.Domain`, `TechEval.Application`, `TechEval.Infrastructure`, `TechEval.API`, `TechEval.Web`), cada uno con una única responsabilidad, referenciados desde `TechEval.sln`.

#### Scenario: Compilación de la solución completa
- **WHEN** se ejecuta `dotnet build` sobre `TechEval.sln`
- **THEN** los cinco proyectos de capa (`Domain`, `Application`, `Infrastructure`, `API`, `Web`) compilan como ensamblados independientes referenciados por la solución

### Requirement: Regla de dependencia entre capas
`TechEval.Domain` SHALL NOT depender de ningún otro proyecto de la solución. `TechEval.Application` SHALL depender únicamente de `TechEval.Domain`. `TechEval.Infrastructure` SHALL depender de `TechEval.Domain` (y opcionalmente `TechEval.Application`) e implementar sus interfaces, nunca al revés.

#### Scenario: Domain sin dependencias externas
- **WHEN** se inspeccionan las referencias de proyecto de `TechEval.Domain.csproj`
- **THEN** no existe ninguna referencia a `TechEval.Application`, `TechEval.Infrastructure`, `TechEval.API` ni `TechEval.Web`

#### Scenario: Infrastructure implementa contratos de Domain
- **WHEN** `TechEval.Infrastructure` provee una implementación de persistencia
- **THEN** dicha implementación satisface una interfaz declarada en `TechEval.Domain.Interfaces` (por ejemplo `IRepository<T>`) sin que `TechEval.Domain` referencie tipos de Entity Framework Core

### Requirement: Patrón de repositorio genérico
El acceso a datos SHALL exponerse a través de la interfaz genérica `IRepository<T>` (definida en `TechEval.Domain.Interfaces.Repositories`) con operaciones CRUD asíncronas (`GetByIdAsync`, `GetAllAsync`, `FindAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `CountAsync`), implementada de forma reutilizable por `BaseRepository<T>` sobre `AppDbContext`.

#### Scenario: Nueva entidad obtiene repositorio sin código adicional
- **WHEN** se registra `IRepository<TEntity>` para una entidad `TEntity` en el contenedor de dependencias mediante `AddScoped(typeof(IRepository<>), typeof(BaseRepository<>))`
- **THEN** el sistema resuelve un `BaseRepository<TEntity>` funcional respaldado por `DbSet<TEntity>` sin necesidad de escribir una clase de repositorio específica

#### Scenario: Repositorios especializados extienden el contrato genérico
- **WHEN** una entidad requiere consultas más allá del CRUD genérico (por ejemplo `IQuestionRepository`, `IExamRepository`)
- **THEN** la interfaz especializada se define en `TechEval.Domain` y su implementación en `TechEval.Infrastructure`, y ambas se registran explícitamente en `DependencyInjection.AddInfrastructure`

### Requirement: Inyección de dependencias centralizada por capa
Cada capa SHALL exponer su propia configuración de servicios mediante un método de extensión (por ejemplo `AddInfrastructure`) invocado desde `TechEval.API.Program`, en lugar de registrar dependencias de infraestructura directamente en la capa de entrada.

#### Scenario: Composición de la aplicación en el punto de entrada
- **WHEN** se inicia `TechEval.API` y se ejecuta `builder.Services.AddInfrastructure(builder.Configuration)`
- **THEN** quedan registrados en el contenedor `AppDbContext`, los repositorios genéricos y específicos, y los servicios de infraestructura (email, tokens) sin que `Program.cs` conozca sus implementaciones concretas

### Requirement: Manejo global de errores
La API SHALL interceptar toda excepción no controlada mediante un middleware (`ErrorHandlingMiddleware`) registrado al inicio del pipeline, traduciéndola a una respuesta HTTP en formato JSON con `error`, `statusCode` y `timestamp`, sin exponer detalles internos para errores no clasificados.

#### Scenario: Excepción de negocio conocida
- **WHEN** un controlador lanza `InvalidOperationException`, `UnauthorizedAccessException` o `KeyNotFoundException`
- **THEN** el middleware responde con `400`, `401` o `404` respectivamente, incluyendo el mensaje de la excepción en el cuerpo JSON

#### Scenario: Excepción no clasificada
- **WHEN** ocurre una excepción de un tipo no mapeado explícitamente
- **THEN** el middleware registra el error con el logger configurado y responde `500` con un mensaje genérico ("Ha ocurrido un error interno. Contacte al administrador.") sin filtrar el detalle de la excepción

### Requirement: Logging estructurado
La aplicación SHALL emitir logs estructurados mediante Serilog, configurados desde `appsettings.json` (`Serilog:MinimumLevel`), con salida simultánea a consola y a archivo con rotación diaria (`logs/techeval-.txt`).

#### Scenario: Arranque de la API con Serilog activo
- **WHEN** `TechEval.API` inicia y se invoca `builder.Host.UseSerilog()`
- **THEN** los eventos de log de la aplicación se escriben tanto en la consola como en un archivo `logs/techeval-{fecha}.txt`, respetando los niveles mínimos configurados por namespace (por ejemplo `Microsoft.EntityFrameworkCore` en `Warning`)

### Requirement: Documentación interactiva de la API
La API SHALL exponer documentación OpenAPI/Swagger generada automáticamente, incluyendo el esquema de seguridad Bearer JWT, disponible únicamente en entorno de desarrollo.

#### Scenario: Acceso a Swagger UI en desarrollo
- **WHEN** la aplicación se ejecuta con `Environment` en `Development`
- **THEN** los endpoints `/swagger` y `/swagger/v1/swagger.json` están disponibles y describen los controladores de la API, incluyendo el requisito de autenticación Bearer

#### Scenario: Swagger deshabilitado fuera de desarrollo
- **WHEN** la aplicación se ejecuta en un entorno distinto de `Development`
- **THEN** los endpoints de Swagger no se registran en el pipeline

