## MODIFIED Requirements

### Requirement: Filtrado de preguntas del banco
El sistema SHALL permitir listar preguntas filtrando de forma combinable por categoría, nivel de dificultad y tipo de pregunta, devolviendo únicamente preguntas activas y con revisión aprobada (`QuestionReviewStatus = Approved`) por defecto.

#### Scenario: Filtro combinado por categoría y dificultad
- **GIVEN** un banco de preguntas con múltiples categorías y niveles de dificultad
- **WHEN** se solicita `GET /api/questions?categoryId=1&difficulty=Advanced`
- **THEN** el sistema SHALL devolver únicamente las preguntas activas y aprobadas de la categoría 1 con dificultad `Advanced`, ordenadas por nombre de categoría y dificultad

#### Scenario: Filtro sin parámetros
- **GIVEN** un banco de preguntas existente
- **WHEN** se solicita `GET /api/questions` sin parámetros de filtro
- **THEN** el sistema SHALL devolver el listado resumido de todas las preguntas activas y aprobadas, independientemente de categoría, dificultad o tipo

#### Scenario: Preguntas pendientes de revisión o rechazadas quedan excluidas
- **GIVEN** un banco de preguntas con algunas preguntas en `QuestionReviewStatus = PendingReview` o `QuestionReviewStatus = Rejected`
- **WHEN** se solicita `GET /api/questions` con o sin filtros de categoría, dificultad o tipo
- **THEN** el sistema SHALL excluir esas preguntas del resultado en todos los casos, de forma que nunca queden disponibles para la creación manual o generación automática de pruebas mientras no estén aprobadas
