## ADDED Requirements

### Requirement: Guion aditivo de las condiciones de la sesión
El sistema SHALL ofrecer `scripts/add_session_terms_columns.sql` para actualizar una base `TechEvalDb` ya existente con las columnas `ExamSessions.TimeLimitMinutesSnapshot` y `ExamSessions.PassingScoreSnapshot`, ambas `INT NULL`. El guion SHALL ser idempotente, SHALL conservar todas las filas y SHALL dejar las sesiones existentes con los dos valores nulos. A diferencia de otros guiones, la API anterior SHALL seguir funcionando contra la base actualizada, porque las columnas son opcionales. `scripts/create_database.sql` SHALL crear las mismas columnas, de modo que una base nueva y una actualizada converjan.

#### Scenario: Ejecutar el guion
- **GIVEN** una base con sesiones existentes
- **WHEN** se ejecuta `scripts/add_session_terms_columns.sql`
- **THEN** existen las dos columnas, nulables
- **AND** ninguna fila ha cambiado y las sesiones existentes tienen los dos valores a `NULL`

#### Scenario: Reejecución
- **GIVEN** una base en la que ya se ejecutó el guion
- **WHEN** se vuelve a ejecutar
- **THEN** termina sin error y sin cambios

#### Scenario: Una base nueva
- **WHEN** se ejecuta `scripts/create_database.sql` sobre un servidor sin la base de datos
- **THEN** `ExamSessions` tiene las dos columnas

### Requirement: Configuración del trabajo de sesiones abandonadas
La API SHALL leer `Sessions:AbandonAfterHours` (24 por defecto, y 0 desactiva el trabajo) y `Sessions:SweepMinutes` (15 por defecto, con un mínimo de 1). Los valores inválidos o negativos SHALL sustituirse por los de por defecto, sin impedir el arranque.

#### Scenario: Sin configuración
- **GIVEN** una API sin sección `Sessions`
- **WHEN** arranca
- **THEN** el trabajo corre cada 15 minutos y marca las sesiones vencidas hace más de 24 horas

#### Scenario: Valor inválido
- **GIVEN** `Sessions:SweepMinutes` con un valor de 0 o negativo
- **WHEN** arranca
- **THEN** la API usa 15 minutos y arranca con normalidad
