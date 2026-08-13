## Context
El banco de preguntas es el catálogo maestro sobre el que se construirán los exámenes técnicos de TechEval. Cada pregunta pertenece a una categoría, tiene un nivel de dificultad y un tipo (test u abierta), y una vez usada en un examen queda referenciada desde el histórico de resultados de los candidatos (`ExamQuestion`, `UserAnswer`). Esto convierte a `Question` en una entidad con "gravedad": no puede tratarse como un simple registro CRUD desechable, porque otras tablas dependen de su identidad incluso después de que deje de estar disponible para nuevos exámenes.

## Goals / Non-Goals
**Goals**
- Modelar categorías y preguntas con las reglas de negocio mínimas necesarias para garantizar exámenes de calidad: nombre de categoría único, preguntas tipo test con exactamente 4 opciones y 1 correcta, preguntas abiertas con respuesta modelo opcional.
- Permitir filtrar y listar preguntas de forma eficiente por categoría, dificultad y tipo, con índices adecuados.
- Preservar la integridad referencial e histórica cuando una pregunta o categoría deja de usarse.

**Non-Goals**
- No se aborda en este cambio la selección aleatoria de preguntas para armar un examen (`GetRandomAsync` ya existe en el repositorio pero su uso pertenece a la capacidad `exam-management`).
- No se implementa versionado de preguntas (historial de ediciones) ni importación/exportación masiva del banco.
- No se define aquí la corrección de respuestas abiertas por parte de un revisor humano; `SampleAnswer` solo se expone como referencia de apoyo.

## Decisions

### Soft delete en lugar de hard delete para preguntas y categorías
**Decisión**: eliminar una pregunta o categoría se implementa como una actualización de estado (`IsActive = false`), nunca como un `DELETE` físico en base de datos. Esto se refuerza a nivel de esquema con `OnDelete(DeleteBehavior.Restrict)` entre `Question` y `Category`, y entre `Question`/`ExamQuestion`/`UserAnswer`, de modo que ni siquiera un borrado accidental en cascada es posible.

**Por qué**: las preguntas quedan referenciadas desde el historial de resultados de los candidatos (`ExamQuestion`, `UserAnswer`) una vez que se usan en un examen. Un borrado físico dejaría esos resultados huérfanos —sin texto de pregunta, sin respuestas, sin contexto— haciendo imposible auditar o revisar un examen ya completado. El soft delete conserva el registro completo: la pregunta sigue existiendo para cualquier resultado histórico que la referencie, simplemente deja de ofrecerse para nuevos exámenes (se excluye de los listados filtrados por `IsActive = true` y de la selección aleatoria).

**Alternativas consideradas**:
- *Hard delete con `ON DELETE SET NULL`*: se descartó porque dejaría resultados de exámenes con preguntas "vacías", degradando la trazabilidad y la posibilidad de justificar una calificación ante un candidato o un cliente.
- *Archivado en tabla separada*: se descartó por complejidad adicional sin beneficio claro frente a un simple flag `IsActive`, dado el volumen de datos esperado.

### Validación de respuestas en la capa de aplicación, no en la base de datos
**Decisión**: la regla "4 opciones, 1 correcta" para preguntas tipo test se valida en `QuestionService.ValidateAnswers`, lanzando `InvalidOperationException`, en lugar de expresarse como una constraint de base de datos.

**Por qué**: la regla depende del `QuestionType` de la pregunta (solo aplica a `MultipleChoice`, no a `OpenEnded`), lo cual es difícil de expresar como `CHECK constraint` portable en SQL Server sin acoplar fuertemente el esquema a esta regla de negocio. Mantenerla en la capa de aplicación permite mensajes de error claros en español y facilita evolucionar la regla (p. ej. permitir preguntas de opción múltiple con varias respuestas correctas) sin migraciones de esquema.

### Actualización de preguntas reemplaza el conjunto completo de respuestas
**Decisión**: `UpdateAsync` en `QuestionService` limpia (`Answers.Clear()`) y reconstruye la colección completa de respuestas en cada edición, en lugar de hacer un diff incremental.

**Por qué**: dado el volumen reducido de respuestas por pregunta (máximo 4 para tipo test), la simplicidad de reemplazar todo el conjunto supera el costo de un diff granular, y evita inconsistencias de `Order` al reordenar opciones desde el cliente.

## Risks / Trade-offs
- **Crecimiento indefinido de preguntas inactivas**: como nunca se eliminan físicamente, la tabla `Questions` crecerá con preguntas retiradas. Mitigación: los índices en `CategoryId`, `Difficulty` e `IsActive` mantienen el filtrado eficiente; si el volumen se vuelve significativo, se podría evaluar particionado o archivado en frío en una capacidad futura.
- **Nombre de categoría único a nivel de base de datos**: el índice único en `Category.Name` es la única barrera contra duplicados; una carrera de solicitudes concurrentes con el mismo nombre resultará en una excepción de base de datos que debe traducirse a un `400`/`409` legible por el middleware de errores global (capacidad `project-architecture`).
- **Reemplazo completo de respuestas en cada `Update`**: simplifica el código pero implica que los `Id` de las respuestas anteriores se pierden y se generan nuevos registros en cada edición; si en el futuro se necesita trazabilidad de cambios de una respuesta específica, este enfoque debería revisarse.
