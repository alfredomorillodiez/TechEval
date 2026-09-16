## MODIFIED Requirements

### Requirement: Detalle de resultado con revisión de respuestas
El sistema SHALL permitir a usuarios con rol `Admin` consultar el detalle de un resultado (`GET /api/results/{id}`), incluyendo el estado de corrección y, por cada pregunta de la sesión, el texto de la pregunta, la respuesta seleccionada o el texto abierto del candidato, la respuesta correcta esperada, si fue evaluada como correcta (o `null` si es una pregunta abierta pendiente), los puntos de la pregunta, los puntos otorgados (`AwardedPoints`, `null` si está pendiente) y el comentario del corrector cuando exista; si el resultado no existe, SHALL responder `404`. El enunciado, la respuesta seleccionada, la correcta y los puntos de la pregunta SHALL leerse de la copia congelada en la `UserAnswer` y no de la pregunta actual del banco.

#### Scenario: Consulta de detalle de un resultado existente
- **WHEN** un administrador solicita `GET /api/results/{id}` para un `id` de resultado existente
- **THEN** el sistema responde con los datos del resultado, su estado de corrección y la lista de revisión de respuestas, una entrada por cada pregunta única respondida en la sesión

#### Scenario: Detalle de un resultado ya corregido muestra la puntuación otorgada
- **WHEN** un administrador consulta el detalle de un resultado con `Status = Reviewed` que incluía preguntas abiertas
- **THEN** cada respuesta abierta se expone con los `AwardedPoints` otorgados por el corrector y su comentario, cuando exista

#### Scenario: Consulta de detalle de un resultado inexistente
- **WHEN** un administrador solicita `GET /api/results/{id}` para un `id` que no corresponde a ningún resultado
- **THEN** el sistema responde `404 Not Found`

#### Scenario: El detalle no cambia al editar la pregunta
- **GIVEN** un resultado cuyo detalle muestra el enunciado y las opciones de una pregunta
- **WHEN** un administrador edita esa pregunta en el banco y se vuelve a consultar el detalle
- **THEN** el sistema SHALL devolver el mismo enunciado, la misma opción elegida, la misma opción correcta y los mismos puntos máximos que antes de la edición
