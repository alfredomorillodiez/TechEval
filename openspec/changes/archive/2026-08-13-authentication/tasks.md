## 1. Modelo de dominio
- [x] 1.1 Definir la entidad `User` (`TechEval.Domain.Entities.User`) heredando de `AuditableEntity`, con `Email`, `PasswordHash`, `Name`, `IsAdmin` e `IsActive`.
- [x] 1.2 Registrar `User` en `AppDbContext` (`DbSet<User> Users`) y su mapeo EF Core.

## 2. Servicio de tokens JWT
- [x] 2.1 Definir el contrato `ITokenService` (`GenerateJwtToken`, `ValidateJwtToken`, `GenerateSecureToken`) en `TechEval.Domain.Interfaces.Services`.
- [x] 2.2 Implementar `TokenService` en `TechEval.Infrastructure.Security` usando `JwtSecurityTokenHandler` y firma `HmacSha256` sobre `JwtSettings` (`SecretKey`, `Issuer`, `Audience`, `ExpirationHours`).
- [x] 2.3 Incluir en el JWT los claims `NameIdentifier`, `Email`, `Role` (`Admin`/`User`) e `isAdmin`.
- [x] 2.4 Registrar `TokenService` en el contenedor de DI (`AddInfrastructure`) y enlazar `JwtSettings` a la sección `Jwt` de configuración.

## 3. Endpoint de login
- [x] 3.1 Implementar `AuthController` con `POST /api/auth/login` recibiendo `LoginDto` (email, password).
- [x] 3.2 Consultar el usuario activo y administrador por email (`Users.FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive && u.IsAdmin)`).
- [x] 3.3 Implementar `VerifyPassword` calculando SHA-256 de la contraseña recibida y comparándola contra `PasswordHash`.
- [x] 3.4 Devolver `401 Unauthorized` con mensaje genérico ante credenciales inválidas, o `200 OK` con `AuthResultDto` (token, nombre, email) en caso de éxito.

## 4. Middleware y autorización
- [x] 4.1 Configurar `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` en `Program.cs` validando emisor, audiencia, firma y expiración (`ClockSkew = TimeSpan.Zero`).
- [x] 4.2 Registrar `AddAuthorization()` y activar los middlewares de autenticación/autorización en el pipeline HTTP.
- [x] 4.3 Proteger `CategoriesController`, `QuestionsController`, `ExamsController` y `ResultsController` con `[Authorize(Roles = "Admin")]` a nivel de controlador.

## 5. Aprovisionamiento del administrador inicial
- [x] 5.1 Calcular el hash SHA-256 de `AdminPassword` (configuración, valor por defecto `Admin@123!`) en el arranque de `Program.cs`.
- [x] 5.2 Implementar `DbSeeder.SeedAsync` para insertar el usuario `admin@techeval.com` (`IsAdmin = true`, `IsActive = true`) solo si la tabla `Users` está vacía.

## 6. Configuración
- [x] 6.1 Añadir la sección `Jwt` (`SecretKey`, `Issuer`, `Audience`, `ExpirationHours`) y la clave `AdminPassword` a `appsettings.json`.

## 7. Verificación manual
- [x] 7.1 Verificar mediante Swagger/HTTP manual que `POST /api/auth/login` con las credenciales del admin sembrado devuelve un JWT válido y que ese JWT autoriza correctamente contra los endpoints `[Authorize(Roles = "Admin")]`.
