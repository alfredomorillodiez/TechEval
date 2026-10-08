## 1. Datos: flag por categoría

- [x] 1.1 Añadir `AllowsAiGeneration` (bool, default `true`) a `Category` (src/TechEval.Domain/Entities/Category.cs)
- [x] 1.2 Actualizar `CategoryConfiguration` (src/TechEval.Infrastructure/Data/Configurations/QuestionConfiguration.cs) con el default de columna
- [x] 1.3 Crear script SQL no destructivo (`scripts/add_category_ai_generation_flag.sql`, mismo patrón que `scripts/add_user_link_columns.sql`) que añade la columna a `Categories` con default `1` y marca `AllowsAiGeneration = 0` para la categoría `iECS`
- [x] 1.4 Añadir la columna también en `scripts/create_database.sql` (tabla `Categories`) — hecho con el default de columna; **no** se añadió un `UPDATE` marcando `iECS` en la sección de seed porque ese script nunca sembró `iECS` (se creó después, directo por API/BD) — no hay fila que actualizar ahí

## 2. Cambio de modelo

- [x] 2.1 Cambiar el valor por defecto de `OllamaSettings.Model` (src/TechEval.Infrastructure/Ai/OllamaSettings.cs) a `qwen2.5-coder:14b`
- [x] 2.2 Actualizar `Ollama:Model` en `appsettings.json`/`appsettings.Development.json`
- [x] 2.3 Confirmar que `qwen2.5-coder:14b` está descargado en cualquier instancia de Ollama donde se despliegue — ya descargado y probado en desarrollo local (9.0GB, 8 pruebas exitosas en la sesión de exploración)

## 3. Simplificar el pipeline de generación (quitar crítica)

- [x] 3.1 Eliminar `CritiqueAsync`, `BuildCritiquePrompt` y `CritiqueVerdict` de `OllamaQuestionGenerationService.cs`
- [x] 3.2 Simplificar `GenerateQuestionAsync` para que devuelva el resultado de `ParseAndValidate` directamente, sin la segunda llamada de crítica
- [x] 3.3 Quitar de `BuildPrompt` la frase "Puedes razonar brevemente antes de responder"; se conservó persona, criterios de distractores distintos y la instrucción anti-comentarios (`JsonPurityInstruction`)
- [x] 3.4 `ExtractJsonPayload`/`StripLineComments` se dejaron sin cambios (parseo tolerante a `<think>` y comentarios) — inocuo para el modelo actual, sigue siendo válido si se cambia de modelo en el futuro

## 4. Restricción de categorías no aptas

- [x] 4.1 Añadir `AllowsAiGeneration` a `CategoryDto` (src/TechEval.Application/DTOs/CategoryDto.cs) y a su mapeo en `CategoryService`
- [x] 4.2 En `QuestionGenerationService.CreateJobAsync`, tras obtener `category`, rechaza con `InvalidOperationException` si `!category.AllowsAiGeneration`
- [x] 4.3 En `GenerateQuestions.razor`, se filtra `_categories` para excluir las que tengan `AllowsAiGeneration = false` antes de mostrarlas en el selector
- [x] 4.4 Confirmado: `QuestionForm.razor` (creación/edición manual) sigue mostrando todas las categorías sin filtrar

## 5. Validación

- [x] 5.1 Ejecutado el script de datos contra la BD de desarrollo: `iECS` quedó con `AllowsAiGeneration = 0`, las demás 8 categorías en `1`
- [x] 5.2 Verificado visualmente (Playwright): el selector de `GenerateQuestions.razor` lista 8 categorías, sin `iECS`
- [x] 5.3 Verificado contra la API real: `POST /api/question-generation/jobs` con `CategoryId` de `iECS` responde `400` con mensaje claro, y no se creó ningún `QuestionGenerationJob` para esa categoría (confirmado por consulta directa a la BD)
- [x] 5.4 Verificado visualmente (Playwright): el formulario manual de preguntas (`/admin/questions/new`) sigue listando las 9 categorías, incluyendo `iECS`
- [x] 5.5 Corrido un job real de generación (C#/LINQ, categoría distinta de `iECS`) de principio a fin a través de la app completa: usó `qwen2.5-coder:14b`, una sola llamada al modelo (sin etapa de crítica), y el resultado quedó en `QuestionReviewStatus = PendingReview` como antes — pregunta con distractores claramente distintos entre sí y respuesta correcta acertada
- [x] 5.6 Confirmado: los 8 tests existentes (`dotnet test`) siguen pasando tras quitar la crítica
