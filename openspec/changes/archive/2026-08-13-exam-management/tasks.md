## 1. Entidades de dominio
- [x] 1.1 Crear entidad `Exam` (`AuditableEntity`) con título, descripción, tiempo límite, porcentaje de aprobación, estado activo, usuario creador y propiedad calculada `TotalPoints`
- [x] 1.2 Crear entidad `ExamQuestion` como asociación ordenada entre `Exam` y `Question` (`ExamId`, `QuestionId`, `Order`)

## 2. Persistencia
- [x] 2.1 Definir `IExamRepository` con `GetWithQuestionsAsync` (detalle con preguntas, respuestas, categoría y usuario creador) y `GetWithStatsAsync` (listado con tokens y resultados)
- [x] 2.2 Implementar `ExamRepository` sobre `BaseRepository<Exam>` usando `Include`/`ThenInclude` de EF Core para los dos escenarios de consulta
- [x] 2.3 Registrar las tablas `Exams` y `ExamQuestions` en `AppDbContext`

## 3. DTOs
- [x] 3.1 Definir `CreateExamDto` (título, descripción, tiempo límite, porcentaje de aprobación, `QuestionIds`)
- [x] 3.2 Definir `GenerateExamDto` (título, descripción, tiempo límite, porcentaje de aprobación, `QuestionCount`, `CategoryIds` opcional, `Difficulty` opcional)
- [x] 3.3 Definir `UpdateExamDto`, `ExamDto`, `ExamQuestionDto`, `ExamSummaryDto`, `AdminAnswerOptionDto`

## 4. Servicio de creación manual
- [x] 4.1 Implementar `ExamService.CreateAsync`: validar que todas las `QuestionIds` existan y estén activas, asignar `Order` secuencial según el orden recibido y persistir el examen

## 5. Servicio de generación automática
- [x] 5.1 Implementar `IQuestionRepository.GetRandomAsync` (selección aleatoria filtrable por categorías y dificultad, sólo preguntas activas)
- [x] 5.2 Implementar `ExamService.GenerateAsync`: obtener preguntas aleatorias, validar cantidad suficiente y lanzar `InvalidOperationException` con el conteo encontrado vs. solicitado si no alcanza
- [x] 5.3 Asignar `Order` secuencial a las preguntas seleccionadas aleatoriamente

## 6. Gestión del ciclo de vida del examen
- [x] 6.1 Implementar `ExamService.UpdateAsync` (título, descripción, tiempo límite, porcentaje de aprobación, estado activo)
- [x] 6.2 Implementar `ExamService.DeleteAsync` como soft delete (`IsActive = false` + `UpdatedAt`)
- [x] 6.3 Implementar `ExamService.GetAllAsync` (mapeo a `ExamSummaryDto` con estadísticas de tokens/resultados) y `GetByIdAsync` (mapeo a `ExamDto` completo)

## 7. Endpoints API
- [x] 7.1 Implementar `ExamsController` con `[Authorize(Roles = "Admin")]`: `GET /api/exams`, `GET /api/exams/{id}`, `POST /api/exams`, `POST /api/exams/generate`, `PUT /api/exams/{id}`, `DELETE /api/exams/{id}`

## 8. Pruebas
- [x] 8.1 Test unitario: `GenerateAsync` lanza excepción cuando no hay suficientes preguntas disponibles
- [x] 8.2 Test unitario: `CreateAsync` crea el examen correctamente cuando todas las preguntas existen y están activas
