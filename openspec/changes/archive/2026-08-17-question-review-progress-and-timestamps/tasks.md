## 1. Backend: progreso agregado de generación por job

- [x] 1.1 Añadir `QuestionGenerationJobProgressDto` (`JobId`, `Topic`, `CategoryId`, `CategoryName`, `Difficulty`, `Type`, `Status` del job, `RequestedCount`, `PendingCount`, `SucceededCount`, `FailedCount`, `CreatedAt`) en `QuestionGenerationDto.cs`
- [x] 1.2 Añadir `GetActiveJobsProgressAsync` a `QuestionGenerationJobRepository` (y su interfaz): agrupar `QuestionGenerationJobItem` por `JobId`, contar por `Status`, incluyendo solo jobs con al menos un ítem `Pending`, o `Succeeded` con `Question.QuestionReviewStatus = PendingReview`, o `Failed`
- [x] 1.3 Añadir `GetActiveJobsProgressAsync` a `IQuestionGenerationService`/`QuestionGenerationService`, mapeando a `QuestionGenerationJobProgressDto`
- [x] 1.4 Añadir endpoint `GET api/question-generation/jobs/progress` en `QuestionGenerationController` (mismo `[Authorize(Roles = "Admin")]` que el resto del controller)

## 2. Backend: timestamps de preguntas

- [x] 2.1 Añadir `CreatedAt` (`DateTime`) y `UpdatedAt` (`DateTime?`) a `QuestionDto`
- [x] 2.2 Mapear ambos campos en `QuestionGenerationService.MapQuestionToDto`
- [x] 2.3 Mapear ambos campos en el servicio de preguntas usado por `QuestionForm.razor`/`QuestionList.razor` (el que respalda `GetQuestionAsync`/`GetQuestionsAsync`)
- [x] 2.4 Añadir `JobId` y `Topic` del job de origen a `QuestionGenerationJobItemDto` si no viajan ya de forma legible en la UI (verificar mapeo actual antes de duplicar)

## 3. Frontend: cliente API

- [x] 3.1 Añadir `GetActiveGenerationProgressAsync()` a `ApiService.cs` (`GET api/question-generation/jobs/progress`)

## 4. Frontend: indicador de progreso en la bandeja de revisión

- [x] 4.1 En `QuestionReview.razor`, cargar el progreso agregado junto con los ítems pendientes en `OnInitializedAsync`
- [x] 4.2 Renderizar el indicador de estado agrupado por job: tema, categoría, estado del job (`Running`/`Completed`/`Failed`), y contadores `Succeeded`/`Failed`/`Pending` sobre `RequestedCount`
- [x] 4.3 Mostrar, junto a cada ítem de la lista existente, el `Topic`/identificador del job de origen
- [x] 4.4 Implementar polling con `PeriodicTimer` (cada pocos segundos) que refresque tanto el progreso agregado como la lista de ítems pendientes de revisión
- [x] 4.5 Detener el `PeriodicTimer` al destruir el componente (`IAsyncDisposable`/`Dispose`) para no dejar polling huérfano
- [x] 4.6 Mostrar estado vacío ("no hay generación en curso ni preguntas pendientes de revisión") cuando no haya jobs activos ni ítems pendientes

## 5. Frontend: timestamps en el formulario de edición

- [x] 5.1 En `QuestionForm.razor`, mostrar `CreatedAt` como campo de solo lectura, formateado con minutos y etiquetado "UTC" (p. ej. `yyyy-MM-dd HH:mm`)
- [x] 5.2 Mostrar `UpdatedAt` de la misma forma solo cuando tenga valor (pregunta editada tras su creación); ocultarlo si es `null`

## 6. Verificación

- [x] 6.1 Probar manualmente: lanzar un job de generación, verificar que el indicador de la bandeja de revisión muestra el progreso incremental sin recargar la página
- [x] 6.2 Probar manualmente: abrir una pregunta manual (sin job asociado) y una generada por IA, verificar que ambas muestran `CreatedAt` correctamente y `UpdatedAt` solo cuando corresponde
- [x] 6.3 Confirmar que el polling se detiene al navegar fuera de `/admin/questions/review` (sin llamadas de red residuales)
