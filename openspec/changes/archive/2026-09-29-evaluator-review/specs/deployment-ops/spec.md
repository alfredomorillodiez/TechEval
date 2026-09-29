## ADDED Requirements

### Requirement: Guion aditivo de evaluadores y reservas
El sistema SHALL ofrecer `scripts/add_evaluator_columns.sql` para actualizar una base `TechEvalDb` ya existente con la tabla de asignaciones de evaluadores y las columnas de la reserva de un resultado. El guion SHALL ser idempotente, SHALL conservar todas las filas existentes y SHALL dejar todos los resultados existentes sin reserva. A diferencia de `add_user_roles.sql`, este guion no retira ninguna columna, así que el binario anterior sigue funcionando contra la base actualizada.

#### Scenario: Ejecución sobre una base con resultados previos
- **GIVEN** una base con resultados pendientes y corregidos
- **WHEN** se ejecuta `scripts/add_evaluator_columns.sql`
- **THEN** la base tiene la tabla de asignaciones vacía y las columnas de la reserva a nulo en todos los resultados, sin perder ninguna fila

#### Scenario: Reejecución idempotente
- **GIVEN** una base en la que el guion ya se ejecutó
- **WHEN** se vuelve a ejecutar
- **THEN** el guion termina sin error y sin duplicar objetos

#### Scenario: Convergencia con la creación completa
- **WHEN** se compara una base actualizada con este guion y otra creada con `scripts/create_database.sql`
- **THEN** las dos tienen la misma tabla de asignaciones y las mismas columnas en `ExamResults`
