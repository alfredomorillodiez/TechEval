# authentication Specification

## Purpose
TBD - created by archiving change authentication. Update Purpose after archive.
## Requirements
### Requirement: Login de administrador con credenciales válidas
El sistema SHALL exponer `POST /api/auth/login` y, cuando el email corresponde a un usuario activo con rol de administrador y la contraseña coincide con el hash almacenado, SHALL responder `200 OK` con un JWT firmado, el nombre y el email del usuario.

#### Scenario: Administrador activo con contraseña correcta
- **GIVEN** un usuario con `Email = "admin@techeval.com"`, `IsAdmin = true`, `IsActive = true` y un `PasswordHash` correspondiente a `Admin@123!`
- **WHEN** se envía `POST /api/auth/login` con `{ "email": "admin@techeval.com", "password": "Admin@123!" }`
- **THEN** el sistema MUST responder `200 OK` con un cuerpo `AuthResultDto` que contiene un JWT no vacío, el `Name` y el `Email` del usuario

### Requirement: Rechazo de credenciales inválidas
El sistema SHALL responder `401 Unauthorized` cuando el email no existe, la contraseña no coincide con el hash almacenado, o el usuario no cumple los criterios de administrador activo, sin revelar cuál de las condiciones falló.

#### Scenario: Contraseña incorrecta para un email existente
- **GIVEN** un usuario administrador activo con email `admin@techeval.com`
- **WHEN** se envía `POST /api/auth/login` con la contraseña incorrecta
- **THEN** el sistema MUST responder `401 Unauthorized` con `{ "error": "Credenciales incorrectas." }`

#### Scenario: Email no registrado
- **GIVEN** que no existe ningún usuario con `Email = "desconocido@techeval.com"`
- **WHEN** se envía `POST /api/auth/login` con ese email y cualquier contraseña
- **THEN** el sistema MUST responder `401 Unauthorized` con el mismo mensaje genérico usado para contraseña incorrecta

### Requirement: Expiración del token JWT
El sistema SHALL emitir tokens JWT firmados con HMAC-SHA256 cuya expiración sea configurable mediante `Jwt:ExpirationHours` (por defecto 8 horas desde el momento de la emisión), y SHALL rechazar como inválido cualquier token cuya fecha de expiración ya haya pasado.

#### Scenario: Token generado con expiración de 8 horas
- **GIVEN** la configuración por defecto `Jwt:ExpirationHours = 8`
- **WHEN** `TokenService.GenerateJwtToken` emite un token para un usuario
- **THEN** el claim `exp` del JWT MUST corresponder a `DateTime.UtcNow` más 8 horas en el momento de la emisión

#### Scenario: Validación de un token expirado
- **GIVEN** un JWT válido cuya fecha de expiración ya pasó
- **WHEN** se invoca `TokenService.ValidateJwtToken` con ese token
- **THEN** el sistema MUST devolver `null`, indicando que el token no es válido

### Requirement: Autorización por rol Admin en endpoints de gestión
El sistema SHALL exigir un JWT válido con rol `Admin` para acceder a los endpoints de gestión de categorías, preguntas, exámenes y resultados, y SHALL responder `401 Unauthorized` (sin token válido) o `403 Forbidden` (token válido sin rol `Admin`) en caso contrario.

#### Scenario: Petición sin token a un endpoint de gestión
- **GIVEN** los controladores `CategoriesController`, `QuestionsController`, `ExamsController` y `ResultsController`, todos decorados con `[Authorize(Roles = "Admin")]`
- **WHEN** se envía una petición a cualquiera de sus endpoints sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Token válido sin claim de rol Admin
- **GIVEN** un JWT válido emitido para un usuario con `IsAdmin = false` (claim `Role = "User"`)
- **WHEN** se envía una petición a un endpoint de gestión con ese token en el header `Authorization`
- **THEN** el sistema MUST responder `403 Forbidden`

### Requirement: Hash de contraseña
El sistema SHALL almacenar únicamente el hash SHA-256 (en hexadecimal) de la contraseña del usuario, nunca la contraseña en texto plano, y SHALL calcular el mismo hash sobre la contraseña recibida en el login para compararlo contra el valor almacenado.

#### Scenario: Verificación de contraseña mediante hash SHA-256
- **GIVEN** un usuario cuyo `PasswordHash` es el resultado de `SHA256.HashData` sobre `Admin@123!` codificado en UTF-8 y convertido a hexadecimal
- **WHEN** se envía `POST /api/auth/login` con `password = "Admin@123!"`
- **THEN** `AuthController.VerifyPassword` MUST calcular el hash SHA-256 de la contraseña recibida y compararlo (sin distinguir mayúsculas/minúsculas) contra el hash almacenado antes de autorizar el login

### Requirement: Bloqueo de cuentas inactivas
El sistema SHALL negar el login a cualquier usuario cuyo campo `IsActive` sea `false`, aunque el email y la contraseña sean correctos.

#### Scenario: Intento de login con cuenta desactivada
- **GIVEN** un usuario administrador con `IsActive = false` y credenciales válidas
- **WHEN** se envía `POST /api/auth/login` con esas credenciales
- **THEN** el sistema MUST responder `401 Unauthorized`, ya que la consulta de login filtra explícitamente por `u.IsActive`

### Requirement: Aprovisionamiento de administrador inicial
El sistema SHALL crear automáticamente un usuario administrador por defecto la primera vez que la base de datos no contiene ningún usuario, usando el email `admin@techeval.com` y una contraseña configurable mediante `AdminPassword` en `appsettings.json` (valor por defecto `Admin@123!`).

#### Scenario: Primera ejecución con base de datos vacía
- **GIVEN** una base de datos sin registros en la tabla `Users`
- **WHEN** se ejecuta `DbSeeder.SeedAsync` durante el arranque de la aplicación
- **THEN** el sistema MUST insertar un usuario con `Email = "admin@techeval.com"`, `IsAdmin = true`, `IsActive = true` y `PasswordHash` igual al hash SHA-256 de `AdminPassword`

