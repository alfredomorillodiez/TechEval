## Why
TechEval necesita una base arquitectónica que permita evolucionar reglas de negocio, proveedores de persistencia y canales de entrada (API REST, futuras integraciones) sin que un cambio en una capa obligue a reescribir las demás. Sin esa separación, el acoplamiento entre lógica de negocio y detalles de infraestructura (EF Core, SQL Server, SMTP) haría cada cambio más caro y frágil a medida que crezca el catálogo de preguntas, exámenes y usuarios.

## What Changes
- Crear la solución `.NET 9` con 5 proyectos en capas: `TechEval.Domain`, `TechEval.Application`, `TechEval.Infrastructure`, `TechEval.API` y `TechEval.Web` (Blazor WebAssembly), más `TechEval.Tests`.
- Aplicar **Dependency Inversion**: `Domain` no depende de ninguna otra capa; `Application` sólo conoce interfaces de `Domain`; `Infrastructure` depende de `Domain` e implementa sus interfaces (repositorios, servicios) sin que `Domain` conozca EF Core ni SQL Server.
- Definir una entidad base auditable (`AuditableEntity` con `CreatedAt`/`UpdatedAt`) y un contrato de **Repository Pattern genérico** (`IRepository<T>`) con implementación reutilizable (`BaseRepository<T>`) sobre `AppDbContext` (EF Core + SQL Server).
- Registrar la composición de dependencias en `TechEval.Infrastructure.DependencyInjection` (`AddInfrastructure`): `DbContext`, repositorios genéricos y específicos, y servicios de infraestructura (email, tokens JWT).
- Añadir un middleware global de manejo de errores (`ErrorHandlingMiddleware`) que traduce excepciones de dominio a respuestas HTTP JSON consistentes (`400`, `401`, `404`, `500`).
- Configurar logging estructurado con **Serilog** (consola + archivo con rotación diaria) leído desde `appsettings.json`.
- Configurar **Swagger/OpenAPI** con autenticación Bearer JWT y comentarios XML para documentar la API automáticamente.
- Configurar el pipeline de `Program.cs`: middleware de errores, Swagger (solo en desarrollo), HTTPS redirection, CORS para el cliente Blazor, autenticación/autorización JWT y seed inicial de base de datos.

## Capabilities
### New Capabilities
- `project-architecture`: Estructura de solución en capas (Clean Architecture), reglas de dependencia entre capas, patrón de repositorio genérico, manejo global de errores, logging y documentación de API — la base sobre la que se construyen el resto de capacidades funcionales de TechEval.

### Modified Capabilities
(ninguna — esta es la primera capacidad del proyecto)

## Impact
- **Código afectado**: toda la solución `TechEval.sln` y los proyectos `src/TechEval.Domain`, `src/TechEval.Application`, `src/TechEval.Infrastructure`, `src/TechEval.API`, `src/TechEval.Web`, `tests/TechEval.Tests`.
- **Dependencias añadidas**: Entity Framework Core (SQL Server provider), Serilog (`Serilog.AspNetCore`, sinks de consola y archivo), Swashbuckle (Swagger/OpenAPI), `Microsoft.AspNetCore.Authentication.JwtBearer`.
- **Sistemas externos**: SQL Server (persistencia vía `AppDbContext`), sistema de archivos (`logs/techeval-*.txt`).
- **Configuración**: `appsettings.json` (cadena de conexión, sección `Serilog`, `AllowedOrigins`, `Jwt`).
