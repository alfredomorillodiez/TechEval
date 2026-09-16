## MODIFIED Requirements

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

### Requirement: Aprovisionamiento de administrador inicial
El sistema SHALL crear automáticamente un usuario administrador por defecto la primera vez que la base de datos no contiene ningún usuario, usando el email `admin@techeval.com` y una contraseña configurable mediante `AdminPassword` en `appsettings.json` (valor por defecto `Admin@123!`). Su `PasswordHash` SHALL generarse con el esquema vigente.

#### Scenario: Primera ejecución con base de datos vacía
- **GIVEN** una base de datos sin registros en la tabla `Users`
- **WHEN** se ejecuta `DbSeeder.SeedAsync` durante el arranque de la aplicación
- **THEN** el sistema MUST insertar un usuario con `Email = "admin@techeval.com"`, `IsAdmin = true`, `IsActive = true` y un `PasswordHash` derivado de `AdminPassword` con PBKDF2 y sal propia
