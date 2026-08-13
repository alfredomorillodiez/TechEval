# exam-taking Specification

## Purpose
TBD - created by archiving change exam-taking. Update Purpose after archive.
## Requirements
### Requirement: Validación pública de token de examen
El sistema SHALL exponer un endpoint público (sin autenticación) que permita comprobar si un token de examen es válido antes de mostrar la interfaz de examen al candidato.

#### Scenario: Token válido
- **GIVEN** un `ExamToken` existente que no ha expirado y no ha sido usado
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = true`, el título del examen y el nombre del candidato asociado al token

#### Scenario: Token inexistente
- **GIVEN** un valor de token que no corresponde a ningún `ExamToken` almacenado
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = false` y el mensaje "Token no válido."

#### Scenario: Token expirado
- **GIVEN** un `ExamToken` cuya fecha de expiración (`ExpiresAt`) es anterior al momento actual
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = false` y el mensaje "El enlace ha expirado."

#### Scenario: Token ya utilizado
- **GIVEN** un `ExamToken` marcado como `IsUsed = true`
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = false` y el mensaje "Este examen ya ha sido completado."

### Requirement: Inicio de sesión de examen de un solo uso
El sistema SHALL permitir iniciar una sesión de examen a partir de un token válido, y MUST invalidar dicho token para cualquier uso futuro en el mismo momento en que se crea la sesión.

#### Scenario: Inicio exitoso de una nueva sesión
- **GIVEN** un `ExamToken` válido (no expirado, no usado) sin sesión de examen previa
- **WHEN** el candidato solicita `POST /api/exam/start/{token}`
- **THEN** el sistema crea una nueva `ExamSession` en estado `InProgress` asociada a ese token
- **AND** el sistema marca el `ExamToken` como usado (`IsUsed = true`) y registra `UsedAt` con la fecha/hora actual
- **AND** el sistema devuelve el detalle de la sesión: identificador de sesión, título del examen, nombre del candidato, tiempo límite en minutos, hora de inicio y el listado ordenado de preguntas con sus opciones de respuesta

#### Scenario: Reintento de inicio con una sesión ya en progreso
- **GIVEN** un `ExamToken` que ya tiene una `ExamSession` asociada (por ejemplo, el candidato recargó la página)
- **WHEN** el candidato solicita `POST /api/exam/start/{token}` de nuevo
- **THEN** el sistema SHALL devolver el detalle de la sesión existente en lugar de crear una nueva sesión

#### Scenario: Intento de iniciar sesión con token inválido, expirado o ya usado
- **GIVEN** un token que no existe, ha expirado, o ya fue usado por una sesión previamente completada
- **WHEN** el candidato solicita `POST /api/exam/start/{token}`
- **THEN** el sistema MUST rechazar la operación con un error 400 y el mensaje "Token inválido o expirado." sin crear ninguna `ExamSession`

### Requirement: Auto-guardado de respuestas individuales
El sistema SHALL permitir guardar la respuesta de una pregunta concreta dentro de una sesión de examen en curso, de forma incremental, sin requerir el envío completo del examen.

#### Scenario: Primera respuesta a una pregunta
- **GIVEN** una `ExamSession` en progreso sin respuesta previa registrada para una pregunta determinada
- **WHEN** el candidato envía `POST /api/exam/answer/{sessionId}` con el identificador de la pregunta y la respuesta seleccionada u abierta
- **THEN** el sistema crea una nueva `UserAnswer` asociada a esa sesión y pregunta con la respuesta proporcionada

#### Scenario: Corrección de una respuesta ya guardada
- **GIVEN** una `ExamSession` en progreso con una `UserAnswer` ya registrada para una pregunta determinada
- **WHEN** el candidato envía `POST /api/exam/answer/{sessionId}` de nuevo para la misma pregunta con una respuesta distinta
- **THEN** el sistema SHALL actualizar la `UserAnswer` existente (respuesta seleccionada u abierta y fecha de respuesta) en lugar de crear un duplicado

### Requirement: Envío final del examen
El sistema SHALL permitir al candidato enviar el conjunto completo de respuestas de su sesión para marcarla como finalizada, dejando la corrección automática y el cálculo de puntuación a cargo de otra capacidad.

#### Scenario: Envío exitoso del examen
- **GIVEN** una `ExamSession` en progreso
- **WHEN** el candidato envía `POST /api/exam/submit` con el identificador de sesión y el conjunto de respuestas
- **THEN** el sistema persiste cada respuesta enviada (creando o actualizando la `UserAnswer` correspondiente a cada pregunta)
- **AND** el sistema marca la sesión como `Completed` y registra `CompletedAt` con la fecha/hora actual

#### Scenario: Envío referenciando una sesión inexistente
- **GIVEN** un identificador de sesión que no corresponde a ninguna `ExamSession` almacenada
- **WHEN** se solicita `POST /api/exam/submit` con ese identificador
- **THEN** el sistema MUST rechazar la operación indicando que la sesión no fue encontrada, sin registrar ningún resultado

