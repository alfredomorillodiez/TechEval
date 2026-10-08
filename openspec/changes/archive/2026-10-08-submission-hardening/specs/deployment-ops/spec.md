## ADDED Requirements

### Requirement: Guion aditivo del índice único de respuestas
El sistema SHALL ofrecer `scripts/add_useranswers_unique_index.sql` para actualizar una base `TechEvalDb` ya existente con el índice único `IX_UserAnswers_ExamSessionId_QuestionId` y el índice `IX_UserAnswers_QuestionId`. El guion SHALL ser idempotente y SHALL conservar todas las filas. Si la tabla ya contiene más de una `UserAnswer` para la misma pareja `(ExamSessionId, QuestionId)`, el guion SHALL NOT borrar ninguna ni crear el índice único: SHALL detenerse con un error que liste las parejas duplicadas. `scripts/create_database.sql` SHALL crear los mismos dos índices, de modo que una base nueva y una actualizada converjan.

#### Scenario: Base sin duplicados
- **GIVEN** una base `TechEvalDb` cuyas `UserAnswers` no repiten ninguna pareja `(ExamSessionId, QuestionId)`
- **WHEN** se ejecuta `scripts/add_useranswers_unique_index.sql`
- **THEN** existen los dos índices
- **AND** ninguna fila de `UserAnswers` ha cambiado

#### Scenario: Reejecución
- **GIVEN** una base en la que ya se ejecutó el guion
- **WHEN** se vuelve a ejecutar
- **THEN** el guion termina sin error y sin cambios

#### Scenario: Base con respuestas duplicadas
- **GIVEN** una base con dos `UserAnswer` para la misma pareja `(ExamSessionId, QuestionId)`
- **WHEN** se ejecuta el guion
- **THEN** el guion termina con un error que muestra las parejas duplicadas y el número de filas de cada una
- **AND** no existe el índice único
- **AND** ninguna fila de `UserAnswers` se ha borrado ni modificado

#### Scenario: Una base nueva trae los índices
- **WHEN** se ejecuta `scripts/create_database.sql` sobre un servidor sin la base de datos
- **THEN** `UserAnswers` tiene el índice único sobre `(ExamSessionId, QuestionId)` y el índice sobre `QuestionId`
