## ADDED Requirements

### Requirement: Propiedad de la sesión de examen
El sistema SHALL exigir el JWT de alumno que emite la validación de la invitación para guardar respuestas (`POST /api/exam/answer/{sessionId}`) y para enviar la prueba (`POST /api/exam/submit`), y MUST comprobar que la sesión referida pertenece al usuario de ese token antes de escribir nada.

Los identificadores de sesión son secuenciales y por tanto adivinables, así que el identificador por sí solo NEVER SHALL bastar como prueba de propiedad.

Una sesión cuyo `ExamToken` no tiene `UserId` SHALL tratarse como ajena: un dueño desconocido no es el que llama.

La validación del token (`GET /api/exam/validate/{token}`) y el inicio de la sesión (`POST /api/exam/start/{token}`) SHALL seguir siendo públicos, porque ahí la credencial es el token del enlace que el candidato recibe por correo.

#### Scenario: El candidato opera sobre su propia sesión
- **GIVEN** un candidato autenticado con el JWT que le devolvió la validación de su invitación
- **WHEN** guarda una respuesta o envía la prueba de su propia sesión
- **THEN** el sistema procesa la operación con normalidad

#### Scenario: Petición sin token a un endpoint de resolución
- **WHEN** se envía `POST /api/exam/answer/{sessionId}` o `POST /api/exam/submit` sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized` sin escribir nada

#### Scenario: Intento de escribir en la sesión de otro candidato
- **GIVEN** dos candidatos con sesiones distintas, y el JWT válido del primero
- **WHEN** el primero envía `POST /api/exam/answer/{sessionId}` con el identificador de sesión del segundo
- **THEN** el sistema MUST responder `403 Forbidden`
- **AND** el sistema SHALL NOT crear ni modificar ninguna `UserAnswer` de esa sesión

#### Scenario: Intento de cerrar el examen de otro candidato
- **GIVEN** el JWT válido de un candidato
- **WHEN** envía `POST /api/exam/submit` con el identificador de sesión de otro
- **THEN** el sistema MUST responder `403 Forbidden`
- **AND** el sistema SHALL NOT crear ningún `ExamResult` ni cerrar esa sesión

#### Scenario: Sesión sin dueño registrado
- **GIVEN** una `ExamSession` cuyo `ExamToken` tiene `UserId` nulo
- **WHEN** cualquier candidato autenticado intenta operar sobre ella
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Token de administrador contra un endpoint de resolución
- **GIVEN** un JWT válido de un usuario con `IsAdmin = true`
- **WHEN** se envía una petición a `answer` o `submit` con ese token
- **THEN** el sistema MUST responder `403 Forbidden`, porque el rol exigido es `Alumno`

### Requirement: Vigencia del plazo del examen en el servidor
El sistema MUST comprobar en el servidor que el plazo del examen sigue vigente, calculado desde `ExamSession.StartedAt` más el tiempo límite del examen, con un margen de gracia de 60 segundos que absorbe la latencia de la red y el auto-envío del temporizador. El reloj del cliente NEVER SHALL ser la autoridad sobre el tiempo transcurrido.

Fuera de plazo, guardar una respuesta SHALL rechazarse y enviar la prueba SHALL aceptarse ignorando lo que traiga: el examen se cierra y se puntúa con las respuestas que ya estaban guardadas.

#### Scenario: Guardado de respuesta dentro de plazo
- **GIVEN** una sesión cuyo tiempo límite no se ha agotado
- **WHEN** el candidato guarda una respuesta
- **THEN** el sistema la persiste con normalidad

#### Scenario: Guardado de respuesta fuera de plazo
- **GIVEN** una sesión cuyo tiempo límite se agotó hace más del margen de gracia
- **WHEN** el candidato guarda una respuesta
- **THEN** el sistema MUST responder `409 Conflict` y SHALL NOT escribir ni crear ninguna `UserAnswer`

#### Scenario: Guardado dentro del margen de gracia
- **GIVEN** una sesión cuyo tiempo límite se agotó hace menos de 60 segundos
- **WHEN** el candidato guarda una respuesta
- **THEN** el sistema la persiste, porque el margen absorbe la latencia de una petición legítima ya en vuelo

#### Scenario: Envío fuera de plazo se cierra con lo ya guardado
- **GIVEN** una sesión cuyo tiempo límite se agotó hace más del margen de gracia, con tres respuestas guardadas antes de agotarse
- **WHEN** el candidato envía la prueba, incluyendo en el cuerpo respuestas distintas o adicionales
- **THEN** el sistema SHALL cerrar el examen y puntuarlo con las tres respuestas ya guardadas
- **AND** el sistema SHALL NOT escribir las respuestas que trae el envío
- **AND** el candidato que vuelve tarde no pierde su trabajo, y el que trabaja de más no gana nada por ello

#### Scenario: Envío dentro de plazo
- **GIVEN** una sesión cuyo tiempo límite sigue vigente
- **WHEN** el candidato envía la prueba
- **THEN** el sistema escribe las respuestas del envío y puntúa con ellas, como hasta ahora
