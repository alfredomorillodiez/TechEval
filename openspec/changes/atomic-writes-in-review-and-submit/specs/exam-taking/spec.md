## ADDED Requirements

### Requirement: Atomicidad de la escritura del envío
El sistema SHALL escribir en una sola transacción de base de datos todo lo que produce el envío de una prueba: las `UserAnswer` de cada pregunta, el `ExamResult` con su puntuación y el cierre de la `ExamSession`. Si cualquiera de esas escrituras falla, ninguna SHALL quedar persistida. El envío del correo al candidato SHALL quedar fuera de esa transacción, porque es una llamada externa y mantener la transacción abierta mientras se espera bloquearía filas sin motivo.

#### Scenario: Envío correcto
- **GIVEN** una sesión en progreso con sus respuestas
- **WHEN** el candidato envía la prueba y todas las escrituras tienen éxito
- **THEN** quedan persistidas las respuestas, el resultado y el cierre de la sesión
- **AND** el correo se envía después de confirmar la transacción

#### Scenario: Fallo al escribir el resultado
- **GIVEN** una sesión en progreso con sus respuestas ya puntuadas en memoria
- **WHEN** la escritura del `ExamResult` falla
- **THEN** el sistema SHALL revertir también las `UserAnswer` escritas en ese envío
- **AND** la sesión sigue en `InProgress`, sin resultado asociado
- **AND** el candidato puede reanudar y volver a enviar

#### Scenario: Fallo al cerrar la sesión
- **GIVEN** un envío en el que el `ExamResult` ya se ha escrito dentro de la transacción
- **WHEN** falla la actualización de `ExamSession.Status`
- **THEN** el sistema SHALL revertir también el `ExamResult`
- **AND** no queda ninguna sesión con resultado y estado `InProgress` a la vez

#### Scenario: El fallo del correo no revierte el envío
- **GIVEN** un envío cuya transacción ya se ha confirmado
- **WHEN** falla el envío del correo al candidato
- **THEN** el resultado SHALL seguir persistido, porque el correo queda fuera de la transacción
