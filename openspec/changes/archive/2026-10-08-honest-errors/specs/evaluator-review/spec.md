## ADDED Requirements

### Requirement: Un fallo de red al renovar la reserva no borra la corrección
La pantalla de corrección, del evaluador y del administrador, SHALL distinguir tres resultados al renovar la reserva: renovada; perdida (`404` o `409`: otra persona la tiene o el resultado ya no está disponible); y sin respuesta (fallo de red, `429` o `5xx`). Solo «perdida» SHALL reemplazar el formulario por el aviso de reserva perdida. «Sin respuesta» SHALL dejar el formulario intacto con todo lo escrito, mostrar un aviso de que no se pudo renovar y de que la corrección sigue en pantalla, y reintentar la renovación cada 30 segundos hasta que el servidor responda. El aviso SHALL desaparecer cuando una renovación tenga éxito.

#### Scenario: Se corta la red al renovar
- **GIVEN** un evaluador con puntuaciones y comentarios escritos en el formulario
- **WHEN** la renovación de la reserva falla por la red
- **THEN** el formulario sigue en pantalla con todo lo escrito
- **AND** aparece el aviso de que no se pudo renovar la reserva

#### Scenario: La red vuelve
- **GIVEN** el aviso de renovación fallida
- **WHEN** un reintento de la renovación tiene éxito
- **THEN** el aviso desaparece y el formulario sigue intacto

#### Scenario: La reserva se perdió de verdad
- **GIVEN** una renovación que el servidor rechaza con `409`
- **WHEN** la respuesta llega
- **THEN** la pantalla muestra el aviso de reserva perdida en lugar del formulario

#### Scenario: Un 429 al renovar
- **WHEN** la renovación recibe `429`
- **THEN** se trata como «sin respuesta»: no se pierde el formulario y se reintenta
