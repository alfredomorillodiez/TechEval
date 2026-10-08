## ADDED Requirements

### Requirement: Script SQL aditivo añade las columnas de corrección manual sin pérdida de datos
El sistema SHALL ofrecer `scripts/add_review_columns.sql` para actualizar el esquema de una base de datos `TechEvalDb` ya existente con las columnas necesarias para la corrección manual (`ExamResults.Status`, `ExamResults.ReviewedAt`, `ExamResults.ReviewedByUserId`, `UserAnswers.AwardedPoints`, `UserAnswers.ReviewerComment`), siguiendo el patrón idempotente y no destructivo ya establecido en `scripts/add_user_link_columns.sql`. El script SHALL poder ejecutarse varias veces sin error y SHALL preservar todos los datos existentes.

#### Scenario: Ejecución sobre una base de datos con datos previos
- **GIVEN** una base de datos `TechEvalDb` con exámenes, preguntas y resultados ya registrados
- **WHEN** se ejecuta `scripts/add_review_columns.sql`
- **THEN** el sistema SHALL añadir las columnas nuevas comprobando antes su existencia con `COL_LENGTH`, sin eliminar ni modificar ninguna fila existente

#### Scenario: Reejecución idempotente del script
- **GIVEN** una base de datos en la que el script ya se ejecutó con éxito
- **WHEN** se vuelve a ejecutar `scripts/add_review_columns.sql`
- **THEN** el sistema SHALL detectar que las columnas, la clave foránea y los índices ya existen y SHALL terminar sin error ni duplicados

#### Scenario: Retrocompatibilidad de los resultados históricos
- **GIVEN** resultados creados antes de este cambio, con la corrección ya cerrada de hecho
- **WHEN** se ejecuta el script de actualización
- **THEN** el sistema SHALL asignarles `Status = Reviewed` como valor por defecto, de modo que no aparezcan en la cola de correcciones pendientes ni alteren las métricas del dashboard

#### Scenario: Clave foránea del corrector
- **WHEN** el script añade la columna `ExamResults.ReviewedByUserId`
- **THEN** el sistema SHALL crearla como `INT NULL` con una clave foránea hacia `dbo.Users(Id)` y un índice asociado, comprobando antes que no existan ya, en coherencia con el tratamiento de `ExamResults.UserId`

### Requirement: El script de creación completa del esquema incluye las columnas de corrección
El sistema SHALL mantener `scripts/create_database.sql` alineado con el modelo, incluyendo las columnas de corrección manual en las definiciones de `ExamResults` y `UserAnswers`, de forma que una base de datos creada desde cero con ese script y otra actualizada con `scripts/add_review_columns.sql` converjan en el mismo esquema.

#### Scenario: Creación desde cero incluye las columnas de corrección
- **WHEN** se ejecuta `scripts/create_database.sql` sobre un servidor sin la base de datos
- **THEN** las tablas `ExamResults` y `UserAnswers` SHALL crearse ya con las columnas de estado, trazabilidad de corrección y puntuación otorgada, sin necesidad de ejecutar después el script aditivo
