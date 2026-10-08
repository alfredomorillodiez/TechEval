## Why

TechEval depende hoy de un modelo de IA local (Ollama) para generar preguntas nuevas del banco. Se decide dejar de depender de un modelo de IA local: el banco de preguntas se mantendrá exclusivamente mediante alta manual, y los exámenes se seguirán generando por selección aleatoria de preguntas ya existentes en BD (capacidad que ya existe hoy y es independiente de la IA).

## What Changes

- **BREAKING**: Se elimina por completo la capacidad de generar preguntas mediante IA (Ollama): endpoints, servicio en segundo plano, entidades `QuestionGenerationJob`/`QuestionGenerationJobItem`, pantallas de generación y de revisión de preguntas generadas.
- **BREAKING**: Se elimina el servicio `ollama` de `docker-compose.yml` (imagen, volumen, `depends_on`, variables de entorno `Ollama__*`) y la sección `"Ollama"` de `appsettings.json` / `appsettings.Development.json`.
- **BREAKING**: Se elimina el flag `Category.AllowsAiGeneration` (entidad, DTO, columna de BD y script de seed asociado), ya que solo se usaba para gobernar la generación por IA.
- **BREAKING**: Se elimina el campo `Question.QuestionReviewStatus` (y su enum), ya que sin generación por IA toda pregunta manual nace y permanece en `Approved`; los filtros de selección de preguntas (`QuestionRepository`) dejan de comprobar este campo y se apoyan únicamente en `IsActive`.
- El botón "Generar con IA" y el acceso a "Pendientes de revisión" desaparecen de `/admin/questions` (`QuestionList.razor`). El alta de preguntas queda exclusivamente por el flujo manual ya existente (`QuestionForm.razor`, `/admin/questions/new` y `/admin/questions/{id}`).
- No se modifica la generación de exámenes (`/admin/pruebas/generate`, `ExamService.GenerateAsync`, `QuestionRepository.GetRandomAsync`): ya selecciona preguntas aleatoriamente de la BD por categoría/dificultad y sigue funcionando igual, salvo que el filtro sobre `QuestionReviewStatus` se retira.
- No se modifica la revisión de preguntas abiertas de examen (`open-question-review`): las respuestas abiertas de un examen siguen quedando pendientes de corrección manual del profesor, sin ningún cambio en ese flujo. Es un concepto distinto de la "revisión de preguntas generadas por IA" que aquí se elimina.
- Se elimina cualquier prueba automatizada que referencie los componentes retirados (no se encontraron pruebas unitarias directas sobre `OllamaQuestionGenerationService` ni `QuestionGenerationService`, pero se revisará `ExamServiceTests.cs` para confirmar que no dependa de `QuestionReviewStatus`).

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities
- `ai-question-generation`: se elimina la capacidad completa (todos sus requisitos dejan de existir; el spec se retira del árbol de specs activo al archivar este cambio).
- `admin-console`: el requisito "Gestión del banco de preguntas desde la interfaz" deja de ofrecer acceso a generación por IA ni a bandeja de revisión; solo cubre alta/edición/listado manual.
- `question-bank`: el requisito "Filtrado de preguntas del banco" deja de filtrar por `QuestionReviewStatus` (se retira el escenario de exclusión de pendientes/rechazadas, ya inalcanzable sin generación por IA) y pasa a depender solo de `IsActive`; el requisito "Visualización de fecha y hora..." deja de mencionar preguntas "generadas por IA" como origen posible.

Nota: `deployment-ops` no tiene ningún requisito que mencione el servicio `ollama` (se revisó su spec y no aparece), por lo que la baja de ese servicio en `docker-compose.yml` es un cambio de implementación sin delta de spec asociado.

## Impact

- **Dominio**: `Category.cs` (quita `AllowsAiGeneration`), `Question.cs` (quita `QuestionReviewStatus`), se eliminan `QuestionGenerationJob.cs`, `QuestionGenerationJobItem.cs`, `QuestionGenerationJobStatus.cs`, `QuestionGenerationJobItemStatus.cs`, `QuestionReviewStatus.cs`, `IQuestionGenerationAiService.cs`, `IQuestionGenerationJobRepository.cs`.
- **Aplicación**: se elimina `QuestionGenerationService.cs`/`IQuestionGenerationService`; se revisa `CategoryService.cs`/`CategoryDto.cs` para quitar el flag.
- **Infraestructura**: se eliminan `OllamaQuestionGenerationService.cs`, `OllamaSettings.cs`, el bloque de registro DI en `DependencyInjection.cs`, `QuestionGenerationJobRepository.cs`; se actualiza `QuestionConfiguration.cs` y `AppDbContext.cs` (quita DbSets); se actualiza `QuestionRepository.cs` (quita filtros por `QuestionReviewStatus`).
- **API**: se elimina `QuestionGenerationController.cs`, el registro del hosted service `QuestionGenerationWorker` y su propio archivo en `Program.cs`; se elimina la sección `Ollama` de `appsettings*.json`.
- **Web**: se eliminan `GenerateQuestions.razor`, `QuestionReview.razor`; se edita `QuestionList.razor` (quita botón y badge) y `ApiService.cs` (quita métodos cliente de generación).
- **BD**: script de migración para eliminar tablas `QuestionGenerationJob`/`QuestionGenerationJobItem`, columna `Category.AllowsAiGeneration` y columna `Question.QuestionReviewStatus`; se actualiza `scripts/create_database.sql` y se elimina `scripts/add_category_ai_generation_flag.sql` (o se documenta como histórico ya no aplicable).
- **Infraestructura de despliegue**: `docker-compose.yml` pierde el servicio `ollama`, su volumen y las referencias desde `api`.
- **Rama**: todo el trabajo se realiza en una rama nueva `sin-ia-local` creada desde `dev`; no se toca `main`.
