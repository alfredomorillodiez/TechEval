## 1. Estructura de la solución
- [x] 1.1 Crear `TechEval.sln` con los proyectos `TechEval.Domain`, `TechEval.Application`, `TechEval.Infrastructure`, `TechEval.API`, `TechEval.Web` y `TechEval.Tests`
- [x] 1.2 Configurar referencias de proyecto respetando la regla de dependencia: `Application` → `Domain`; `Infrastructure` → `Domain`; `API` → `Application` + `Infrastructure`

## 2. Capa de dominio
- [x] 2.1 Crear entidad base auditable `AuditableEntity` (`CreatedAt`, `UpdatedAt`) en `TechEval.Domain.Common`
- [x] 2.2 Definir interfaz genérica de repositorio `IRepository<T>` (`GetByIdAsync`, `GetAllAsync`, `FindAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `CountAsync`) en `TechEval.Domain.Interfaces.Repositories`
- [x] 2.3 Definir interfaces de repositorios especializados (`IQuestionRepository`, `IExamRepository`, `IExamTokenRepository`, `IExamResultRepository`) y de servicios de infraestructura (`IEmailService`, `ITokenService`) en `TechEval.Domain.Interfaces`

## 3. Capa de infraestructura
- [x] 3.1 Implementar `AppDbContext` (EF Core) con los `DbSet` de todas las entidades y carga de configuraciones vía `ApplyConfigurationsFromAssembly`
- [x] 3.2 Implementar repositorio genérico reutilizable `BaseRepository<T>` sobre `AppDbContext`/`DbSet<T>`
- [x] 3.3 Implementar repositorios especializados sobre `BaseRepository<T>` para las entidades que requieren consultas adicionales
- [x] 3.4 Centralizar el registro de infraestructura en `DependencyInjection.AddInfrastructure` (DbContext con SQL Server, repositorios, `EmailSettings`/`JwtSettings`, `IEmailService`, `ITokenService`)

## 4. Capa de API
- [x] 4.1 Configurar `Program.cs`: creación del `WebApplicationBuilder`, carga de `appsettings.Local.json` opcional
- [x] 4.2 Implementar `ErrorHandlingMiddleware` con mapeo de excepciones a códigos HTTP (`400`, `401`, `404`, `500`) y respuesta JSON uniforme (`error`, `statusCode`, `timestamp`)
- [x] 4.3 Registrar el middleware de errores al inicio del pipeline (`app.UseMiddleware<ErrorHandlingMiddleware>()`)
- [x] 4.4 Configurar autenticación/autorización JWT Bearer y política de CORS (`BlazorPolicy`) para el cliente Blazor

## 5. Logging y observabilidad
- [x] 5.1 Integrar Serilog (`ReadFrom.Configuration`, `Enrich.FromLogContext`) leyendo niveles mínimos desde `appsettings.json`
- [x] 5.2 Configurar sinks de consola y de archivo con rotación diaria (`logs/techeval-.txt`)

## 6. Documentación de API
- [x] 6.1 Configurar Swagger/OpenAPI (`AddEndpointsApiExplorer`, `AddSwaggerGen`) con esquema de seguridad Bearer JWT
- [x] 6.2 Incluir comentarios XML de los controladores en la documentación generada
- [x] 6.3 Habilitar Swagger UI únicamente en entorno de desarrollo

## 7. Configuración y arranque
- [x] 7.1 Definir `appsettings.json` con cadena de conexión, sección `Serilog`, `Jwt`, `Email`, `AllowedOrigins` y `AdminPassword`
- [x] 7.2 Ejecutar seed inicial de base de datos (`DbSeeder.SeedAsync`) al arrancar la aplicación
