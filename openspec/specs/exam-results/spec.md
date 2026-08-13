# exam-results Specification

## Purpose
TBD - created by archiving change exam-results. Update Purpose after archive.
## Requirements
### Requirement: Corrección automática de preguntas de opción múltiple
Al enviar un examen (`SubmitExamAsync`), el sistema SHALL evaluar automáticamente cada pregunta de tipo `MultipleChoice` comparando el `SelectedAnswerId` enviado por el candidato contra la respuesta marcada como `IsCorrect` en el banco de preguntas, marcando la respuesta del candidato (`UserAnswer.IsCorrect`) como verdadera o falsa y sumando los puntos de la pregunta (`Question.Points`) al total obtenido cuando coincide con la respuesta correcta.

#### Scenario: Respuesta de opción múltiple correcta
- **WHEN** el candidato selecciona en una pregunta de tipo `MultipleChoice` la respuesta marcada como `IsCorrect = true` en el banco de preguntas
- **THEN** el sistema marca `UserAnswer.IsCorrect = true` y suma los puntos de esa pregunta a `ObtainedPoints` del resultado

#### Scenario: Respuesta de opción múltiple incorrecta o sin responder
- **WHEN** el candidato selecciona una respuesta distinta de la correcta, o no selecciona ninguna respuesta para una pregunta de tipo `MultipleChoice`
- **THEN** el sistema marca `UserAnswer.IsCorrect = false` y no suma puntos de esa pregunta a `ObtainedPoints`

### Requirement: Preguntas abiertas pendientes de corrección manual
El sistema SHALL dejar las respuestas de preguntas de tipo `OpenQuestion` sin evaluación automática, guardando `UserAnswer.IsCorrect = null` y el texto libre en `OpenAnswer`, de forma que queden identificables como pendientes de revisión humana y no se contabilicen automáticamente en `ObtainedPoints`.

#### Scenario: Envío de una pregunta abierta
- **WHEN** el candidato responde una pregunta de tipo `OpenQuestion` con texto libre
- **THEN** el sistema guarda la respuesta en `UserAnswer.OpenAnswer` y establece `UserAnswer.IsCorrect = null`, sin sumar ni descontar puntos automáticamente por esa pregunta

### Requirement: Cálculo de puntuación y determinación de aprobado
Al completar la corrección de todas las preguntas, el sistema SHALL calcular `ScorePercentage` como el porcentaje de `ObtainedPoints` sobre `TotalPoints` (redondeado a 2 decimales, `0` si `TotalPoints` es `0`), y SHALL determinar `Passed` comparando `ScorePercentage` contra el umbral `PassingScorePercentage` configurado en el examen (`Passed = true` cuando `ScorePercentage >= PassingScorePercentage`).

#### Scenario: Puntuación igual o superior al umbral de aprobación
- **WHEN** el `ScorePercentage` calculado es mayor o igual que el `PassingScorePercentage` del examen
- **THEN** el resultado se guarda con `Passed = true`

#### Scenario: Puntuación inferior al umbral de aprobación
- **WHEN** el `ScorePercentage` calculado es menor que el `PassingScorePercentage` del examen
- **THEN** el resultado se guarda con `Passed = false`

### Requirement: Persistencia del resultado y notificación al candidato
El sistema SHALL crear y persistir una entidad `ExamResult` (vinculada a `ExamSessionId` y `ExamId`, con `CandidateName`, `CandidateEmail`, `TotalPoints`, `ObtainedPoints`, `ScorePercentage`, `Passed` y `CompletedAt`) al finalizar la corrección, y SHALL enviar un correo con el resultado al candidato inmediatamente después de guardarlo.

#### Scenario: Creación del ExamResult tras enviar el examen
- **WHEN** `SubmitExamAsync` termina de procesar todas las respuestas de la sesión
- **THEN** el sistema persiste un nuevo `ExamResult` con los puntos totales, puntos obtenidos, porcentaje y estado de aprobación calculados, y marca la `ExamSession` asociada como `Completed`

#### Scenario: Notificación de resultado por email
- **WHEN** el `ExamResult` ha sido persistido correctamente
- **THEN** el sistema envía al correo del candidato (`CandidateEmail`) un mensaje con el nombre del examen, el porcentaje obtenido y si aprobó o no

### Requirement: Dashboard de estadísticas para administradores
El sistema SHALL exponer a usuarios con rol `Admin` un endpoint de dashboard (`GET /api/results/dashboard`) que devuelva el número de exámenes activos, el número de preguntas activas, la cantidad de resultados registrados en el mes en curso, el promedio de `ScorePercentage` del mes en curso, la tasa de aprobación del mes en curso, y los últimos 10 resultados registrados (ordenados por fecha de finalización descendente).

#### Scenario: Consulta del dashboard con resultados en el mes actual
- **WHEN** un administrador solicita `GET /api/results/dashboard` y existen resultados completados dentro del mes calendario en curso
- **THEN** el sistema responde con el promedio de puntuación y la tasa de aprobación calculados únicamente sobre los resultados de ese mes, junto con los conteos de exámenes/preguntas activos y los 10 resultados más recientes

#### Scenario: Consulta del dashboard sin resultados en el mes actual
- **WHEN** un administrador solicita `GET /api/results/dashboard` y no existe ningún resultado completado dentro del mes en curso
- **THEN** el sistema responde con promedio de puntuación y tasa de aprobación en `0` para el mes, sin fallar la consulta

### Requirement: Listado de resultados y filtrado por examen
El sistema SHALL permitir a usuarios con rol `Admin` consultar el listado completo de resultados (`GET /api/results`) y el listado de resultados de un examen específico (`GET /api/results/exam/{examId}`), ambos ordenados por fecha de finalización descendente, exponiendo para cada resultado el nombre e email del candidato, el título del examen, el porcentaje obtenido, si aprobó y la fecha de finalización.

#### Scenario: Listado global de resultados
- **WHEN** un administrador solicita `GET /api/results`
- **THEN** el sistema responde con todos los `ExamResult` registrados, ordenados del más reciente al más antiguo

#### Scenario: Listado de resultados de un examen concreto
- **WHEN** un administrador solicita `GET /api/results/exam/{examId}` para un examen existente
- **THEN** el sistema responde únicamente con los resultados cuyo `ExamId` coincide con el examen solicitado, ordenados del más reciente al más antiguo

### Requirement: Detalle de resultado con revisión de respuestas
El sistema SHALL permitir a usuarios con rol `Admin` consultar el detalle de un resultado (`GET /api/results/{id}`), incluyendo por cada pregunta de la sesión el texto de la pregunta, la respuesta seleccionada o el texto abierto del candidato, la respuesta correcta esperada, si fue evaluada como correcta (o `null` si es una pregunta abierta pendiente) y los puntos de la pregunta; si el resultado no existe, SHALL responder `404`.

#### Scenario: Consulta de detalle de un resultado existente
- **WHEN** un administrador solicita `GET /api/results/{id}` para un `id` de resultado existente
- **THEN** el sistema responde con los datos del resultado y la lista de revisión de respuestas, una entrada por cada pregunta única respondida en la sesión

#### Scenario: Consulta de detalle de un resultado inexistente
- **WHEN** un administrador solicita `GET /api/results/{id}` para un `id` que no corresponde a ningún resultado
- **THEN** el sistema responde `404 Not Found`

