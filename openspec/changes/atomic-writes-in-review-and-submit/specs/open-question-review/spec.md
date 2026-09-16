## MODIFIED Requirements

### Requirement: Corrección atómica de todas las respuestas abiertas
El sistema SHALL aceptar la corrección de un resultado pendiente en una única operación que incluya la puntuación de **todas** las respuestas a preguntas abiertas del resultado, tengan contenido o estén en blanco. Cada entrada SHALL indicar el identificador de la `UserAnswer` y los puntos otorgados (`AwardedPoints`), y MAY incluir un comentario del corrector (`ReviewerComment`). El sistema SHALL rechazar la operación completa si falta alguna respuesta abierta del resultado, si se incluye una respuesta que no pertenece al resultado, o si algún `AwardedPoints` es negativo o superior a `Question.Points`. No existe corrección parcial: o se corrige todo el resultado, o no se modifica nada.

Esa indivisibilidad SHALL sostenerse también cuando el fallo ocurra **durante** la escritura, y no solo cuando la corrección se rechace por inválida. Las puntuaciones de las respuestas y el cierre del resultado SHALL escribirse en una sola transacción de base de datos: si cualquiera de esas escrituras falla, ninguna SHALL quedar persistida. El envío del correo al candidato SHALL quedar fuera de esa transacción.

#### Scenario: Corrección completa y válida
- **GIVEN** un resultado con `Status = PendingReview` y tres respuestas abiertas
- **WHEN** un administrador envía la corrección con los puntos otorgados de las tres respuestas, todos dentro del rango `[0, Question.Points]`
- **THEN** el sistema persiste `AwardedPoints` y `ReviewerComment` en cada `UserAnswer`, recalcula el resultado y responde `200 OK` con el resultado ya corregido

#### Scenario: Corrección incompleta rechazada
- **GIVEN** un resultado con `Status = PendingReview` y tres respuestas abiertas
- **WHEN** un administrador envía una corrección que solo puntúa dos de las tres respuestas
- **THEN** el sistema responde `400 Bad Request`, no modifica ninguna `UserAnswer` y mantiene el resultado en `PendingReview`

#### Scenario: Fallo a mitad de la escritura de la corrección
- **GIVEN** un resultado pendiente con tres respuestas abiertas y una corrección válida en curso
- **WHEN** la escritura falla después de puntuar la primera respuesta y antes de cerrar el resultado
- **THEN** el sistema SHALL revertir todas las escrituras de esa corrección
- **AND** ninguna `UserAnswer` conserva la puntuación escrita
- **AND** el resultado sigue en `PendingReview`, disponible para corregirse de nuevo desde cero

#### Scenario: Confirmación de un resultado con todas las abiertas en blanco
- **GIVEN** un resultado pendiente en el que el candidato dejó sin responder las dos preguntas abiertas, precargadas a `AwardedPoints = 0`
- **WHEN** un administrador confirma la corrección aceptando ambos ceros
- **THEN** el sistema cierra el resultado como `Reviewed` y determina `Passed` con la puntuación resultante, igual que en cualquier otra corrección

#### Scenario: Puntuación fuera de rango rechazada
- **GIVEN** un resultado pendiente con una respuesta abierta de una pregunta de 3 puntos
- **WHEN** un administrador envía una corrección otorgando 5 puntos a esa respuesta
- **THEN** el sistema responde `400 Bad Request` y no persiste ningún cambio

#### Scenario: Respuesta ajena al resultado rechazada
- **GIVEN** un resultado pendiente de corrección
- **WHEN** la corrección incluye el identificador de una `UserAnswer` que pertenece a otra sesión
- **THEN** el sistema responde `400 Bad Request` y no persiste ningún cambio

#### Scenario: Corrección de un resultado ya corregido
- **WHEN** un administrador envía una corrección sobre un resultado cuyo `Status` ya es `Reviewed`
- **THEN** el sistema responde `409 Conflict` y no modifica el resultado, evitando que dos administradores corrijan el mismo resultado dos veces
