## REMOVED Requirements

### Requirement: Filtrado de preguntas del banco
**Reason**: El requisito excluía del listado las preguntas con `QuestionReviewStatus = PendingReview` o `Rejected`. Ese estado solo existía para las preguntas que generaba el modelo local, que se retira, y ya no existe en el código.
**Migration**: El listado filtra ahora solo por `IsActive`. Ver «Filtrado de preguntas activas del banco».

## ADDED Requirements

### Requirement: Filtrado de preguntas activas del banco
El sistema SHALL permitir listar preguntas filtrando de forma combinable por categoría, nivel de dificultad y tipo de pregunta, devolviendo únicamente preguntas activas (`IsActive = true`) por defecto.

#### Scenario: Filtro combinado por categoría y dificultad
- **GIVEN** un banco de preguntas con múltiples categorías y niveles de dificultad
- **WHEN** se solicita `GET /api/questions?categoryId=1&difficulty=Advanced`
- **THEN** el sistema SHALL devolver únicamente las preguntas activas de la categoría 1 con dificultad `Advanced`, ordenadas por nombre de categoría y dificultad

#### Scenario: Filtro sin parámetros
- **GIVEN** un banco de preguntas existente
- **WHEN** se solicita `GET /api/questions` sin parámetros de filtro
- **THEN** el sistema SHALL devolver el listado resumido de todas las preguntas activas, independientemente de categoría, dificultad o tipo

## MODIFIED Requirements

### Requirement: Visualización de fecha y hora de creación y actualización de una pregunta
El sistema SHALL exponer y mostrar, en la pantalla de edición de una pregunta, la fecha y hora exactas (con precisión de minutos) de creación (`CreatedAt`) y, si existe, de última actualización (`UpdatedAt`) de la pregunta, expresadas en UTC.

#### Scenario: Pregunta con fecha de creación y sin actualizaciones posteriores
- **GIVEN** una pregunta creada y nunca modificada desde su creación
- **WHEN** un administrador abre su pantalla de edición
- **THEN** el sistema SHALL mostrar la fecha y hora de creación en UTC con precisión de minutos, sin mostrar una fecha de última actualización

#### Scenario: Pregunta editada después de su creación
- **GIVEN** una pregunta que fue modificada después de creada
- **WHEN** un administrador abre su pantalla de edición
- **THEN** el sistema SHALL mostrar tanto la fecha y hora de creación como la de última actualización, ambas en UTC con precisión de minutos
