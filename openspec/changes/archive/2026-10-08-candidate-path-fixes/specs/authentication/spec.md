## MODIFIED Requirements

### Requirement: Aprovisionamiento automático de cuenta de alumno
El sistema SHALL crear automáticamente un `User` con rol `Alumno` la primera vez que se abre una invitación de examen (`GET /api/exam/validate/{token}`) para un email que no corresponde a ningún usuario existente. La cuenta SHALL nacer **sin nombre de usuario**: el alumno no entra con usuario y contraseña, y un nombre derivado de la parte local del email hacía chocar a dos candidatos de dominios distintos. La cuenta SHALL nacer también **sin contraseña utilizable**: el sistema MUST NOT derivar ninguna contraseña del email, del nombre ni de ningún otro dato conocido del candidato.

El acceso del candidato es el enlace de la invitación, que ya emite su sesión autenticada.

Si el email corresponde a un usuario existente con un rol distinto de `Alumno`, o a un alumno desactivado, el sistema SHALL responder que el enlace no es válido y SHALL NOT emitir ningún token.

#### Scenario: Primera apertura de una invitación con email nuevo
- **GIVEN** un `ExamToken` válido con `CandidateEmail = "alejandro.robles@pronet-ise.com"` y ningún `User` existente con ese email
- **WHEN** se invoca `GET /api/exam/validate/{token}` con ese token
- **THEN** el sistema MUST crear un `User` con `Email = "alejandro.robles@pronet-ise.com"`, `Name` igual al `CandidateName` de la invitación, rol `Alumno`, `IsActive = true`, sin nombre de usuario y sin contraseña utilizable

#### Scenario: Dos candidatos con la misma parte local del email
- **GIVEN** un alumno existente con email `maria.lopez@gmail.com`
- **AND** un `ExamToken` válido con `CandidateEmail = "maria.lopez@hotmail.com"`
- **WHEN** se invoca `GET /api/exam/validate/{token}` con ese token
- **THEN** el sistema MUST crear un `User` nuevo para `maria.lopez@hotmail.com`, sin nombre de usuario, y responder con `isValid = true` y un JWT
- **AND** el alumno `maria.lopez@gmail.com` no cambia

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
