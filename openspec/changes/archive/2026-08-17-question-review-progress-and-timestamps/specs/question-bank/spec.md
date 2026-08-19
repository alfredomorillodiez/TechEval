## ADDED Requirements

### Requirement: Visualización de fecha y hora de creación y actualización de una pregunta
El sistema SHALL exponer y mostrar, en la pantalla de edición de una pregunta, la fecha y hora exactas (con precisión de minutos) de creación (`CreatedAt`) y, si existe, de última actualización (`UpdatedAt`) de la pregunta, expresadas en UTC, tanto para preguntas creadas manualmente como generadas por IA.

#### Scenario: Pregunta con fecha de creación y sin actualizaciones posteriores
- **GIVEN** una pregunta creada y nunca modificada desde su creación
- **WHEN** un administrador abre su pantalla de edición
- **THEN** el sistema SHALL mostrar la fecha y hora de creación en UTC con precisión de minutos, sin mostrar una fecha de última actualización

#### Scenario: Pregunta editada después de su creación
- **GIVEN** una pregunta que fue modificada después de creada
- **WHEN** un administrador abre su pantalla de edición
- **THEN** el sistema SHALL mostrar tanto la fecha y hora de creación como la de última actualización, ambas en UTC con precisión de minutos
