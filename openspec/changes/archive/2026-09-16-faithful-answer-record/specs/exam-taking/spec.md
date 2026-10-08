## MODIFIED Requirements

### Requirement: Auto-guardado de respuestas individuales
El sistema SHALL permitir guardar la respuesta de una pregunta concreta dentro de una sesión de examen en curso, de forma incremental, sin requerir el envío completo del examen. La fecha de respuesta SHALL registrarse siempre en tiempo universal coordinado (UTC), igual que el resto de marcas de tiempo del modelo.

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

## ADDED Requirements

### Requirement: Congelación de lo preguntado en el momento del envío
Al cerrar el envío de un examen, el sistema SHALL guardar en cada `UserAnswer` una copia del enunciado de la pregunta, del texto de la opción elegida, del texto de la opción correcta y de los puntos máximos de la pregunta, tal como estaban en ese momento. Las consultas de resultados y de corrección SHALL leer esa copia y no la pregunta actual, de forma que editar una pregunta más adelante no altere lo que consta en los exámenes ya cerrados.

#### Scenario: Editar el enunciado no cambia un resultado ya cerrado
- **GIVEN** un examen cerrado cuya ficha de resultados muestra el enunciado de una pregunta
- **WHEN** un administrador edita el texto de esa pregunta en el banco
- **THEN** la ficha del resultado SHALL seguir mostrando el enunciado que se le formuló al candidato

#### Scenario: Editar las opciones no cambia lo que consta que se eligió
- **GIVEN** un examen cerrado en el que el candidato eligió una opción de una pregunta de test
- **WHEN** un administrador edita el texto de esa opción, o marca como correcta una opción distinta
- **THEN** la ficha del resultado SHALL seguir mostrando el texto de la opción que el candidato eligió y el de la que era correcta entonces

#### Scenario: Editar los puntos no descuadra la nota mostrada
- **GIVEN** un examen cerrado en el que una pregunta valía diez puntos
- **WHEN** un administrador cambia el valor de esa pregunta a cinco puntos
- **THEN** la ficha del resultado SHALL seguir mostrando diez como máximo de esa pregunta, de forma que los puntos otorgados nunca superen el máximo que se muestra

#### Scenario: Respuestas anteriores al cambio
- **GIVEN** respuestas registradas antes de que existiera esta copia
- **WHEN** un administrador consulta su ficha de resultados
- **THEN** el sistema SHALL mostrar el texto con el que se rellenaron esas filas al aplicar el cambio, que es el mismo que mostraba antes
