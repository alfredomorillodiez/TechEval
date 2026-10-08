## ADDED Requirements

### Requirement: Disponibilidad de preguntas por nivel
El sistema SHALL exponer a usuarios con rol `Admin` el número de preguntas activas de cada nivel de dificultad, filtrado por una lista opcional de categorías. Sin categorías, SHALL contar todo el banco activo. La respuesta SHALL incluir los tres niveles, también los que tienen cero preguntas. Las preguntas dadas de baja SHALL NOT contarse.

#### Scenario: Disponibilidad de unas categorías
- **GIVEN** la categoría Azure con 8 preguntas básicas, 15 intermedias y 2 avanzadas activas, y 1 avanzada dada de baja
- **WHEN** un administrador consulta la disponibilidad de Azure
- **THEN** el sistema responde 8 / 15 / 2

#### Scenario: Nivel sin preguntas
- **GIVEN** una categoría sin preguntas avanzadas
- **WHEN** un administrador consulta su disponibilidad
- **THEN** la respuesta incluye el nivel avanzado con 0

#### Scenario: Acceso sin rol Admin
- **WHEN** un usuario con rol `Evaluador` o `Alumno` consulta la disponibilidad
- **THEN** el sistema responde `403 Forbidden`
