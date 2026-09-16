# authentication Specification

## Purpose
TBD - created by archiving change authentication. Update Purpose after archive.

## Requirements

### Requirement: Login de administrador con credenciales válidas
El sistema SHALL exponer `POST /api/auth/login` y, cuando el email corresponde a un usuario **activo** (administrador o alumno) y la contraseña coincide con el hash almacenado, SHALL responder `200 OK` con un JWT firmado, el nombre, el email y el rol del usuario.

#### Scenario: Administrador activo con contraseña correcta
- **GIVEN** un usuario con `Email = "admin@techeval.com"`, `IsAdmin = true`, `IsActive = true` y un `PasswordHash` correspondiente a `Admin@123!`
- **WHEN** se envía `POST /api/auth/login` con `{ "email": "admin@techeval.com", "password": "Admin@123!" }`
- **THEN** el sistema MUST responder `200 OK` con un cuerpo `AuthResultDto` que contiene un JWT no vacío, el `Name`, el `Email` y el rol `Admin`

#### Scenario: Alumno activo con contraseña correcta
- **GIVEN** un usuario con `Email = "alejandro.robles@pronet-ise.com"`, `IsAdmin = false`, `IsActive = true` y un `PasswordHash` correspondiente a `alejandro.robles`
- **WHEN** se envía `POST /api/auth/login` con `{ "email": "alejandro.robles@pronet-ise.com", "password": "alejandro.robles" }`
- **THEN** el sistema MUST responder `200 OK` con un cuerpo `AuthResultDto` que contiene un JWT no vacío, el `Name`, el `Email` y el rol `Alumno`

### Requirement: Rechazo de credenciales inválidas
El sistema SHALL responder `401 Unauthorized` cuando el email no existe, la contraseña no coincide con el hash almacenado, o el usuario no está activo, sin revelar cuál de las condiciones falló ni el rol del usuario.

#### Scenario: Contraseña incorrecta para un email existente
- **GIVEN** un usuario activo (administrador o alumno) con un email registrado
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
El sistema SHALL almacenar únicamente una derivación de la contraseña, nunca la contraseña en texto plano. La derivación SHALL usar PBKDF2-HMAC-SHA256 con una sal aleatoria de al menos 16 bytes generada por cada contraseña, y un coste de al menos 600 000 iteraciones. El valor almacenado SHALL identificar el algoritmo, el número de iteraciones y la sal, de modo que el coste pueda subirse más adelante sin invalidar los hashes existentes.

El sistema SHALL seguir verificando correctamente los hashes en el formato SHA-256 anterior, y SHALL reescribirlos en el formato nuevo tras el primer login correcto, sin pedir al usuario que cambie su contraseña.

Un `PasswordHash` vacío SHALL significar «esta cuenta no tiene contraseña utilizable», y la verificación MUST rechazarlo siempre, sea cual sea la contraseña recibida.

#### Scenario: Dos usuarios con la misma contraseña no comparten hash
- **GIVEN** dos usuarios a los que se asigna la misma contraseña
- **WHEN** el sistema calcula el hash de cada uno
- **THEN** los valores almacenados SHALL ser distintos, porque cada uno lleva su propia sal

#### Scenario: Verificación de una contraseña correcta
- **GIVEN** un usuario cuyo `PasswordHash` se generó con el esquema vigente
- **WHEN** se envía `POST /api/auth/login` con la contraseña correcta
- **THEN** el sistema MUST autorizar el login

#### Scenario: Verificación de una contraseña incorrecta
- **GIVEN** un usuario con un `PasswordHash` válido
- **WHEN** se envía `POST /api/auth/login` con una contraseña distinta
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Verificación de contraseña mediante hash SHA-256
- **GIVEN** un usuario cuyo `PasswordHash` es el SHA-256 hexadecimal de `Admin@123!`, guardado con el esquema anterior a este cambio
- **WHEN** se envía `POST /api/auth/login` con `password = "Admin@123!"`
- **THEN** el sistema MUST calcular el SHA-256 de la contraseña recibida y compararlo (sin distinguir mayúsculas y minúsculas) contra el hash almacenado, y autorizar el login
- **AND** ese camino existe solo para compatibilidad: ningún hash nuevo SHALL generarse con SHA-256

#### Scenario: Login con un hash en el formato SHA-256 anterior
- **GIVEN** un usuario cuyo `PasswordHash` es el SHA-256 hexadecimal de su contraseña, guardado antes de este cambio
- **WHEN** se envía `POST /api/auth/login` con la contraseña correcta
- **THEN** el sistema MUST autorizar el login
- **AND** el sistema SHALL reescribir el `PasswordHash` en el formato vigente antes de responder

#### Scenario: Cuenta sin contraseña utilizable
- **GIVEN** un usuario cuyo `PasswordHash` está vacío
- **WHEN** se envía `POST /api/auth/login` con cualquier contraseña, incluida la cadena vacía
- **THEN** el sistema MUST responder `401 Unauthorized`

### Requirement: Bloqueo de cuentas inactivas
El sistema SHALL negar el login a cualquier usuario cuyo campo `IsActive` sea `false`, aunque el email y la contraseña sean correctos.

#### Scenario: Intento de login con cuenta desactivada
- **GIVEN** un usuario administrador con `IsActive = false` y credenciales válidas
- **WHEN** se envía `POST /api/auth/login` con esas credenciales
- **THEN** el sistema MUST responder `401 Unauthorized`, ya que la consulta de login filtra explícitamente por `u.IsActive`

### Requirement: Aprovisionamiento de administrador inicial
El sistema SHALL crear automáticamente un usuario administrador por defecto la primera vez que la base de datos no contiene ningún usuario, usando el email `admin@techeval.com` y una contraseña configurable mediante `AdminPassword` en `appsettings.json` (valor por defecto `Admin@123!`). Su `PasswordHash` SHALL generarse con el esquema vigente.

#### Scenario: Primera ejecución con base de datos vacía
- **GIVEN** una base de datos sin registros en la tabla `Users`
- **WHEN** se ejecuta `DbSeeder.SeedAsync` durante el arranque de la aplicación
- **THEN** el sistema MUST insertar un usuario con `Email = "admin@techeval.com"`, `IsAdmin = true`, `IsActive = true` y un `PasswordHash` derivado de `AdminPassword` con PBKDF2 y sal propia

### Requirement: Aprovisionamiento automático de cuenta de alumno
El sistema SHALL crear automáticamente un `User` con `IsAdmin = false` la primera vez que se abre una invitación de examen (`GET /api/exam/validate/{token}`) para un email que no corresponde a ningún usuario existente, derivando el `username` de la parte local del email. La cuenta SHALL nacer **sin contraseña utilizable**: el sistema MUST NOT derivar ninguna contraseña del email, del nombre ni de ningún otro dato conocido del candidato.

El acceso del candidato es el enlace de la invitación, que ya emite su sesión autenticada.

#### Scenario: Primera apertura de una invitación con email nuevo
- **GIVEN** un `ExamToken` válido con `CandidateEmail = "alejandro.robles@pronet-ise.com"` y ningún `User` existente con ese email
- **WHEN** se invoca `GET /api/exam/validate/{token}` con ese token
- **THEN** el sistema MUST crear un `User` con `Email = "alejandro.robles@pronet-ise.com"`, `Name` igual al `CandidateName` de la invitación, `IsAdmin = false`, `IsActive = true` y sin contraseña utilizable

#### Scenario: La cuenta recién creada no permite entrar con datos del candidato
- **GIVEN** una cuenta de alumno recién aprovisionada para `alejandro.robles@pronet-ise.com`
- **WHEN** alguien envía `POST /api/auth/login` con esa cuenta y la contraseña `"alejandro.robles"`, o cualquier otro dato derivado de su email o de su nombre
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Apertura de una invitación con email ya existente
- **GIVEN** un `ExamToken` válido cuyo `CandidateEmail` ya corresponde a un `User` existente
- **WHEN** se invoca `GET /api/exam/validate/{token}` con ese token
- **THEN** el sistema MUST reutilizar el `User` existente sin modificar su `Name` ni su `PasswordHash`, y SHALL NOT crear un `User` duplicado

### Requirement: Auto-login al abrir la invitación
El sistema SHALL emitir un JWT válido para el `User` del alumno (creado o reutilizado) como parte de la respuesta de `GET /api/exam/validate/{token}`, de forma que el candidato quede autenticado sin necesidad de introducir credenciales.

#### Scenario: Validación exitosa devuelve sesión autenticada
- **WHEN** `GET /api/exam/validate/{token}` procesa un token válido
- **THEN** la respuesta MUST incluir un JWT firmado para el `User` asociado, con el rol `Alumno`, además de la información de la prueba ya especificada

### Requirement: Autorización por rol Alumno en endpoints del portal
El sistema SHALL exigir un JWT válido con rol `Alumno` para acceder a los endpoints del portal del alumno (pruebas pendientes y realizadas) **y a los endpoints de resolución de la prueba que operan sobre una sesión ya iniciada** (guardado de respuestas y envío), y SHALL responder `401 Unauthorized` (sin token válido) o `403 Forbidden` (token válido sin rol `Alumno`) en caso contrario.

Quedan fuera de esta exigencia los endpoints que operan con el token del enlace de invitación —la validación del token y el inicio de la sesión—, donde la credencial es ese token y no una sesión autenticada.

#### Scenario: Petición sin token a un endpoint del portal
- **WHEN** se envía una petición a un endpoint del portal del alumno sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Token de administrador usado contra un endpoint del portal
- **GIVEN** un JWT válido emitido para un usuario con `IsAdmin = true`
- **WHEN** se envía una petición a un endpoint del portal del alumno con ese token
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Petición sin token a un endpoint de resolución de la prueba
- **WHEN** se envía una petición de guardado de respuesta o de envío de la prueba sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Los endpoints del enlace de invitación siguen siendo públicos
- **WHEN** se envía una petición de validación de token o de inicio de sesión sin header `Authorization`
- **THEN** el sistema MUST procesarla, porque la credencial de esas operaciones es el token del enlace
