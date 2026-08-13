## 1. Modelo de dominio
- [x] 1.1 Crear la entidad `Category` (`AuditableEntity`, `Name`, `Description`, `IsActive`, colección `Questions`)
- [x] 1.2 Crear la entidad `Question` (`AuditableEntity`, `Text`, `Type`, `Difficulty`, `CategoryId`, `Points`, `IsActive`, `SampleAnswer`, colecciones `Answers` y `ExamQuestions`)
- [x] 1.3 Crear la entidad `Answer` (`QuestionId`, `Text`, `IsCorrect`, `Order`)
- [x] 1.4 Definir los enums `QuestionType` (`MultipleChoice`, `OpenEnded`) y `DifficultyLevel` (`Basic`, `Intermediate`, `Advanced`)

## 2. Configuración de persistencia (EF Core)
- [x] 2.1 Configurar `CategoryConfiguration` con índice único en `Name` y longitudes máximas de `Name`/`Description`
- [x] 2.2 Configurar `QuestionConfiguration` con longitudes máximas de `Text`/`SampleAnswer`, valor por defecto de `Points`, e índices en `CategoryId`, `Difficulty` e `IsActive`
- [x] 2.3 Configurar `AnswerConfiguration` con longitud máxima de `Text`
- [x] 2.4 Configurar relación `Question` → `Category` con `DeleteBehavior.Restrict` y `Question` → `Answer` con `DeleteBehavior.Cascade`

## 3. Repositorios
- [x] 3.1 Definir `IQuestionRepository` extendiendo `IRepository<Question>` con métodos específicos del dominio
- [x] 3.2 Implementar `QuestionRepository.GetWithAnswersAsync` (detalle con categoría y respuestas)
- [x] 3.3 Implementar `QuestionRepository.GetFilteredAsync` (filtro combinable por categoría/dificultad/tipo, solo activas por defecto)
- [x] 3.4 Implementar `QuestionRepository.GetByCategoryAsync` y `GetRandomAsync` como soporte para capacidades futuras (selección de preguntas para exámenes)

## 4. DTOs
- [x] 4.1 Definir `CategoryDto`, `CreateCategoryDto`, `UpdateCategoryDto`
- [x] 4.2 Definir `AnswerDto`, `CreateAnswerDto`
- [x] 4.3 Definir `QuestionDto` (detalle completo), `QuestionSummaryDto` (listado), `CreateQuestionDto`, `UpdateQuestionDto`

## 5. Servicios de aplicación
- [x] 5.1 Implementar `CategoryService` (alta, listado con conteo de preguntas activas, actualización, baja lógica)
- [x] 5.2 Implementar `QuestionService` (alta, detalle, listado filtrado, actualización, baja lógica)
- [x] 5.3 Implementar `ValidateAnswers` para exigir exactamente 4 opciones y exactamente 1 correcta en preguntas tipo test
- [x] 5.4 Implementar reemplazo completo de respuestas en `UpdateAsync` respetando el `Order` de cada opción

## 6. Endpoints de la API
- [x] 6.1 Implementar `CategoriesController` (`GET` anónimo, `POST`/`PUT`/`DELETE` restringidos a rol `Admin`)
- [x] 6.2 Implementar `QuestionsController` con filtros por query string (`categoryId`, `difficulty`, `type`) y CRUD restringido a rol `Admin`
- [x] 6.3 Documentar los endpoints con comentarios XML para Swagger

## 7. Pruebas
- [x] 7.1 Pruebas de creación válida de pregunta tipo test (4 respuestas, 1 correcta)
- [x] 7.2 Pruebas de rechazo por número incorrecto de respuestas y por ausencia de respuesta correcta
- [x] 7.3 Pruebas de obtención de pregunta existente/inexistente por `Id`
- [x] 7.4 Pruebas de baja lógica de pregunta (verifica `IsActive = false` sin eliminar el registro)
