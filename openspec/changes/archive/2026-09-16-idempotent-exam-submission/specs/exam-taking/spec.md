## MODIFIED Requirements

### Requirement: Envío final del examen
El sistema SHALL permitir al candidato enviar el conjunto completo de respuestas de su sesión para marcarla como finalizada, dejando la corrección automática y el cálculo de puntuación a cargo de otra capacidad. El envío SHALL ser idempotente: una sesión que ya tiene resultado devuelve ese resultado en lugar de crear otro, sin recalcular la nota ni reenviar ninguna notificación.

#### Scenario: Envío exitoso del examen
- **GIVEN** una `ExamSession` en progreso
- **WHEN** el candidato envía `POST /api/exam/submit` con el identificador de sesión y el conjunto de respuestas
- **THEN** el sistema persiste cada respuesta enviada (creando o actualizando la `UserAnswer` correspondiente a cada pregunta)
- **AND** el sistema marca la sesión como `Completed` y registra `CompletedAt` con la fecha/hora actual

#### Scenario: Segundo envío de la misma sesión
- **GIVEN** una `ExamSession` que ya tiene un `ExamResult` asociado
- **WHEN** se solicita `POST /api/exam/submit` de nuevo con ese identificador de sesión
- **THEN** el sistema responde `200 OK` con el acuse del resultado ya guardado
- **AND** el sistema SHALL NOT crear un segundo `ExamResult`
- **AND** el sistema SHALL NOT modificar la nota, el veredicto ni ninguna `UserAnswer`

#### Scenario: Segundo envío con respuestas distintas
- **GIVEN** una `ExamSession` que ya tiene un `ExamResult` asociado
- **WHEN** se solicita `POST /api/exam/submit` con un conjunto de respuestas diferente al del primer envío
- **THEN** el sistema devuelve el acuse del resultado original sin alterarlo
- **AND** la prueba se corrige una sola vez: el primer envío es el que cuenta

#### Scenario: Segundo envío sobre una sesión a medio cerrar
- **GIVEN** una `ExamSession` con `ExamResult` asociado pero todavía marcada `InProgress`, porque el envío se interrumpió entre la escritura del resultado y la del estado
- **WHEN** se solicita `POST /api/exam/submit` con ese identificador de sesión
- **THEN** el sistema devuelve el acuse del resultado ya guardado
- **AND** la detección se apoya en la existencia del resultado, no en el estado de la sesión

#### Scenario: El segundo envío no reenvía correo al candidato
- **GIVEN** una `ExamSession` que ya tiene resultado y cuyo correo de resultado o de acuse ya se envió
- **WHEN** se solicita `POST /api/exam/submit` de nuevo
- **THEN** el sistema SHALL NOT enviar ningún correo adicional al candidato

#### Scenario: El acuse repetido de una prueba pendiente sigue sin cifras
- **GIVEN** una `ExamSession` cuyo `ExamResult` está en estado `PendingReview`
- **WHEN** se solicita `POST /api/exam/submit` de nuevo
- **THEN** el acuse devuelto lleva el estado pendiente y SHALL NOT incluir puntuación, porcentaje ni veredicto

#### Scenario: Envío referenciando una sesión inexistente
- **GIVEN** un identificador de sesión que no corresponde a ninguna `ExamSession` almacenada
- **WHEN** se solicita `POST /api/exam/submit` con ese identificador
- **THEN** el sistema MUST rechazar la operación indicando que la sesión no fue encontrada, sin registrar ningún resultado
