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

### Requirement: Los errores se traducen a HTTP en un solo sitio
El sistema SHALL traducir excepciones a códigos de estado HTTP en un único punto del pipeline. Un controlador NEVER SHALL elegir el código de estado de una excepción de negocio por su cuenta, porque el mismo error acabaría mapeado de dos formas según por dónde saliera.

La traducción SHALL apoyarse en una jerarquía de excepciones de aplicación que exprese **intención** —validación, recurso no encontrado, conflicto de estado, operación prohibida— y NEVER SHALL apoyarse en tipos de excepción genéricos de la plataforma o de sus bibliotecas.

Toda excepción ajena a esa jerarquía SHALL producir `500 Internal Server Error` con un mensaje genérico. Su detalle SHALL quedar en el registro del servidor y NEVER SHALL viajar al cliente: el mensaje de una excepción de infraestructura puede contener nombres de entidad, fragmentos de consulta o rutas internas.

Las respuestas de error SHALL usar el formato `ProblemDetails`.

#### Scenario: Error de validación de negocio
- **GIVEN** una operación que incumple una regla de negocio, como pedir más preguntas de las que hay disponibles
- **WHEN** el cliente la solicita
- **THEN** el sistema responde `400 Bad Request` con el mensaje de la regla incumplida

#### Scenario: Recurso inexistente
- **GIVEN** una operación que referencia un examen, una sesión o un resultado que no existe
- **WHEN** el cliente la solicita
- **THEN** el sistema responde `404 Not Found`

#### Scenario: Conflicto con el estado actual
- **GIVEN** una operación que el estado del sistema no permite, como corregir un resultado ya corregido o eliminar una opción que un candidato eligió
- **WHEN** el cliente la solicita
- **THEN** el sistema responde `409 Conflict` con el motivo

#### Scenario: Operación prohibida
- **GIVEN** una operación sobre un recurso que no pertenece a quien la solicita
- **WHEN** el cliente la solicita
- **THEN** el sistema responde `403 Forbidden`

#### Scenario: Fallo de infraestructura no llega al cliente
- **GIVEN** un fallo ajeno al negocio, como una excepción de EF Core al perderse la conexión
- **WHEN** ocurre durante una petición
- **THEN** el sistema responde `500 Internal Server Error` con un mensaje genérico
- **AND** el mensaje de la excepción original SHALL NOT aparecer en la respuesta
- **AND** el detalle SHALL quedar registrado en el servidor

#### Scenario: Un tipo genérico ya no se confunde con un error de negocio
- **GIVEN** una `InvalidOperationException` lanzada por una biblioteca, no por el código de negocio
- **WHEN** llega al pipeline de errores
- **THEN** el sistema responde `500`, y NEVER SHALL responder `400` con su mensaje

### Requirement: El código se compila y se prueba en cada subida
El repositorio SHALL incluir un flujo de trabajo de integración continua que, en cada subida a una rama del repositorio y en cada solicitud de incorporación, restaure las dependencias, compile la solución completa y ejecute la batería de pruebas.

El flujo SHALL fallar cuando la compilación falle o cuando alguna prueba falle, de forma que un cambio que rompa algo se detecte antes de fusionarlo y no al usarlo.

#### Scenario: Subida con la batería en verde
- **WHEN** alguien sube un cambio que compila y cuyas pruebas pasan
- **THEN** el flujo SHALL terminar correctamente

#### Scenario: Subida que rompe una prueba
- **GIVEN** un cambio que hace fallar una prueba de la batería
- **WHEN** se sube o se abre una solicitud de incorporación
- **THEN** el flujo SHALL fallar señalando la prueba que no pasa

#### Scenario: Subida que no compila
- **WHEN** se sube un cambio que no compila
- **THEN** el flujo SHALL fallar en el paso de compilación, sin llegar a ejecutar las pruebas

### Requirement: La restauración de paquetes no depende de un feed privado
El repositorio SHALL declarar sus orígenes de paquetes en un `NuGet.config` propio que descarte los heredados de la máquina y deje únicamente los públicos que el proyecto necesita.

Sin esa declaración, la restauración hereda los feeds configurados en el equipo de quien compila. En esta organización eso incluye feeds privados que exigen credenciales, y la restauración falla con `401` aunque todos los paquetes del proyecto sean públicos.

#### Scenario: Restauración en una máquina sin credenciales
- **GIVEN** una máquina sin credenciales para los feeds privados de la organización
- **WHEN** se ejecuta la restauración de paquetes del repositorio
- **THEN** la restauración SHALL completarse, porque el repositorio declara sus propios orígenes

#### Scenario: Restauración en la integración continua
- **GIVEN** un ejecutor de integración continua, que nunca tiene esas credenciales
- **WHEN** el flujo restaura las dependencias
- **THEN** la restauración SHALL completarse sin configuración adicional
