## ADDED Requirements

### Requirement: Validación de los datos de una prueba
El sistema SHALL validar en el servidor los datos de una prueba al crearla, generarla o editarla, y MUST responder `400 Bad Request` con un mensaje que diga qué falla, sin escribir nada. El título SHALL no estar vacío ni pasar de 200 caracteres. El tiempo límite SHALL estar entre 1 y 600 minutos. La nota de corte SHALL estar entre 0 y 100. El alta manual SHALL incluir al menos una pregunta y SHALL NOT repetir ninguna. La generación automática SHALL pedir entre 1 y 200 preguntas.

#### Scenario: Prueba de cero minutos
- **WHEN** un administrador envía `POST /api/exams` con `TimeLimitMinutes = 0`
- **THEN** el sistema responde `400` indicando que el tiempo límite debe estar entre 1 y 600 minutos
- **AND** no se crea ninguna prueba

#### Scenario: Prueba sin preguntas
- **WHEN** un administrador envía `POST /api/exams` con una lista de preguntas vacía
- **THEN** el sistema responde `400` indicando que hace falta al menos una pregunta

#### Scenario: Preguntas repetidas
- **WHEN** un administrador envía `POST /api/exams` con el mismo identificador de pregunta dos veces
- **THEN** el sistema responde `400` indicando que hay preguntas repetidas

#### Scenario: Nota de corte fuera de rango
- **WHEN** un administrador envía una prueba con `PassingScorePercentage = 150`
- **THEN** el sistema responde `400` indicando que la nota de corte debe estar entre 0 y 100

#### Scenario: Título vacío
- **WHEN** un administrador envía una prueba con el título en blanco
- **THEN** el sistema responde `400` indicando que el título es obligatorio

#### Scenario: La edición aplica las mismas reglas
- **WHEN** un administrador envía `PUT /api/exams/{id}` con `TimeLimitMinutes = 0`
- **THEN** el sistema responde `400` y la prueba no cambia

#### Scenario: Generación con cero preguntas
- **WHEN** un administrador envía `POST /api/exams/generate` con `QuestionCount = 0`
- **THEN** el sistema responde `400` indicando que debe pedir entre 1 y 200 preguntas

### Requirement: Las categorías desactivadas no aportan preguntas a una prueba
Una pregunta SHALL entrar en la generación automática, en la disponibilidad que esta anuncia y en el alta manual de una prueba solo si la pregunta y su categoría están activas. Las preguntas de una categoría desactivada SHALL NOT contarse como disponibles.

#### Scenario: Generación con una categoría desactivada
- **GIVEN** una categoría desactivada con preguntas activas
- **WHEN** un administrador genera una prueba sin indicar categorías
- **THEN** ninguna de esas preguntas entra en la prueba

#### Scenario: La disponibilidad no cuenta una categoría desactivada
- **GIVEN** una categoría desactivada con 10 preguntas activas
- **WHEN** se consulta `GET /api/questions/availability`
- **THEN** esas 10 preguntas no figuran en el recuento

#### Scenario: Alta manual con una pregunta de categoría desactivada
- **GIVEN** una pregunta activa cuya categoría está desactivada
- **WHEN** un administrador la incluye en `POST /api/exams`
- **THEN** el sistema responde `400` indicando que algunas preguntas no existen o están inactivas
