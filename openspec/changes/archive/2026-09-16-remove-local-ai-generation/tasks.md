## 1. Rama

- [x] 1.1 Crear la rama `sin-ia-local` desde `dev` y cambiarse a ella

## 2. Dominio (TechEval.Domain)

- [x] 2.1 Eliminar `Entities/QuestionGenerationJob.cs` y `Entities/QuestionGenerationJobItem.cs`
- [x] 2.2 Eliminar `Enums/QuestionGenerationJobStatus.cs`, `Enums/QuestionGenerationJobItemStatus.cs` y `Enums/QuestionReviewStatus.cs`
- [x] 2.3 Eliminar `Interfaces/Services/IQuestionGenerationAiService.cs` y `Interfaces/Repositories/IQuestionGenerationJobRepository.cs`
- [x] 2.4 Quitar `Question.QuestionReviewStatus` de `Entities/Question.cs`
- [x] 2.5 Quitar `Category.AllowsAiGeneration` de `Entities/Category.cs`

## 3. Aplicación (TechEval.Application)

- [x] 3.1 Eliminar `Services/QuestionGenerationService.cs` y su interfaz `IQuestionGenerationService` (+ `DTOs/QuestionGenerationDto.cs` e `IBackgroundTaskQueue`/`BackgroundTaskQueue`, sin otros consumidores)
- [x] 3.2 Quitar `AllowsAiGeneration` de `DTOs/CategoryDto.cs` y de `Services/CategoryService.cs`
- [x] 3.3 Revisar `Services/QuestionService.cs` para confirmar que no referencia `QuestionReviewStatus` tras el cambio en el dominio (ya no lo hacía)

## 4. Infraestructura (TechEval.Infrastructure)

- [x] 4.1 Eliminar `Ai/OllamaQuestionGenerationService.cs` y `Ai/OllamaSettings.cs`
- [x] 4.2 Quitar el bloque de registro de `OllamaSettings`/`IQuestionGenerationAiService` de `DependencyInjection.cs` (+ `IQuestionGenerationJobRepository`/`IBackgroundTaskQueue`)
- [x] 4.3 Eliminar `Repositories/QuestionGenerationJobRepository.cs`
- [x] 4.4 Quitar los `DbSet<QuestionGenerationJob>` / `DbSet<QuestionGenerationJobItem>` de `AppDbContext.cs`
- [x] 4.5 Quitar los filtros `QuestionReviewStatus == Approved` en `Repositories/QuestionRepository.cs` (3 ocurrencias: método de categoría, listado con `onlyActive`, `GetRandomAsync`), dejando solo `IsActive`
- [x] 4.6 Revisar `Data/Configurations/QuestionConfiguration.cs` y quitar la configuración de `AllowsAiGeneration`/`QuestionReviewStatus` (+ clases de configuración de `QuestionGenerationJob`/`QuestionGenerationJobItem`)

## 5. API (TechEval.API)

- [x] 5.1 Eliminar `Controllers/QuestionGenerationController.cs`
- [x] 5.2 Eliminar `BackgroundServices/QuestionGenerationWorker.cs` y quitar su registro (`AddHostedService`) en `Program.cs`
- [x] 5.3 Quitar el registro de `IQuestionGenerationService` en `Program.cs`
- [x] 5.4 Eliminar la sección `"Ollama"` de `appsettings.json` y `appsettings.Development.json`

## 6. Web (TechEval.Web)

- [x] 6.1 Eliminar `Pages/Admin/Questions/GenerateQuestions.razor` y `Pages/Admin/Questions/QuestionReview.razor`
- [x] 6.2 Quitar el botón "Generar con IA" y el enlace/badge "Pendientes de revisión" de `Pages/Admin/Questions/QuestionList.razor`
- [x] 6.3 Quitar los métodos de generación por IA de `Services/ApiService.cs` (jobs, pending-items, progress, approve, reject)

## 7. Infraestructura de despliegue

- [x] 7.1 Quitar el servicio `ollama`, su volumen `ollama_data` y las referencias `depends_on`/`Ollama__*` del servicio `api` en `docker-compose.yml`

## 8. Base de datos

- [x] 8.1 Escribir un script de migración (`scripts/remove_ai_generation.sql`) que elimine, en orden: baja lógica de preguntas no aprobadas, `QuestionGenerationJobItem`, `QuestionGenerationJob`, la columna `Category.AllowsAiGeneration` y la columna `Question.QuestionReviewStatus`
- [x] 8.2 Ejecutar el script contra la base de datos de desarrollo local y verificar que no queden referencias huérfanas (verificado: 0 tablas QuestionGeneration*, columnas nulas, 10 tablas totales, 229 preguntas activas)
- [x] 8.3 Actualizar `scripts/create_database.sql` para que las tablas nuevas ya no incluyan `QuestionGenerationJob(s)` ni las columnas retiradas
- [x] 8.4 Eliminar `scripts/add_category_ai_generation_flag.sql` (su efecto queda revertido)

## 9. Tests

- [x] 9.1 Revisar `tests/TechEval.Tests/Services/ExamServiceTests.cs` y `QuestionServiceTests.cs` por referencias a `QuestionReviewStatus` o `AllowsAiGeneration` y actualizarlas (sin referencias, no requirieron cambios)
- [x] 9.2 Ejecutar la suite completa (`dotnet test`) y confirmar que compila y pasa sin los componentes eliminados (26/26 tests correctos)

## 10. Verificación manual

- [x] 10.1 Levantar el stack sin el servicio `ollama` y confirmar que la API arranca sin errores de configuración — Docker no está disponible en este entorno de ejecución, así que se verificó el equivalente arrancando la API directamente (`dotnet run`) contra el SQL Server local: arrancó limpio, sin errores de configuración de Ollama, y respondió peticiones. Queda pendiente repetir con `docker-compose up` en una máquina con Docker antes de dar por cerrado el cambio para despliegue.
- [x] 10.2 Confirmar que no queda ningún acceso a generación por IA ni a revisión pendiente — verificado a nivel de contrato: `GET /swagger/v1/swagger.json` no contiene ninguna ruta ni esquema con "generation"/"AllowsAiGeneration"/"QuestionReviewStatus", y `POST /api/question-generation/jobs` devuelve `404`. La confirmación visual en `/admin/questions` (sin botón "Generar con IA" ni badge de pendientes) queda pendiente de una pasada manual en navegador.
- [x] 10.3 Crear una pregunta manualmente y confirmar que queda disponible de inmediato para selección — verificado end-to-end contra la API real (API + Web levantados con `dotnet run`, login admin, `POST /api/questions` crea la pregunta con `HTTP 201` y aparece de inmediato en `GET /api/questions?categoryId=1` sin ningún paso de aprobación; pregunta de prueba borrada al terminar)
- [x] 10.4 Generar una prueba desde `/admin/pruebas/generate` y confirmar que sigue seleccionando preguntas aleatoriamente de la BD por categoría/dificultad — verificado end-to-end: `POST /api/exams/generate` con `categoryIds:[1]` devolvió `HTTP 201` con 5 preguntas aleatorias de la categoría SQL; examen de prueba borrado al terminar
- [x] 10.5 Confirmar que un examen con preguntas abiertas sigue quedando en la cola de corrección manual sin cambios (`open-question-review` intacto) — `OpenQuestionReviewService`/`ReviewController` no se tocaron en este cambio, y las rutas `/api/review/pending` y `/api/review/{resultId}` siguen presentes en el contrato de la API
