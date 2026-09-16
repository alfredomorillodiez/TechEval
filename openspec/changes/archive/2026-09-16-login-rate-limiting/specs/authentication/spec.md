## MODIFIED Requirements

### Requirement: Login de administrador con credenciales válidas
El sistema SHALL exponer `POST /api/auth/login` y, cuando el email corresponde a un usuario **activo** (administrador o alumno) y la contraseña coincide con el hash almacenado, SHALL responder `200 OK` con un JWT firmado, el nombre, el email y el rol del usuario. Una cuenta sin contraseña utilizable SHALL NOT poder iniciar sesión por esta vía.

#### Scenario: Administrador activo con contraseña correcta
- **GIVEN** un usuario con `Email = "admin@techeval.com"`, `IsAdmin = true`, `IsActive = true` y un `PasswordHash` correspondiente a la contraseña configurada en `AdminPassword`
- **WHEN** se envía `POST /api/auth/login` con ese email y esa contraseña
- **THEN** el sistema MUST responder `200 OK` con un cuerpo `AuthResultDto` que contiene un JWT no vacío, el `Name`, el `Email` y el rol `Admin`

#### Scenario: Alumno activo con contraseña correcta
- **GIVEN** un usuario con `IsAdmin = false`, `IsActive = true` y un `PasswordHash` no vacío, establecido por alguna vía distinta del aprovisionamiento automático
- **WHEN** se envía `POST /api/auth/login` con su email y esa contraseña
- **THEN** el sistema MUST responder `200 OK` con un cuerpo `AuthResultDto` que contiene un JWT no vacío, el `Name`, el `Email` y el rol `Alumno`

#### Scenario: Alumno aprovisionado por invitación no entra por esta vía
- **GIVEN** una cuenta de alumno creada al abrir una invitación, que nace con `PasswordHash` vacío
- **WHEN** se envía `POST /api/auth/login` con su email y cualquier contraseña, incluida la parte local del email
- **THEN** el sistema MUST responder `401 Unauthorized`, porque el acceso del candidato es el enlace de la invitación y no una contraseña

## ADDED Requirements

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
