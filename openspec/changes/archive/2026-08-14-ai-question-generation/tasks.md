## 1. Dominio

- [x] 1.1 Añadir enum `QuestionReviewStatus` (`Approved`, `PendingReview`, `Rejected`) en `TechEval.Domain.Enums`
- [x] 1.2 Añadir propiedad `QuestionReviewStatus` a `Question` (default `Approved`)
- [x] 1.3 Crear entidad `QuestionGenerationJob` (Id, CategoryId, Difficulty, Type, Topic, RequestedCount, Status, CreatedAt, CompletedAt, CreatedByUserId) con enum `QuestionGenerationJobStatus` (`Queued`, `Running`, `Completed`, `Failed`)
- [x] 1.4 Crear entidad `QuestionGenerationJobItem` (Id, JobId, Status, QuestionId nullable, ErrorMessage nullable) con enum `QuestionGenerationJobItemStatus` (`Pending`, `Succeeded`, `Failed`)
- [x] 1.5 Crear interfaces `IQuestionGenerationJobRepository` e `IQuestionGenerationAiService` en `TechEval.Domain.Interfaces`

## 2. Infraestructura — persistencia

- [x] 2.1 Registrar `QuestionGenerationJob` y `QuestionGenerationJobItem` en `AppDbContext` con sus relaciones (`Job` 1—N `Items`, `Item` 0..1—1 `Question`)
- [x] 2.2 Actualizar `scripts/create_database.sql`: añadir columna `QuestionReviewStatus` a `Questions` y las tablas `QuestionGenerationJobs`/`QuestionGenerationJobItems`, respetando el orden de dependencias de claves foráneas ya usado en el script (sin introducir migraciones de EF Core, siguiendo la convención actual del proyecto)
- [x] 2.3 Implementar `QuestionGenerationJobRepository`

## 3. Infraestructura — integración con Ollama

- [x] 3.1 Añadir `OllamaSettings` (`BaseUrl`, `Model`) y su sección en `appsettings.json`/`appsettings.Development.json` (`http://ollama:11434` en Docker, override a `http://localhost:11434` en desarrollo local)
- [x] 3.2 Implementar `OllamaQuestionGenerationService : IQuestionGenerationAiService` que llame a la API de Ollama pidiendo formato JSON estructurado, con un prompt de sistema que fije la forma exacta esperada (enunciado + 4 respuestas con una `IsCorrect` para `MultipleChoice`, o enunciado + `SampleAnswer` para `OpenEnded`)
- [x] 3.3 Implementar la validación de la respuesta del modelo (parseo JSON, conteo de respuestas, exactamente una correcta) devolviendo un resultado tipado de éxito/fallo con mensaje de error, sin lanzar excepciones sin controlar
- [x] 3.4 Registrar `OllamaQuestionGenerationService` y `OllamaSettings` en `DependencyInjection.cs` de `TechEval.Infrastructure`

## 4. Aplicación

- [x] 4.1 Crear DTOs: `CreateQuestionGenerationJobDto`, `QuestionGenerationJobDto`, `QuestionGenerationJobItemDto` (incluyendo el contenido de la pregunta cuando `Succeeded` y el `ErrorMessage` cuando `Failed`)
- [x] 4.2 Implementar `QuestionGenerationService` con: `CreateJobAsync` (valida categoría/cantidad, crea job + items `Pending`, encola), `GetPendingReviewItemsAsync`, `ApproveAsync`, `RejectAsync`
- [x] 4.3 Extender el filtrado existente de `GET /api/questions` (servicio/repositorio de `Question`) para excluir `QuestionReviewStatus != Approved` por defecto, reutilizado tanto por el listado del banco como por la generación manual/automática de pruebas

## 5. Background processing

- [x] 5.1 Implementar `IBackgroundTaskQueue` (envoltorio sobre `System.Threading.Channels.Channel<int>` con IDs de job)
- [x] 5.2 Implementar `QuestionGenerationWorker : BackgroundService` que desencola jobs, los marca `Running`, procesa sus ítems secuencialmente llamando a `IQuestionGenerationAiService`, y marca cada ítem `Succeeded`/`Failed` según corresponda; al terminar todos los ítems marca el job `Completed`
- [x] 5.3 Al arrancar el worker (`StartAsync`), marcar como `Failed` cualquier `QuestionGenerationJob` que haya quedado en `Running` de una ejecución anterior, junto con sus ítems `Pending`
- [x] 5.4 Registrar `IBackgroundTaskQueue` (singleton) y `QuestionGenerationWorker` (hosted service) en `TechEval.API`

## 6. API

- [x] 6.1 Crear `QuestionGenerationController` con `POST /api/question-generation/jobs` (crea y encola, responde `202 Accepted`), `GET /api/question-generation/pending-items` (bandeja de revisión), `POST /api/question-generation/items/{id}/approve`, `POST /api/question-generation/items/{id}/reject`
- [x] 6.2 Reutilizar `PUT /api/questions/{id}` existente para la edición de preguntas en `PendingReview` (confirmar que no exige `QuestionReviewStatus = Approved` para permitir la edición)
- [x] 6.3 Restringir los nuevos endpoints a usuarios `Admin`, igual que el resto de `/api/questions`

## 7. Frontend — generación

- [x] 7.1 Crear `GenerateQuestions.razor` en `@page "/admin/questions/generate"`: formulario con categoría, dificultad, tipo, tema (texto libre) y cantidad; al enviar, llama al nuevo endpoint y muestra confirmación de que el job quedó encolado (sin esperar a que termine)
- [x] 7.2 Añadir método `GenerateQuestionsAsync` a `ApiService.cs` para `POST /api/question-generation/jobs`

## 8. Frontend — bandeja de revisión

- [x] 8.1 Crear `QuestionReview.razor` en `@page "/admin/questions/review"`: lista los ítems pendientes (`GET /api/question-generation/pending-items`), mostrando el contenido de la pregunta para los exitosos y el mensaje de error para los fallidos, con acciones aprobar/rechazar/editar por fila
- [x] 8.2 La acción "Editar" de un ítem exitoso navega a `/admin/questions/{id}` (mismo formulario de edición existente, `QuestionForm.razor`) y vuelve a la bandeja al guardar
- [x] 8.3 Añadir métodos `GetPendingReviewItemsAsync`, `ApproveQuestionAsync`, `RejectQuestionAsync` a `ApiService.cs`

## 9. Frontend — accesos desde el listado de preguntas

- [x] 9.1 En `QuestionList.razor`, añadir botón "Generar con IA" (→ `/admin/questions/generate`) junto a los controles existentes
- [x] 9.2 En `QuestionList.razor`, añadir acceso "Pendientes de revisión" (→ `/admin/questions/review`) con el conteo de ítems pendientes

## 10. Infraestructura de despliegue

- [x] 10.1 Añadir servicio `ollama` a `docker-compose.yml` (imagen `ollama/ollama`, puerto `11434`, volumen persistente para modelos descargados)
- [x] 10.2 Configurar `api` en `docker-compose.yml` con `Ollama__BaseUrl=http://ollama:11434` y `Ollama__Model=deepseek-r1:7b`
- [x] 10.3 Documentar el paso manual de primera vez (`docker exec ollama ollama pull deepseek-r1:7b`) en el README o en `deployment-ops`

## 11. Verificación

- [x] 11.1 Probar el flujo completo localmente: generar un lote pequeño (2-3 preguntas) contra Ollama corriendo en `localhost:11434`, confirmar que aparecen en la bandeja de revisión con `PendingReview`
- [x] 11.2 Confirmar que una pregunta en `PendingReview` NO aparece en `GET /api/questions` ni es seleccionable al crear/generar una prueba
- [x] 11.3 Aprobar una pregunta generada y confirmar que pasa a estar disponible para pruebas
- [x] 11.4 Rechazar una pregunta generada y confirmar que desaparece de la bandeja y sigue sin ser seleccionable
- [x] 11.5 Forzar un fallo de formato (por ejemplo, pedir una cantidad alta o un tema ambiguo) y confirmar que el ítem fallido se muestra con su error sin tumbar el resto del lote
- [x] 11.6 Reiniciar la API con un job en `Running` y confirmar que se marca `Failed` al volver a arrancar
