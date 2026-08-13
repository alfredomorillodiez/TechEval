## Why
TechEval necesita un banco de preguntas centralizado, clasificado por categoría, dificultad y tipo, para que los administradores puedan construir exámenes técnicos consistentes sin reinventar el contenido en cada convocatoria. Sin un catálogo estructurado, cada examen se armaría de forma ad-hoc a partir de preguntas dispersas, lo que dificultaría reutilizar contenido validado, calibrar la dificultad del examen y mantener la calidad de las preguntas a lo largo del tiempo. Además, dado que cada pregunta queda vinculada al histórico de resultados de los candidatos que la respondieron, el catálogo debe permitir retirar preguntas obsoletas sin destruir esa trazabilidad.

## What Changes
- CRUD de **categorías**: nombre único, descripción y conteo de preguntas activas asociadas.
- CRUD de **preguntas**, cada una vinculada a una categoría, con tipo (`MultipleChoice` / `OpenEnded`), nivel de dificultad (`Basic` / `Intermediate` / `Advanced`) y puntuación.
- Para preguntas de **tipo test**: gestión de un conjunto de respuestas ordenadas, con validación de que existan **exactamente 4 opciones** y **exactamente 1** marcada como correcta.
- Para preguntas **abiertas**: campo de **respuesta modelo** (`SampleAnswer`) que sirve de referencia al corrector humano.
- **Filtrado** de preguntas por categoría, dificultad y/o tipo, con listado resumido (`QuestionSummaryDto`) y vista de detalle con las respuestas ordenadas (`QuestionDto`).
- **Soft delete** de preguntas y categorías (marcado `IsActive = false`) en lugar de borrado físico, preservando la integridad del histórico de resultados.
- **Validaciones de datos**: longitudes máximas de texto, nombre de categoría único, y reglas de consistencia de respuestas según el tipo de pregunta.
- Endpoints REST bajo `/api/categories` y `/api/questions`, protegidos con autorización por rol `Admin` para las operaciones de escritura (creación, edición, borrado), reutilizando la autenticación JWT existente.

## Capabilities
### New Capabilities
- `question-bank`: Gestión del banco de preguntas técnicas y sus categorías — creación, edición, filtrado y baja lógica de preguntas (tipo test u abiertas) y categorías, con las validaciones de consistencia necesarias para garantizar exámenes de calidad.

### Modified Capabilities
(ninguna — `question-bank` es una capacidad nueva)

## Impact
- **Código afectado**: entidades `Category`, `Question`, `Answer`; enums `DifficultyLevel`, `QuestionType`; `CategoryService`, `QuestionService` y sus interfaces; DTOs (`CategoryDto`, `QuestionDto`, `AnswerDto` y variantes de creación/actualización); controladores `CategoriesController` y `QuestionsController`; `QuestionRepository` y las configuraciones EF Core `QuestionConfiguration`, `AnswerConfiguration`, `CategoryConfiguration`.
- **Dependencias previas**: se apoya en `project-architecture` (patrón de repositorio genérico, `AuditableEntity`, manejo global de errores) y en `authentication` (JWT + autorización por rol `Admin` en los endpoints de escritura).
- **Base de datos**: tablas `Categories`, `Questions` y `Answers`, con índice único en `Category.Name`, índices en `Question.CategoryId`, `Question.Difficulty` e `Question.IsActive`, y `DeleteBehavior.Restrict` entre `Question` y `Category` para impedir borrados físicos en cascada.
- **Consumidores futuros**: este banco de preguntas es la base sobre la que se construirán la selección de preguntas para exámenes (`exam-management`) y la corrección de respuestas abiertas.
