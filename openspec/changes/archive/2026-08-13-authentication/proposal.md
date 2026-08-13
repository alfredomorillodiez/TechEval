## Why
TechEval necesita restringir la gestión de categorías, preguntas, exámenes y resultados a personal autorizado, evitando que cualquier visitante de la API pueda crear o modificar el banco de preguntas o los exámenes de evaluación. Se requiere un mecanismo de autenticación stateless que se integre con el pipeline de ASP.NET Core y que permita a `TechEval.Web` (Blazor WASM) mantener una sesión de administrador sin depender de estado en servidor.

## What Changes
- Añadir la entidad `User` (`TechEval.Domain.Entities.User`) con `Email`, `PasswordHash`, `Name`, `IsAdmin` e `IsActive`, heredando de `AuditableEntity`.
- Implementar `POST /api/auth/login` en `AuthController`: valida email + contraseña contra usuarios activos y con rol admin, y devuelve un JWT junto con nombre y email (`AuthResultDto`).
- Hashear la contraseña con SHA-256 (`VerifyPassword` en `AuthController`) y comparar el hash calculado contra el almacenado en BD.
- Implementar `ITokenService` / `TokenService` (`TechEval.Infrastructure.Security`) para emitir y validar JWT firmados con HMAC-SHA256, con claims de `NameIdentifier`, `Email`, `Role` (`Admin`/`User`) e `isAdmin`, y expiración configurable (`Jwt:ExpirationHours`, por defecto 8 horas).
- Configurar el middleware de autenticación/autorización JWT Bearer en `Program.cs` (`AddAuthentication` + `AddJwtBearer` + `AddAuthorization`), validando emisor, audiencia, firma y tiempo de vida.
- Proteger con `[Authorize(Roles = "Admin")]` los controladores de gestión (`CategoriesController`, `QuestionsController`, `ExamsController`, `ResultsController`).
- Sembrar un usuario administrador por defecto (`admin@techeval.com`) en `DbSeeder.SeedAsync`, con contraseña configurable vía `AdminPassword` en `appsettings.json` (por defecto `Admin@123!`).

## Capabilities
### New Capabilities
- `authentication`: Autenticación de administradores mediante JWT (login por email/contraseña, emisión y validación de tokens HS256, protección por rol `Admin` de los endpoints de gestión) y aprovisionamiento de un usuario administrador inicial vía seed.

### Modified Capabilities
(ninguna)

## Impact
- **Código afectado**: `src/TechEval.Domain/Entities/User.cs`, `src/TechEval.Domain/Interfaces/Services/ITokenService.cs`, `src/TechEval.Infrastructure/Security/TokenService.cs`, `src/TechEval.API/Controllers/AuthController.cs`, `src/TechEval.API/Program.cs`, `src/TechEval.Infrastructure/Data/DbSeeder.cs`, controladores `CategoriesController`, `QuestionsController`, `ExamsController`, `ResultsController`.
- **Dependencias añadidas**: `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt`.
- **Configuración**: sección `Jwt` (`SecretKey`, `Issuer`, `Audience`, `ExpirationHours`) y clave `AdminPassword` en `appsettings.json`.
- **Consumidores**: `TechEval.Web` (Blazor WASM) debe adjuntar el JWT como header `Authorization: Bearer <token>` en las llamadas a endpoints de gestión.
