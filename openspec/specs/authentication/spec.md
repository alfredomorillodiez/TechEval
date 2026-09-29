# authentication Specification

## Purpose
TBD - created by archiving change authentication. Update Purpose after archive.

## Requirements

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
- **WHEN** se envía una petición a cualquier endpoint de gestión, incluidos los de corrección de preguntas abiertas del administrador
- **THEN** el sistema MUST responder `403 Forbidden`, porque esos endpoints muestran la identidad del candidato; el evaluador corrige por los endpoints de evaluación

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

### Requirement: Auto-login al abrir la invitación
El sistema SHALL emitir un JWT válido para el `User` del alumno (creado o reutilizado) como parte de la respuesta de `GET /api/exam/validate/{token}`, de forma que el candidato quede autenticado sin necesidad de introducir credenciales.

#### Scenario: Validación exitosa devuelve sesión autenticada
- **WHEN** `GET /api/exam/validate/{token}` procesa un token válido
- **THEN** la respuesta MUST incluir un JWT firmado para el `User` asociado, con el rol `Alumno`, además de la información de la prueba ya especificada

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

### Requirement: Límite de ritmo en el inicio de sesión
El sistema SHALL limitar el número de peticiones a `POST /api/auth/login` por dirección de origen dentro de una ventana de tiempo. Superado el límite, SHALL responder `429 Too Many Requests` con un cuerpo `ProblemDetails` y una cabecera `Retry-After`, sin comprobar la contraseña.

El motivo SHALL entenderse como protección del servidor y no solo de las cuentas: verificar una contraseña cuesta cientos de milisegundos de CPU desde que el hash es PBKDF2, así que un volumen moderado de intentos agota los hilos disponibles aunque ninguno acierte.

#### Scenario: Intentos dentro del límite
- **GIVEN** una dirección de origen que no ha superado el límite en la ventana actual
- **WHEN** envía `POST /api/auth/login`
- **THEN** el sistema SHALL procesar la petición con normalidad, acierte o no la contraseña

#### Scenario: Intentos por encima del límite
- **GIVEN** una dirección de origen que ya ha agotado su cupo en la ventana actual
- **WHEN** envía una petición más a `POST /api/auth/login`
- **THEN** el sistema SHALL responder `429 Too Many Requests` sin verificar la contraseña, de forma que la petición no consuma CPU de derivación de clave

#### Scenario: El rechazo dice cuándo reintentar
- **WHEN** el sistema responde `429` a una petición de inicio de sesión
- **THEN** la respuesta SHALL incluir una cabecera `Retry-After` con los segundos que faltan para que el cupo se renueve

#### Scenario: El límite no distingue aciertos de fallos
- **GIVEN** una dirección de origen que ha agotado su cupo con contraseñas correctas
- **WHEN** envía una petición más
- **THEN** el sistema SHALL responder `429` igualmente, porque el coste en CPU es el mismo acierte o falle

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

### Requirement: Autorización por rol Evaluador en endpoints de evaluación
El sistema SHALL exigir un JWT válido con rol `Evaluador` para acceder a los endpoints de evaluación (cola, detalle, reserva, envío, señales e historial del evaluador), y SHALL responder `401 Unauthorized` sin token válido o `403 Forbidden` con un token de otro rol. El administrador SHALL NOT usar estos endpoints: corrige por los suyos, que le muestran la identidad del candidato. La exigencia SHALL definirse en el mismo punto de la API que las demás políticas.

#### Scenario: Token de administrador contra un endpoint de evaluación
- **GIVEN** un JWT válido de un usuario con rol `Admin`
- **WHEN** se envía una petición a un endpoint de evaluación
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Token de alumno contra un endpoint de evaluación
- **GIVEN** un JWT válido de un usuario con rol `Alumno`
- **WHEN** se envía una petición a un endpoint de evaluación
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Petición sin token a un endpoint de evaluación
- **WHEN** se envía una petición a un endpoint de evaluación sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized`
