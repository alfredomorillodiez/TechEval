## ADDED Requirements

### Requirement: Rol único de cada usuario
Cada usuario SHALL tener exactamente un rol de estos tres: `Admin`, `Evaluador` o `Alumno`. El rol SHALL guardarse como un dato propio del usuario, y SHALL NOT deducirse de ningún otro campo. El JWT SHALL llevar el rol del usuario en el claim de rol estándar, con uno de esos tres valores literales.

#### Scenario: Token de un evaluador
- **GIVEN** un usuario activo con rol `Evaluador` y contraseña fijada
- **WHEN** inicia sesión con su email y su contraseña
- **THEN** el JWT emitido lleva el claim de rol con el valor `Evaluador`

#### Scenario: Ningún usuario sin rol
- **WHEN** se intenta guardar un usuario sin rol o con un valor de rol que no es uno de los tres
- **THEN** la base de datos rechaza la escritura

### Requirement: Revocación inmediata de los tokens
El JWT SHALL llevar un sello de seguridad del usuario. En cada petición autenticada, la API SHALL comprobar que el usuario del token existe, está activo, tiene el mismo rol que indica el token y tiene el mismo sello. Si alguna comprobación falla, la API SHALL responder `401 Unauthorized`. Un token sin sello SHALL rechazarse.

El sistema SHALL cambiar el sello de un usuario cuando cambia su rol, cuando se desactiva su cuenta, cuando se restablece su acceso y cuando fija una contraseña nueva. Así, un token deja de valer en el acto, sin esperar a su caducidad.

#### Scenario: Token de un usuario desactivado
- **GIVEN** un JWT vigente de un evaluador
- **WHEN** un administrador desactiva al evaluador y el evaluador envía una petición con ese JWT
- **THEN** la API responde `401 Unauthorized`

#### Scenario: Token con un rol que ya no corresponde
- **GIVEN** un JWT vigente emitido cuando el usuario tenía el rol `Admin`
- **WHEN** un administrador cambia su rol a `Evaluador` y el usuario envía una petición con ese JWT
- **THEN** la API responde `401 Unauthorized`

#### Scenario: Token emitido antes de este cambio
- **GIVEN** un JWT vigente emitido por la versión anterior de la API, sin sello de seguridad
- **WHEN** se envía una petición autenticada con ese JWT
- **THEN** la API responde `401 Unauthorized`

#### Scenario: Token vigente de un usuario sin cambios
- **GIVEN** un JWT vigente de un usuario cuyo rol, estado y sello no han cambiado
- **WHEN** envía una petición autenticada
- **THEN** la API procesa la petición con normalidad

## MODIFIED Requirements

### Requirement: Login de administrador con credenciales válidas
El sistema SHALL exponer `POST /api/auth/login` y, cuando el email corresponde a un usuario **activo** de cualquier rol y la contraseña coincide con el hash almacenado, SHALL responder `200 OK` con un JWT firmado, el nombre, el email y el rol del usuario (`Admin`, `Evaluador` o `Alumno`). Una cuenta sin contraseña utilizable SHALL NOT poder iniciar sesión por esta vía.

#### Scenario: Administrador activo con contraseña correcta
- **GIVEN** un usuario con `Email = "admin@techeval.com"`, rol `Admin`, `IsActive = true` y un `PasswordHash` correspondiente a la contraseña configurada en `AdminPassword`
- **WHEN** se envía `POST /api/auth/login` con ese email y esa contraseña
- **THEN** el sistema MUST responder `200 OK` con un cuerpo `AuthResultDto` que contiene un JWT no vacío, el `Name`, el `Email` y el rol `Admin`

#### Scenario: Evaluador activo con contraseña correcta
- **GIVEN** un usuario con rol `Evaluador`, `IsActive = true` y una contraseña fijada con su enlace
- **WHEN** se envía `POST /api/auth/login` con su email y esa contraseña
- **THEN** el sistema MUST responder `200 OK` con un cuerpo `AuthResultDto` que contiene un JWT no vacío, el `Name`, el `Email` y el rol `Evaluador`

#### Scenario: Alumno activo con contraseña correcta
- **GIVEN** un usuario con rol `Alumno`, `IsActive = true` y un `PasswordHash` no vacío, establecido por alguna vía distinta del aprovisionamiento automático
- **WHEN** se envía `POST /api/auth/login` con su email y esa contraseña
- **THEN** el sistema MUST responder `200 OK` con un cuerpo `AuthResultDto` que contiene un JWT no vacío, el `Name`, el `Email` y el rol `Alumno`

#### Scenario: Alumno aprovisionado por invitación no entra por esta vía
- **GIVEN** una cuenta de alumno creada al abrir una invitación, que nace con `PasswordHash` vacío
- **WHEN** se envía `POST /api/auth/login` con su email y cualquier contraseña, incluida la parte local del email
- **THEN** el sistema MUST responder `401 Unauthorized`, porque el acceso del candidato es el enlace de la invitación y no una contraseña

#### Scenario: Evaluador con el acceso pendiente
- **GIVEN** un evaluador recién creado que todavía no ha fijado su contraseña
- **WHEN** se envía `POST /api/auth/login` con su email y cualquier contraseña
- **THEN** el sistema MUST responder `401 Unauthorized`

### Requirement: Autorización por rol Admin en endpoints de gestión
El sistema SHALL exigir un JWT válido con rol `Admin` para acceder a los endpoints de gestión de categorías, preguntas, exámenes, resultados, corrección de preguntas abiertas y usuarios, y SHALL responder `401 Unauthorized` (sin token válido) o `403 Forbidden` (token válido de un rol distinto de `Admin`) en caso contrario. La exigencia SHALL definirse en un solo punto de la API, de forma que un cambio en la matriz de permisos no obligue a editar cada controlador.

#### Scenario: Petición sin token a un endpoint de gestión
- **GIVEN** los endpoints de gestión de categorías, preguntas, exámenes, resultados, corrección y usuarios
- **WHEN** se envía una petición a cualquiera de ellos sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Token válido sin claim de rol Admin
- **GIVEN** un JWT válido emitido para un usuario con rol `Alumno`
- **WHEN** se envía una petición a un endpoint de gestión con ese token en el header `Authorization`
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Token válido de un evaluador
- **GIVEN** un JWT válido emitido para un usuario con rol `Evaluador`
- **WHEN** se envía una petición a cualquier endpoint de gestión, incluidos los de corrección de preguntas abiertas
- **THEN** el sistema MUST responder `403 Forbidden`, porque el acceso del evaluador a la corrección se define en un cambio posterior

### Requirement: Bloqueo de cuentas inactivas
El sistema SHALL negar el login a cualquier usuario cuyo campo `IsActive` sea `false`, aunque el email y la contraseña sean correctos. El sistema SHALL rechazar además con `401 Unauthorized` cualquier petición autenticada con un token de un usuario inactivo, aunque el token no haya caducado.

#### Scenario: Intento de login con cuenta desactivada
- **GIVEN** un usuario administrador con `IsActive = false` y credenciales válidas
- **WHEN** se envía `POST /api/auth/login` con esas credenciales
- **THEN** el sistema MUST responder `401 Unauthorized`, ya que la consulta de login filtra explícitamente por `u.IsActive`

#### Scenario: Token vigente de una cuenta desactivada después
- **GIVEN** un JWT emitido cuando la cuenta estaba activa y todavía no caducado
- **WHEN** la cuenta pasa a `IsActive = false` y se envía una petición con ese JWT
- **THEN** el sistema MUST responder `401 Unauthorized`

### Requirement: Aprovisionamiento de administrador inicial
El sistema SHALL crear automáticamente un usuario administrador por defecto la primera vez que la base de datos no contiene ningún usuario, usando el email `admin@techeval.com` y una contraseña configurable mediante `AdminPassword` en `appsettings.json` (valor por defecto `Admin@123!`). Su `PasswordHash` SHALL generarse con el esquema vigente.

#### Scenario: Primera ejecución con base de datos vacía
- **GIVEN** una base de datos sin registros en la tabla `Users`
- **WHEN** se ejecuta `DbSeeder.SeedAsync` durante el arranque de la aplicación
- **THEN** el sistema MUST insertar un usuario con `Email = "admin@techeval.com"`, rol `Admin`, `IsActive = true` y un `PasswordHash` derivado de `AdminPassword` con PBKDF2 y sal propia

### Requirement: Aprovisionamiento automático de cuenta de alumno
El sistema SHALL crear automáticamente un `User` con rol `Alumno` la primera vez que se abre una invitación de examen (`GET /api/exam/validate/{token}`) para un email que no corresponde a ningún usuario existente, derivando el `username` de la parte local del email. La cuenta SHALL nacer **sin contraseña utilizable**: el sistema MUST NOT derivar ninguna contraseña del email, del nombre ni de ningún otro dato conocido del candidato.

El acceso del candidato es el enlace de la invitación, que ya emite su sesión autenticada.

Si el email corresponde a un usuario existente con un rol distinto de `Alumno`, o a un alumno desactivado, el sistema SHALL responder que el enlace no es válido y SHALL NOT emitir ningún token.

#### Scenario: Primera apertura de una invitación con email nuevo
- **GIVEN** un `ExamToken` válido con `CandidateEmail = "alejandro.robles@pronet-ise.com"` y ningún `User` existente con ese email
- **WHEN** se invoca `GET /api/exam/validate/{token}` con ese token
- **THEN** el sistema MUST crear un `User` con `Email = "alejandro.robles@pronet-ise.com"`, `Name` igual al `CandidateName` de la invitación, rol `Alumno`, `IsActive = true` y sin contraseña utilizable

#### Scenario: La cuenta recién creada no permite entrar con datos del candidato
- **GIVEN** una cuenta de alumno recién aprovisionada para `alejandro.robles@pronet-ise.com`
- **WHEN** alguien envía `POST /api/auth/login` con esa cuenta y la contraseña `"alejandro.robles"`, o cualquier otro dato derivado de su email o de su nombre
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Apertura de una invitación con email ya existente
- **GIVEN** un `ExamToken` válido cuyo `CandidateEmail` ya corresponde a un `User` activo con rol `Alumno`
- **WHEN** se invoca `GET /api/exam/validate/{token}` con ese token
- **THEN** el sistema MUST reutilizar el `User` existente sin modificar su `Name` ni su `PasswordHash`, y SHALL NOT crear un `User` duplicado

#### Scenario: Invitación a un email de otro rol
- **GIVEN** un `ExamToken` cuyo `CandidateEmail` corresponde a un usuario con rol `Admin` o `Evaluador`
- **WHEN** se invoca `GET /api/exam/validate/{token}` con ese token
- **THEN** el sistema MUST responder que el enlace no es válido, sin emitir ningún JWT y sin modificar el usuario

### Requirement: Autorización por rol Alumno en endpoints del portal
El sistema SHALL exigir un JWT válido con rol `Alumno` para acceder a los endpoints del portal del alumno (pruebas pendientes y realizadas) **y a los endpoints de resolución de la prueba que operan sobre una sesión ya iniciada** (guardado de respuestas y envío), y SHALL responder `401 Unauthorized` (sin token válido) o `403 Forbidden` (token válido de un rol distinto de `Alumno`) en caso contrario.

Quedan fuera de esta exigencia los endpoints que operan con el token del enlace de invitación —la validación del token y el inicio de la sesión—, donde la credencial es ese token y no una sesión autenticada.

#### Scenario: Petición sin token a un endpoint del portal
- **WHEN** se envía una petición a un endpoint del portal del alumno sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Token de administrador usado contra un endpoint del portal
- **GIVEN** un JWT válido emitido para un usuario con rol `Admin`
- **WHEN** se envía una petición a un endpoint del portal del alumno con ese token
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Token de evaluador usado contra un endpoint del portal
- **GIVEN** un JWT válido emitido para un usuario con rol `Evaluador`
- **WHEN** se envía una petición a un endpoint del portal del alumno o de resolución de la prueba con ese token
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Petición sin token a un endpoint de resolución de la prueba
- **WHEN** se envía una petición de guardado de respuesta o de envío de la prueba sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Los endpoints del enlace de invitación siguen siendo públicos
- **WHEN** se envía una petición de validación de token o de inicio de sesión sin header `Authorization`
- **THEN** el sistema MUST procesarla, porque la credencial de esas operaciones es el token del enlace
