# open-question-review Specification

## Purpose
Corrección manual de las respuestas a preguntas abiertas: cola de resultados pendientes priorizada por antigüedad, otorgación de puntos parciales con comentario del corrector, recálculo atómico del resultado y trazabilidad de quién corrigió y cuándo.

## Requirements

### Requirement: Cola de resultados pendientes de corrección
El sistema SHALL exponer a usuarios con rol `Admin` un endpoint que devuelva los `ExamResult` cuyo `Status` es `PendingReview`, ordenados por `CompletedAt` ascendente (el más antiguo primero, para que la espera del candidato determine la prioridad), indicando para cada uno el nombre y email del candidato, el título del examen, la fecha de finalización, el número de respuestas abiertas por corregir y los días transcurridos desde el envío.

#### Scenario: Consulta de la cola con resultados pendientes
- **WHEN** un administrador solicita la cola de correcciones pendientes y existen resultados con `Status = PendingReview`
- **THEN** el sistema responde `200 OK` con esos resultados ordenados por `CompletedAt` ascendente, cada uno con su recuento de respuestas abiertas por corregir

#### Scenario: Consulta de la cola sin resultados pendientes
- **WHEN** un administrador solicita la cola de correcciones pendientes y no existe ningún resultado con `Status = PendingReview`
- **THEN** el sistema responde `200 OK` con una lista vacía, sin error

#### Scenario: Acceso no autorizado a la cola
- **WHEN** un usuario sin rol `Admin` solicita la cola de correcciones pendientes
- **THEN** el sistema responde `401 Unauthorized` o `403 Forbidden` y no revela ningún dato de candidatos

### Requirement: Detalle de corrección con respuesta de referencia
El sistema SHALL exponer a usuarios con rol `Admin` el detalle de corrección de un resultado pendiente, devolviendo por cada respuesta a pregunta abierta del resultado —tenga contenido o esté en blanco— el identificador de la `UserAnswer`, el enunciado de la pregunta, el texto libre entregado por el candidato, los puntos máximos de la pregunta, la puntuación pre-asignada cuando exista y la respuesta de referencia (`Question.SampleAnswer`) cuando exista. El enunciado y los puntos máximos SHALL leerse de la copia congelada en la `UserAnswer`, de forma que el corrector vea lo que se le preguntó al candidato. La respuesta de referencia SHALL leerse de la pregunta actual, porque es una guía para quien corrige y no algo que el candidato viera.

#### Scenario: Detalle de un resultado pendiente de corrección
- **WHEN** un administrador solicita el detalle de corrección de un resultado con `Status = PendingReview`
- **THEN** el sistema responde con una entrada por cada respuesta a pregunta abierta del resultado, incluyendo enunciado, respuesta del candidato, puntos máximos y `SampleAnswer` de referencia

#### Scenario: Respuesta en blanco precargada a cero
- **GIVEN** un resultado pendiente en el que el candidato dejó una pregunta abierta sin responder
- **WHEN** un administrador solicita el detalle de corrección
- **THEN** el sistema incluye esa respuesta con su `AwardedPoints` pre-asignado a `0`, de forma que el corrector pueda confirmarla sin volver a teclearla y conserve la posibilidad de modificarla

#### Scenario: Detalle de un resultado ya corregido
- **WHEN** un administrador solicita el detalle de corrección de un resultado cuyo `Status` es `Reviewed`
- **THEN** el sistema responde `409 Conflict` indicando que el resultado ya fue corregido, sin permitir una segunda corrección

#### Scenario: Detalle de un resultado inexistente
- **WHEN** un administrador solicita el detalle de corrección de un identificador que no corresponde a ningún resultado
- **THEN** el sistema responde `404 Not Found`

#### Scenario: El corrector ve el enunciado que se formuló
- **GIVEN** un resultado pendiente de corrección cuya pregunta abierta se ha editado después del envío
- **WHEN** un administrador solicita el detalle de corrección
- **THEN** el sistema SHALL mostrar el enunciado que se le formuló al candidato, de forma que la respuesta no se juzgue contra una pregunta distinta de la que se le hizo

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

### Requirement: Recálculo y cierre del resultado tras la corrección
Al aceptar una corrección válida, el sistema SHALL recalcular `ObtainedPoints` como la suma de los puntos de las preguntas de test acertadas más los `AwardedPoints` de todas las respuestas abiertas, recalcular `ScorePercentage` sobre `TotalPoints` (redondeado a 2 decimales), determinar `Passed` comparando contra `PassingScorePercentage` del examen, fijar `Status = Reviewed`, registrar `ReviewedAt` y `ReviewedByUserId` con el administrador que corrigió, y solo entonces enviar al candidato el correo con el resultado definitivo.

#### Scenario: Recálculo con puntuación parcial en abiertas
- **GIVEN** un examen de 10 puntos totales donde el candidato acertó 5 puntos en preguntas de test y tiene una abierta de 5 puntos
- **WHEN** el administrador otorga 3 puntos a la respuesta abierta
- **THEN** el sistema fija `ObtainedPoints = 8`, `ScorePercentage = 80,00` y determina `Passed` comparando 80,00 contra el `PassingScorePercentage` del examen

#### Scenario: Trazabilidad de la corrección
- **WHEN** un administrador completa la corrección de un resultado
- **THEN** el sistema registra en el resultado el instante de la corrección (`ReviewedAt`) y el identificador del administrador que la realizó (`ReviewedByUserId`)

#### Scenario: Notificación del resultado definitivo
- **WHEN** el resultado queda marcado como `Reviewed` tras la corrección
- **THEN** el sistema envía al `CandidateEmail` el correo de resultado con el título del examen, el porcentaje definitivo y si aprobó — el mismo correo que hoy se envía al finalizar un examen sin preguntas abiertas

#### Scenario: El fallo del envío de correo no revierte la corrección
- **GIVEN** una corrección válida ya persistida con `Status = Reviewed`
- **WHEN** el envío del correo de resultado falla
- **THEN** el sistema SHALL registrar el fallo en el log y mantener la corrección persistida, sin devolver el resultado al estado `PendingReview`

### Requirement: Reserva de un resultado mientras se corrige
Abrir el detalle de corrección de un resultado pendiente SHALL reservarlo para quien lo abre durante 30 minutos, sea administrador o evaluador. La reserva SHALL guardar quién la tiene y hasta cuándo. Mientras una reserva está vigente, el sistema SHALL responder `409 Conflict` a cualquier otra persona que abra el detalle o envíe la corrección de ese resultado, e indicar hasta cuándo dura. Quien tiene la reserva SHALL poder renovarla por otros 30 minutos, abrir el detalle otra vez y enviar la corrección.

Una reserva caducada SHALL tratarse como libre: la siguiente persona que abre el detalle la toma. Enviar la corrección SHALL liberar la reserva en la misma transacción que cierra el resultado. Quien tiene la reserva SHALL poder liberarla sin corregir. Un administrador SHALL poder liberar la reserva de cualquier otra persona.

El motivo es que, con varios correctores sobre la misma prueba, el `409` de un resultado ya corregido llega después de que el segundo corrector haya hecho todo el trabajo.

#### Scenario: Primera apertura reserva el resultado
- **GIVEN** un resultado pendiente sin reserva
- **WHEN** un evaluador asignado abre su detalle de corrección
- **THEN** el resultado queda reservado para él durante 30 minutos

#### Scenario: Segunda persona mientras dura la reserva
- **GIVEN** un resultado reservado por un evaluador hace diez minutos
- **WHEN** otro corrector abre el detalle o envía la corrección
- **THEN** el sistema responde `409 Conflict` indicando el fin de la reserva, y no modifica nada

#### Scenario: Reserva caducada
- **GIVEN** un resultado cuya reserva terminó hace un minuto
- **WHEN** otro corrector abre su detalle
- **THEN** el sistema le da la reserva a él y responde con el detalle

#### Scenario: Renovación mientras la pantalla sigue abierta
- **GIVEN** un evaluador con una reserva que termina en cinco minutos
- **WHEN** la pantalla de corrección renueva la reserva
- **THEN** la reserva termina 30 minutos después de la renovación

#### Scenario: El envío libera la reserva
- **WHEN** quien tiene la reserva envía una corrección válida
- **THEN** el resultado queda `Reviewed` y sin reserva

#### Scenario: Liberación sin corregir
- **GIVEN** un evaluador con la reserva de un resultado
- **WHEN** la libera
- **THEN** el resultado sigue en `PendingReview`, sin reserva, y otro corrector puede abrirlo

#### Scenario: El administrador libera una reserva ajena
- **GIVEN** un resultado reservado por un evaluador
- **WHEN** un administrador libera la reserva
- **THEN** el resultado queda sin reserva
- **AND** el siguiente envío del evaluador responde `200 OK` si nadie lo reservó entretanto, o `409 Conflict` si otra persona lo reservó
