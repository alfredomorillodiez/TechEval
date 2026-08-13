## 1. Dominio
- [x] 1.1 Crear la entidad `ExamResult` (`ExamSessionId`, `ExamId`, `CandidateName`, `CandidateEmail`, `TotalPoints`, `ObtainedPoints`, `ScorePercentage`, `Passed`, `CompletedAt`) con navegación a `ExamSession` y `Exam`.
- [x] 1.2 Definir la interfaz `IExamResultRepository` en `TechEval.Domain.Interfaces.Repositories` con métodos de consulta especializados (`GetByExamAsync`, `GetByCandidateEmailAsync`, `GetWithDetailsAsync`, `GetAllWithDetailsAsync`).

## 2. Corrección automática al enviar el examen
- [x] 2.1 Implementar en `ExamTokenService.SubmitExamAsync` el recorrido de las `ExamQuestions` del examen, comparando la respuesta seleccionada contra la respuesta correcta para preguntas `MultipleChoice`.
- [x] 2.2 Marcar `UserAnswer.IsCorrect = null` para preguntas `OpenQuestion`, dejándolas fuera del cálculo automático de puntos.
- [x] 2.3 Acumular `TotalPoints` y `ObtainedPoints` durante el recorrido y calcular `ScorePercentage` redondeado a 2 decimales.
- [x] 2.4 Determinar `Passed` comparando `ScorePercentage` contra `Exam.PassingScorePercentage`.
- [x] 2.5 Persistir el `ExamResult` y marcar la `ExamSession` como `Completed` con su `CompletedAt`.
- [x] 2.6 Invocar `IEmailService.SendExamResultAsync` con el porcentaje y el estado de aprobación tras persistir el resultado.

## 3. Persistencia
- [x] 3.1 Implementar `ExamResultRepository` sobre `BaseRepository<ExamResult>` con `Include` de `Exam`, `ExamSession`, `UserAnswers`, `Question`, `Answers` y `SelectedAnswer` para las consultas de detalle.
- [x] 3.2 Registrar `IExamResultRepository` en `DependencyInjection.AddInfrastructure`.

## 4. Servicio de consulta y DTOs
- [x] 4.1 Definir los DTOs `ExamResultDto`, `AnswerReviewDto`, `ExamResultSummaryDto` y `DashboardStatsDto`.
- [x] 4.2 Implementar `ResultService.GetAllAsync` y `GetByExamAsync` mapeando `ExamResult` a `ExamResultSummaryDto`.
- [x] 4.3 Implementar `ResultService.GetDetailAsync` deduplicando `UserAnswers` por pregunta y construyendo la revisión de respuestas (`AnswerReviewDto`) con respuesta seleccionada/abierta, respuesta correcta y puntos.
- [x] 4.4 Implementar `ResultService.GetDashboardStatsAsync` con conteos de exámenes/preguntas activos, resultados del mes en curso, promedio de puntuación y tasa de aprobación del mes, y los 10 resultados más recientes.

## 5. API
- [x] 5.1 Crear `ResultsController` con autorización `[Authorize(Roles = "Admin")]` y endpoints `GET /api/results`, `GET /api/results/exam/{examId}`, `GET /api/results/{id}` y `GET /api/results/dashboard`.
- [x] 5.2 Devolver `404 NotFound` en `GET /api/results/{id}` cuando el resultado no existe.
