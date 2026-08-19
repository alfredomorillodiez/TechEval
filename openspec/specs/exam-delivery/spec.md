# exam-delivery Specification

## Purpose
TBD - created by archiving change exam-delivery. Update Purpose after archive.
## Requirements
### Requirement: Generación de token de acceso criptográficamente seguro
El sistema SHALL generar, para cada envío de examen a un candidato, un token de acceso derivado de 48 bytes obtenidos con un generador de números aleatorios criptográficamente seguro, codificado como cadena Base64 URL-safe (sin `+`, `/` ni `=`) de 64 caracteres.

#### Scenario: Envío de examen genera un token seguro
- **GIVEN** un administrador autenticado envía un examen a un candidato mediante `POST /api/exams/send`
- **WHEN** el sistema procesa la solicitud de envío
- **THEN** SHALL generar un nuevo token mediante un generador criptográficamente seguro (`RandomNumberGenerator`)
- **AND** el token SHALL codificarse en Base64 URL-safe, sustituyendo `+` por `-`, `/` por `_` y eliminando el relleno `=`

#### Scenario: El token no es predecible ni reutilizable entre envíos
- **GIVEN** dos envíos de examen distintos (mismo candidato u otro)
- **WHEN** el sistema genera el token de cada envío
- **THEN** cada token generado SHALL ser estadísticamente independiente y no derivable a partir de tokens previos

### Requirement: Unicidad del token garantizada por la base de datos
El sistema SHALL garantizar que no existan dos tokens de examen idénticos activos en el sistema, mediante una restricción de unicidad a nivel de base de datos sobre la columna `Token` de `ExamTokens`.

#### Scenario: Persistencia de un token recién generado
- **GIVEN** un token recién generado para un envío de examen
- **WHEN** el sistema intenta persistirlo en la tabla `ExamTokens`
- **THEN** la operación SHALL fallar si ya existe un registro con el mismo valor de `Token`, protegido por un índice único
- **AND** en la práctica esta colisión SHALL ser inviable dado el espacio de valores de 48 bytes aleatorios

### Requirement: Expiración configurable por horas
El sistema SHALL calcular y almacenar una fecha de expiración (`ExpiresAt`) para cada token, a partir del número de horas indicado en la solicitud de envío (`ExpirationHours`), con un valor por defecto de 72 horas si no se especifica.

#### Scenario: Envío con expiración por defecto
- **GIVEN** una solicitud de envío de examen que no especifica `ExpirationHours`
- **WHEN** el sistema genera el token
- **THEN** SHALL calcular `ExpiresAt` como la fecha/hora UTC actual más 72 horas

#### Scenario: Envío con expiración personalizada
- **GIVEN** una solicitud de envío de examen con `ExpirationHours` igual a un valor distinto de 72
- **WHEN** el sistema genera el token
- **THEN** SHALL calcular `ExpiresAt` sumando ese número de horas a la fecha/hora UTC actual

#### Scenario: Token expirado deja de ser válido
- **GIVEN** un token cuya fecha `ExpiresAt` ya ha pasado respecto a la fecha/hora UTC actual
- **WHEN** se evalúa la validez del token
- **THEN** el token SHALL considerarse inválido por expiración, independientemente de si fue usado o no

### Requirement: Vínculo único entre token, examen y candidato
Cada token de examen SHALL corresponder exactamente a un examen (`ExamId`) y a un candidato identificado por nombre (`CandidateName`) y correo (`CandidateEmail`), fijados en el momento de la generación y no modificables posteriormente. El token SHALL asociarse además a un `User` (`UserId`), resuelto de forma perezosa —no en el momento de la generación, sino la primera vez que el enlace se abre— buscando o creando el `User` correspondiente a `CandidateEmail`.

#### Scenario: El token queda asociado a un examen y candidato concretos
- **GIVEN** una solicitud de envío con un `ExamId`, `CandidateName` y `CandidateEmail` determinados
- **WHEN** el sistema crea el `ExamToken`
- **THEN** el registro persistido SHALL almacenar ese `ExamId`, `CandidateName` y `CandidateEmail` de forma inmutable para ese token, con `UserId` sin resolver todavía

#### Scenario: Envío a examen inexistente es rechazado
- **GIVEN** una solicitud de envío que referencia un `ExamId` que no existe
- **WHEN** el sistema procesa la solicitud
- **THEN** SHALL rechazar la operación sin generar token ni enviar email

#### Scenario: El UserId se resuelve al abrir el enlace, no al generarlo
- **GIVEN** un `ExamToken` recién generado con `UserId` sin resolver
- **WHEN** el candidato abre el enlace y el sistema procesa `GET /api/exam/validate/{token}`
- **THEN** el sistema MUST asociar el `ExamToken` al `User` correspondiente a `CandidateEmail` (buscándolo o creándolo), dejando `UserId` fijado a partir de ese momento

### Requirement: Reinvitación del mismo alumno a la misma prueba
El sistema SHALL permitir generar más de un `ExamToken` para el mismo `ExamId` y el mismo candidato (mismo `CandidateEmail`), sin restricción de unicidad sobre la combinación examen-candidato, cada uno con su propio ciclo de vida (expiración, uso) independiente.

#### Scenario: Segunda invitación al mismo alumno para el mismo examen
- **GIVEN** un `ExamToken` ya existente y usado para `ExamId = 5` y `CandidateEmail = "alejandro.robles@pronet-ise.com"`
- **WHEN** un administrador envía una nueva invitación con el mismo `ExamId` y el mismo `CandidateEmail`
- **THEN** el sistema SHALL crear un segundo `ExamToken` válido e independiente del primero, sin rechazar la operación por duplicidad

### Requirement: Notificación por email HTML con enlace único
El sistema SHALL notificar al candidato mediante un correo electrónico en formato HTML que incluya el título del examen, un enlace único de acceso construido a partir del token generado y la fecha/hora de expiración del enlace.

#### Scenario: Envío individual notifica al candidato
- **GIVEN** un token generado correctamente para un candidato
- **WHEN** el sistema completa el registro del token
- **THEN** SHALL enviar un correo HTML a `CandidateEmail` con asunto que referencia el título del examen
- **AND** el cuerpo del correo SHALL incluir un enlace con la forma `{baseUrl}/exam/{token}` y la fecha de expiración

#### Scenario: El envío de email desacopla el proveedor SMTP concreto
- **GIVEN** cualquier implementación de `IEmailService` registrada en el contenedor de dependencias
- **WHEN** el servicio de envío de examen invoca `SendExamInvitationAsync`
- **THEN** el sistema SHALL depender únicamente de la interfaz `IEmailService`, sin acoplarse a un proveedor SMTP concreto

### Requirement: Envío masivo con aislamiento de errores por candidato
El sistema SHALL permitir enviar el mismo examen a una lista de candidatos en una sola operación, generando un token y un email independientes por candidato, de forma que el fallo en un candidato no impida el envío a los demás.

#### Scenario: Envío masivo con éxito parcial
- **GIVEN** una lista de candidatos donde el envío de correo falla para uno de ellos (por ejemplo, dirección inválida)
- **WHEN** el sistema procesa el envío masivo
- **THEN** SHALL continuar generando y enviando tokens al resto de candidatos de la lista
- **AND** SHALL reportar por separado el resultado de éxito o fallo de cada candidato, incluyendo el mensaje de error cuando corresponda

