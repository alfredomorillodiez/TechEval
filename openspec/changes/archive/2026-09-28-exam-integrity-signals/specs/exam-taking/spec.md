## ADDED Requirements

### Requirement: Orden de presentación propio de cada sesión
El sistema SHALL presentar a cada sesión de examen las preguntas en una permutación propia de la sesión, y las opciones de cada pregunta de test en una permutación propia de la sesión y de la pregunta. El sistema SHALL generar la permutación a partir de un valor aleatorio que se crea con la sesión y se guarda con ella. La permutación SHALL ser estable: cada inicio o reanudación de la misma sesión SHALL devolver el mismo orden.

El orden del examen (`ExamQuestion.Order`) SHALL seguir siendo el orden de referencia. El detalle del resultado y la corrección de preguntas abiertas SHALL mostrar las preguntas en el orden del examen, de forma que el corrector compare candidatos sobre el mismo orden.

La corrección automática SHALL identificar la opción elegida por su identificador, nunca por su posición en pantalla.

#### Scenario: Dos candidatos del mismo examen
- **GIVEN** un examen con varias preguntas y dos invitaciones a ese examen
- **WHEN** cada candidato inicia su sesión
- **THEN** el orden de las preguntas y de las opciones de cada sesión se obtiene del valor aleatorio de esa sesión, de forma independiente de la otra

#### Scenario: El candidato recarga la página
- **GIVEN** una `ExamSession` en curso cuyo detalle presentó las preguntas en un orden determinado
- **WHEN** el candidato solicita `POST /api/exam/start/{token}` de nuevo
- **THEN** el sistema devuelve las preguntas y las opciones en el mismo orden que la primera vez

#### Scenario: El número de orden que recibe la interfaz
- **WHEN** el sistema devuelve el detalle de una sesión
- **THEN** el campo de orden de cada pregunta indica su posición en la sesión, empezando en 1, y no su posición en el examen

#### Scenario: Corrección de una pregunta de test con opciones permutadas
- **GIVEN** una pregunta de test cuya opción correcta está en la tercera posición del banco y en la primera posición de la sesión
- **WHEN** el candidato elige la opción de la primera posición y envía la prueba
- **THEN** el sistema corrige la respuesta como correcta

#### Scenario: Sesión empezada antes del cambio
- **GIVEN** una `ExamSession` en curso que se creó antes de este cambio y no tiene valor aleatorio
- **WHEN** el candidato reanuda la sesión
- **THEN** el sistema presenta las preguntas y las opciones en el orden del examen, igual que al empezar
