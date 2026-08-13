## Context
TechEval es una plataforma de evaluación técnica que debe soportar múltiples tipos de pregunta, exámenes con sesiones controladas por token, y un flujo de resultados auditable. Se espera que el catálogo de entidades y reglas de negocio crezca (nuevos tipos de pregunta, nuevos proveedores de notificación, posibles cambios de motor de base de datos), por lo que la estructura de proyecto elegida al inicio condiciona el costo de esos cambios futuros. Se decide fijar Clean Architecture con .NET 9, EF Core sobre SQL Server, y Blazor WebAssembly como cliente, antes de construir cualquier funcionalidad de negocio.

## Goals / Non-Goals
**Goals**
- Aislar las reglas de negocio (`Domain`, `Application`) de los detalles técnicos (`Infrastructure`, `API`, `Web`).
- Permitir sustituir la tecnología de persistencia o el proveedor de correo sin modificar `Domain` ni `Application`.
- Ofrecer un mecanismo de acceso a datos reutilizable (repositorio genérico) que reduzca código repetido en operaciones CRUD.
- Garantizar una experiencia de error y logging consistente en toda la API desde el primer endpoint.

**Non-Goals**
- No se busca CQRS ni un bus de mediación (MediatR) en esta etapa: los servicios de `Application` invocan repositorios directamente.
- No se implementa un ORM-agnostic query layer más allá de `Expression<Func<T,bool>>` en `IRepository<T>`.
- No se cubre en esta capacidad el modelo de datos de negocio (usuarios, exámenes, preguntas) ni la autenticación JWT en detalle — quedan para capacidades posteriores que se apoyan en esta base.

## Decisions

### Clean Architecture en lugar de capas simples
Se adopta Clean Architecture (Domain → Application → Infrastructure/API/Web) en lugar de una separación de capas más laxa porque invierte la dirección de las dependencias: `Infrastructure` depende de `Domain`, no al revés. `Domain` declara interfaces (`IRepository<T>`, `IQuestionRepository`, `IEmailService`, `ITokenService`) y `Infrastructure` las implementa junto con EF Core, SQL Server y SMTP. Esto permite, en teoría, sustituir EF Core por Dapper o SQL Server por PostgreSQL tocando solo `Infrastructure`, sin que `Application` ni `Domain` se enteren. En un proyecto de evaluación técnica con expectativa de crecimiento, ese desacoplamiento vale el costo adicional de indirección que introduce.

### Repository Pattern genérico (`IRepository<T>` + `BaseRepository<T>`)
Se centraliza el CRUD común (`GetByIdAsync`, `GetAllAsync`, `FindAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `CountAsync`) en una interfaz genérica implementada una sola vez sobre `AppDbContext`/`DbSet<T>`. Las entidades que necesitan consultas específicas (por ejemplo `IQuestionRepository`, `IExamRepository`) extienden el contrato en `Domain` y su implementación específica hereda o compone `BaseRepository<T>` en `Infrastructure`. Se evita así reescribir el mismo CRUD por cada entidad, sin renunciar a extender el contrato cuando una entidad lo requiere.

### `records` para DTOs (referencia: no se implementan en este cambio, pero se fija la convención)
Los DTOs que cruzan de `Application` hacia `API`/`Web` se modelan como `record` de C# porque son inmutables por defecto, tienen igualdad por valor y soportan deconstrucción, lo que resulta natural para objetos de transferencia que no deben mutar una vez creados. Si en el futuro un DTO acumula muchas propiedades opcionales y la sintaxis de `record` deja de ser cómoda, se evaluará su conversión a clase normal caso por caso.

### Mapeos manuales en lugar de AutoMapper
Se descarta AutoMapper para evitar la "magia" implícita de mapeo por convención, que dificulta depurar errores de mapeo y esconde el acoplamiento real entre entidad y DTO. Los servicios de `Application` mapean manualmente entidad ↔ DTO, lo que resulta explícito, fácil de testear con datos concretos y no añade una dependencia externa. Se acepta el costo de escribir y mantener el mapeo a mano; si el número de entidades creciera a decenas con mapeos repetitivos, se reconsiderará.

### Middleware de errores centralizado en vez de manejo por controlador
Un único `ErrorHandlingMiddleware` en `TechEval.API` traduce excepciones a respuestas HTTP homogéneas (`InvalidOperationException`→400, `UnauthorizedAccessException`→401, `KeyNotFoundException`→404, cualquier otra→500 genérico), evitando bloques `try/catch` repetidos en cada controlador y garantizando que ningún detalle interno se filtre en errores no clasificados.

### Serilog en lugar del logging por defecto de ASP.NET Core
Se configura Serilog con sinks de consola y archivo (rotación diaria) leyendo niveles mínimos desde `appsettings.json`, porque ofrece enriquecimiento estructurado (`Enrich.FromLogContext`) y persistencia en archivo out-of-the-box, útil para diagnosticar incidentes en el entorno de examen sin depender de un stack de observabilidad externo desde el día uno.

## Risks / Trade-offs
- **Indirección adicional**: el repositorio genérico y la inversión de dependencias añaden capas de abstracción que pueden sentirse "de más" para operaciones CRUD triviales; se acepta a cambio de testabilidad y reemplazabilidad de `Infrastructure`.
- **`IRepository<T>` puede quedar corto** para consultas complejas (joins, proyecciones); la mitigación es permitir repositorios especializados (`IQuestionRepository`, etc.) que convivan con el genérico en lugar de forzar todo a través de `Expression<Func<T,bool>>`.
- **Mapeo manual escala mal** si el número de entidades y DTOs crece mucho; se documenta el punto de reevaluación (introducir AutoMapper) en lugar de prohibirlo de forma permanente.
- **Logging a archivo local** (`logs/techeval-*.txt`) no centraliza logs entre instancias si la API se escala horizontalmente; válido para el tamaño actual del proyecto, pendiente de revisión si se despliega en múltiples nodos.
- **Middleware de errores con mapeo fijo de excepciones**: nuevas excepciones de dominio que no se agreguen al `switch` caerán en el `500` genérico; requiere disciplina para mantener el mapeo actualizado a medida que crecen las reglas de negocio.
