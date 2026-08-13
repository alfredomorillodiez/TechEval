## Why
Los administradores necesitan poder ensamblar exámenes técnicos a partir del banco de preguntas ya existente, tanto seleccionando preguntas puntualmente como generándolos de forma automática a partir de criterios (cantidad, categoría, dificultad), para no tener que armar cada evaluación pregunta por pregunta. Cada examen debe tener parámetros de negociación propios (tiempo límite, porcentaje mínimo de aprobación) y debe poder desactivarse sin perder el histórico de resultados asociado, en lugar de eliminarse físicamente. Además, el panel de administración necesita ver de un vistazo cuántos exámenes existen, cuántas preguntas y puntos tiene cada uno, y cuántos tokens/resultados se han generado a partir de ellos.

## What Changes
- Nueva entidad `Exam` (título, descripción, tiempo límite en minutos, porcentaje de aprobación, estado activo/inactivo, usuario creador, puntos totales calculados) y `ExamQuestion` como tabla de asociación ordenada entre `Exam` y `Question`.
- Endpoint `POST /api/exams` para **creación manual**: el administrador selecciona una lista de `QuestionIds` existentes y activas; el examen se rechaza si alguna pregunta no existe o está inactiva.
- Endpoint `POST /api/exams/generate` para **generación automática**: se especifica una cantidad de preguntas (`QuestionCount`) y, opcionalmente, categorías (`CategoryIds`) y dificultad (`Difficulty`); el sistema selecciona preguntas activas al azar que cumplan esos criterios.
- Validación de disponibilidad: si el banco de preguntas no tiene suficientes preguntas activas que cumplan los criterios solicitados, la generación falla con un mensaje indicando cuántas se encontraron frente a las solicitadas, sin crear el examen.
- Orden de preguntas persistido por examen (`ExamQuestion.Order`), asignado secuencialmente tanto en creación manual (orden de selección) como en generación automática (orden de sorteo), para que la presentación al candidato sea siempre consistente.
- Endpoints `GET /api/exams` (listado con estadísticas: cantidad de preguntas, puntos totales, tokens enviados, resultados obtenidos) y `GET /api/exams/{id}` (detalle completo con preguntas, respuestas y datos de la pregunta correcta, para uso administrativo).
- Endpoint `PUT /api/exams/{id}` para editar título, descripción, tiempo límite, porcentaje de aprobación y estado activo.
- Endpoint `DELETE /api/exams/{id}` implementado como **soft delete**: marca el examen como inactivo (`IsActive = false`) en lugar de borrarlo, preservando el historial de tokens y resultados ya generados.
- Todos los endpoints de esta capacidad requieren rol `Admin`.

## Capabilities
### New Capabilities
- `exam-management`: Gestión de exámenes técnicos — creación manual a partir de preguntas existentes del banco, generación automática aleatoria por cantidad/categoría/dificultad, configuración de tiempo límite y porcentaje de aprobación, orden persistente de preguntas, listado con estadísticas y desactivación (soft delete) de exámenes.

### Modified Capabilities
(ninguna)

## Impact
- **Código afectado**: `src/TechEval.Domain/Entities/Exam.cs`, `src/TechEval.Domain/Entities/ExamQuestion.cs`, `src/TechEval.Domain/Interfaces/Repositories/IExamRepository.cs`, `src/TechEval.Infrastructure/Repositories/ExamRepository.cs`, `src/TechEval.Application/Services/ExamService.cs`, `src/TechEval.Application/DTOs/ExamDto.cs`, `src/TechEval.API/Controllers/ExamsController.cs`.
- **Dependencias funcionales**: consume el banco de preguntas (`question-bank`) para validar y seleccionar preguntas existentes/activas por categoría y dificultad; requiere autenticación/autorización con rol `Admin` (`authentication`); se apoya en la base arquitectónica de repositorios y entidades auditables (`project-architecture`).
- **Persistencia**: nuevas tablas `Exams` y `ExamQuestions` en `AppDbContext`, con relación muchos-a-muchos ordenada entre exámenes y preguntas.
- **Consumidores posteriores**: sienta la base de datos y de servicio sobre la que se apoyarán capacidades futuras de envío de exámenes a candidatos y de resolución/resultados.
