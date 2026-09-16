## MODIFIED Requirements

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
