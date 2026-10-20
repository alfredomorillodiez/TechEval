## MODIFIED Requirements

### Requirement: Auto-guardado de respuestas individuales
El sistema SHALL permitir guardar la respuesta de una pregunta concreta dentro de una sesión de examen en curso, de forma incremental, sin requerir el envío completo del examen. La fecha de respuesta SHALL registrarse siempre en tiempo universal coordinado (UTC), igual que el resto de marcas de tiempo del modelo. Una sesión que ya se ha enviado SHALL NOT admitir más respuestas.

#### Scenario: Primera respuesta a una pregunta
- **GIVEN** una `ExamSession` en progreso sin respuesta previa registrada para una pregunta determinada
- **WHEN** el candidato envía `POST /api/exam/answer/{sessionId}` con el identificador de la pregunta y la respuesta seleccionada u abierta
- **THEN** el sistema crea una nueva `UserAnswer` asociada a esa sesión y pregunta con la respuesta proporcionada

#### Scenario: Corrección de una respuesta ya guardada
- **GIVEN** una `ExamSession` en progreso con una `UserAnswer` ya registrada para una pregunta determinada
- **WHEN** el candidato envía `POST /api/exam/answer/{sessionId}` de nuevo para la misma pregunta con una respuesta distinta
- **THEN** el sistema SHALL actualizar la `UserAnswer` existente (respuesta seleccionada u abierta y fecha de respuesta) en lugar de crear un duplicado

#### Scenario: La hora de la respuesta no queda por delante de la de la sesión
- **GIVEN** un servidor cuyo huso horario local está adelantado respecto a UTC
- **WHEN** un candidato responde a una pregunta dentro de una sesión iniciada momentos antes
- **THEN** el sistema SHALL registrar `AnsweredAt` en UTC, de forma que nunca resulte anterior a `StartedAt` ni posterior al cierre de la sesión por efecto del huso horario

#### Scenario: Respuesta a una sesión que ya se envió
- **GIVEN** una `ExamSession` con `ExamResult` asociado, o que ya no está en estado `InProgress`, dentro del plazo de la prueba
- **WHEN** su dueño envía `POST /api/exam/answer/{sessionId}`
- **THEN** el sistema MUST responder `409 Conflict` con un mensaje que indique que la prueba ya se envió
- **AND** el sistema SHALL NOT crear ni modificar ninguna `UserAnswer` de esa sesión

#### Scenario: La propiedad manda sobre el estado de la sesión
- **GIVEN** una `ExamSession` ya enviada que pertenece a otro candidato
- **WHEN** un candidato autenticado envía `POST /api/exam/answer/{sessionId}`
- **THEN** el sistema MUST responder `403 Forbidden`, no `409`

## ADDED Requirements

### Requirement: El fallo del correo no cambia la respuesta del envío
El sistema SHALL devolver el acuse del resultado con `200 OK` cuando el envío de la prueba se ha confirmado, aunque falle el correo al candidato. El fallo SHALL NOT propagarse a quien llama. El resultado persistido es el que manda: el correo es una notificación posterior.

#### Scenario: Correo caído al enviar una prueba de test
- **GIVEN** una sesión en progreso de una prueba sin preguntas abiertas
- **WHEN** el candidato envía la prueba y el servicio de correo lanza una excepción
- **THEN** el sistema responde `200 OK` con el acuse del resultado, con su puntuación y su veredicto
- **AND** el `ExamResult` y la sesión cerrada siguen persistidos

#### Scenario: Correo caído al enviar una prueba con preguntas abiertas
- **GIVEN** una sesión en progreso de una prueba con preguntas abiertas
- **WHEN** el candidato envía la prueba y el servicio de correo lanza una excepción
- **THEN** el sistema responde `200 OK` con el acuse pendiente, sin cifras
- **AND** el `ExamResult` queda en estado `PendingReview`

#### Scenario: La cancelación de la petición no se silencia
- **GIVEN** un envío cuya petición HTTP el cliente cancela mientras se envía el correo
- **WHEN** el servicio de correo lanza `OperationCanceledException`
- **THEN** el sistema SHALL NOT tratarla como un fallo de correo capturado

### Requirement: Una sola respuesta por pregunta y sesión
El sistema SHALL guardar como máximo una `UserAnswer` por cada pareja `(ExamSessionId, QuestionId)`. La base de datos SHALL imponerlo con un índice único, y el autoguardado SHALL tolerar que otra petición inserte la misma respuesta a la vez. Una sesión con dos filas para la misma pregunta puntuaría dos veces esa pregunta, porque la corrección suma las `AwardedPoints` de todas las filas.

#### Scenario: Dos autoguardados simultáneos de la misma pregunta
- **GIVEN** una sesión en progreso sin respuesta para una pregunta
- **WHEN** dos peticiones de autoguardado de esa pregunta coinciden y la segunda inserción falla porque la primera ya insertó
- **THEN** el sistema SHALL releer la fila existente y actualizarla con el contenido de la segunda petición
- **AND** la sesión queda con una sola `UserAnswer` para esa pregunta
- **AND** la petición no responde `500`

#### Scenario: El fallo de inserción no se debe a una carrera
- **GIVEN** un autoguardado cuya inserción falla y que, al releer, no encuentra ninguna fila para esa pregunta
- **WHEN** el sistema trata el fallo
- **THEN** el sistema SHALL relanzar el fallo original, sin ocultarlo

#### Scenario: La base rechaza una fila duplicada
- **GIVEN** una sesión con una `UserAnswer` para una pregunta
- **WHEN** se intenta insertar una segunda fila para la misma pareja `(ExamSessionId, QuestionId)`
- **THEN** la base de datos SHALL rechazar la inserción por el índice único
