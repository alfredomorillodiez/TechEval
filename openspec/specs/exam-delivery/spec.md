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

### Requirement: Las invitaciones son solo para alumnos
El sistema SHALL rechazar el envío de una invitación a un email que corresponde a un usuario con rol `Admin` o `Evaluador`. El rechazo SHALL producirse antes de generar el token y antes de enviar el correo, y el mensaje SHALL indicar que ese email pertenece a una cuenta que no es de alumno. En un envío masivo, el rechazo SHALL afectar solo a ese candidato, igual que cualquier otro fallo individual.

El motivo es la regla de un rol por usuario: abrir la invitación emite un token de alumno, y ese token no puede emitirse para una cuenta de otro rol.

#### Scenario: Invitación individual a un evaluador
- **GIVEN** un usuario con rol `Evaluador` y email "laura.gil@pronet-ise.com"
- **WHEN** un administrador envía una prueba a "laura.gil@pronet-ise.com"
- **THEN** el sistema responde `400 Bad Request`, no genera ningún `ExamToken` y no envía ningún correo

#### Scenario: Envío masivo con un evaluador en la lista
- **GIVEN** una lista de tres candidatos en la que el segundo email pertenece a un administrador
- **WHEN** un administrador envía la prueba a la lista
- **THEN** el sistema genera y envía las invitaciones del primer y del tercer candidato
- **AND** el resultado del segundo candidato indica el fallo con su motivo

#### Scenario: Invitación a un email que todavía no es usuario
- **GIVEN** un email que no corresponde a ningún usuario
- **WHEN** un administrador envía una prueba a ese email
- **THEN** el sistema genera la invitación con normalidad, y el usuario de rol `Alumno` nace al abrir el enlace

### Requirement: Validación de la invitación
El sistema SHALL validar en el servidor los datos de una invitación y MUST responder `400 Bad Request` con un mensaje que diga qué falla, sin crear ningún token. El nombre SHALL no estar vacío. El correo SHALL tener formato válido. La expiración SHALL estar entre 1 y 720 horas. Un envío masivo SHALL llevar entre 1 y 500 candidatos, y SHALL validar cada uno antes de crear ningún token.

#### Scenario: Correo con formato inválido
- **WHEN** un administrador envía `POST /api/exams/send` con `CandidateEmail = "no-es-un-correo"`
- **THEN** el sistema responde `400` indicando que el correo no es válido y no crea ningún token

#### Scenario: Expiración fuera de rango
- **WHEN** un administrador envía una invitación con `ExpirationHours = 0`
- **THEN** el sistema responde `400` indicando que la expiración debe estar entre 1 y 720 horas

#### Scenario: Envío masivo vacío
- **WHEN** un administrador envía `POST /api/exams/send-bulk` sin candidatos
- **THEN** el sistema responde `400` indicando que hace falta al menos un candidato

### Requirement: Una prueba desactivada no admite invitaciones
El sistema SHALL rechazar con `409 Conflict` y un mensaje claro cualquier invitación, individual o masiva, a una prueba desactivada. El envío masivo SHALL rechazarse entero, antes de crear ningún token. Desactivar una prueba SHALL NOT invalidar los enlaces ya enviados.

#### Scenario: Invitación individual a una prueba desactivada
- **GIVEN** una prueba con `IsActive = false`
- **WHEN** un administrador envía `POST /api/exams/send` para esa prueba
- **THEN** el sistema responde `409` indicando que la prueba está desactivada
- **AND** no crea ningún token ni envía ningún correo

#### Scenario: Envío masivo a una prueba desactivada
- **GIVEN** una prueba con `IsActive = false`
- **WHEN** un administrador envía `POST /api/exams/send-bulk` con tres candidatos
- **THEN** el sistema responde `409` y no crea ningún token

#### Scenario: Un enlace ya enviado sigue valiendo
- **GIVEN** una invitación enviada antes de desactivar la prueba
- **WHEN** el candidato abre su enlace
- **THEN** el enlace se comporta como antes

### Requirement: Las fechas de los correos van en hora local
El correo de invitación y el de enlace para fijar la contraseña SHALL mostrar la fecha de expiración en la zona horaria que indique `Email:TimeZoneId` (por defecto `Europe/Madrid`), con el nombre de la zona y su desfase respecto a UTC. Si la zona configurada no existe en el equipo, SHALL mostrar la fecha en UTC con esa etiqueta, sin fallar. El valor almacenado SHALL seguir siendo UTC.

#### Scenario: Invitación en horario de verano
- **GIVEN** una expiración de `2026-10-09 10:30 UTC` y la zona `Europe/Madrid`
- **WHEN** se compone el correo de invitación
- **THEN** el correo dice «09/10/2026 12:30 (hora de Madrid, UTC+02:00)»

#### Scenario: Invitación en horario de invierno
- **GIVEN** una expiración de `2026-12-09 10:30 UTC` y la zona `Europe/Madrid`
- **WHEN** se compone el correo
- **THEN** el correo dice «09/12/2026 11:30 (hora de Madrid, UTC+01:00)»

#### Scenario: Zona desconocida
- **GIVEN** `Email:TimeZoneId` con un valor que el equipo no conoce
- **WHEN** se compone el correo
- **THEN** el correo muestra la fecha en UTC con la etiqueta «UTC»
- **AND** el envío no falla
