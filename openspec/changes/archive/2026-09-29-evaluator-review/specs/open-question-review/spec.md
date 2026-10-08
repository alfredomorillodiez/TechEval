## ADDED Requirements

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
