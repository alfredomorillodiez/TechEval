## Why
Una vez que un candidato envía sus respuestas, el equipo de reclutamiento necesita saber de inmediato si aprobó y con qué puntuación, sin tener que corregir manualmente cada pregunta de opción múltiple. Sin una corrección automática y un lugar centralizado donde consultar resultados, cada examen enviado generaría trabajo manual repetitivo y retrasaría la retroalimentación al candidato y a los reclutadores. TechEval necesita calcular la puntuación en el momento del envío, dejar claramente marcadas las preguntas abiertas que requieren revisión humana, y ofrecer a los administradores un panel con estadísticas y el detalle de cada intento.

## What Changes
- Al enviar un examen (`SubmitExamAsync`), corregir automáticamente cada pregunta de tipo test (`MultipleChoice`) comparando la respuesta seleccionada contra la respuesta marcada como correcta, y acumular los puntos de las preguntas correctas.
- Dejar las preguntas de tipo abierto (`OpenQuestion`) sin evaluar automáticamente (`IsCorrect = null`) para que un revisor humano las corrija manualmente más adelante.
- Calcular el porcentaje de acierto (`ScorePercentage = ObtenidoPuntos / TotalPuntos * 100`, redondeado a 2 decimales) y determinar si el candidato aprobó (`Passed`) comparando ese porcentaje contra el umbral `PassingScorePercentage` configurado en el examen.
- Persistir el resultado en la entidad `ExamResult`, vinculada a la sesión (`ExamSession`) y al examen (`Exam`), con el nombre y correo del candidato, puntos totales, puntos obtenidos, porcentaje y fecha de finalización.
- Enviar automáticamente un correo con el resultado (`SendExamResultAsync`) al candidato al completar el examen.
- Exponer a los administradores un dashboard de estadísticas (exámenes activos, preguntas activas, resultados del mes, promedio de puntuación del mes y tasa de aprobación del mes, junto con los últimos resultados).
- Permitir consultar el listado completo de resultados, filtrar por examen y ver el detalle de un resultado individual con la revisión pregunta por pregunta (respuesta seleccionada/abierta, respuesta correcta, si fue acertada y puntos).

## Capabilities
### New Capabilities
- `exam-results`: Corrección automática de preguntas de opción múltiple al enviar un examen, cálculo de puntuación y porcentaje, determinación de aprobado/no aprobado según el umbral del examen, preguntas abiertas marcadas como pendientes de corrección manual, notificación por email del resultado, y consulta administrativa mediante dashboard de estadísticas, listado por examen y detalle con revisión de respuestas.

### Modified Capabilities
(ninguna)

## Impact
- **Código afectado**: `src/TechEval.Domain/Entities/ExamResult.cs`, `src/TechEval.Application/Services/ExamTokenService.cs` (método `SubmitExamAsync`, lógica de corrección), `src/TechEval.Application/Services/ResultService.cs`, `src/TechEval.Application/DTOs/ResultDto.cs`, `src/TechEval.Infrastructure/Repositories/ExamResultRepository.cs`, `src/TechEval.API/Controllers/ResultsController.cs`.
- **Dependencias previas**: `project-architecture` (repositorio genérico, DI, manejo de errores), `authentication` (rol `Admin` sobre `ResultsController`), `question-bank` (tipos de pregunta, respuestas correctas, puntos), `exam-management` (umbral `PassingScorePercentage` del examen), `exam-delivery` (envío de examen mediante token, `ExamToken`, `SendExamResultAsync`), `exam-taking` (sesión pública, `ExamSession`, `UserAnswer`).
- **Sistemas externos**: servicio de email (notificación de resultado al candidato).
- **Base de datos**: tabla `ExamResults` (relación con `ExamSession` y `Exam`).
