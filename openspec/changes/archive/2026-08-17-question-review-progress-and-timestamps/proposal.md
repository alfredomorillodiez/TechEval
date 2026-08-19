## Why

En `/admin/questions/review`, un administrador no tiene forma de saber si hay generación de preguntas en curso, cuántas quedan por generar dentro de un lote, ni de qué job proviene cada pregunta mostrada — el spec de `ai-question-generation` ya exige esto último pero no está implementado. Además, ni la bandeja de revisión ni el formulario de edición de preguntas muestran cuándo se creó o modificó por última vez una pregunta, lo que dificulta auditar el origen y la vigencia del contenido, tanto generado por IA como creado manualmente.

## What Changes

- Nuevo endpoint que expone, por job, el progreso agregado de generación (`RequestedCount` vs. conteos de ítems `Pending`/`Succeeded`/`Failed`), incluyendo jobs aún `Running` cuyos ítems `Pending` hoy son invisibles para la UI.
- La ventana `/admin/questions/review` incorpora un indicador de estado siempre visible que muestra: jobs de generación en curso, preguntas restantes por generar, preguntas listas para revisar y preguntas fallidas — agrupado por job para poder identificar de qué lote proviene cada ítem (cierra el requisito ya existente y no implementado de "Bandeja de revisión de preguntas generadas").
- Este indicador se refresca automáticamente mediante polling (cada pocos segundos) mientras la página esté abierta, sin necesidad de recargar manualmente. Alcance limitado a esta página; no se añade indicador global en el sidebar/`MainLayout`.
- `QuestionDto` se extiende con `CreatedAt` y `UpdatedAt` (ya existentes en la entidad `Question` vía `AuditableEntity` pero no mapeados hoy).
- El formulario de edición de preguntas (`QuestionForm.razor`) muestra, como campos de solo lectura, la fecha y hora exactas (con minutos) de creación y de última actualización de la pregunta, en UTC, tanto para preguntas generadas por IA como creadas manualmente.

## Capabilities

### New Capabilities
(ninguna — este cambio extiende capacidades existentes)

### Modified Capabilities
- `ai-question-generation`: la "Bandeja de revisión de preguntas generadas" pasa a exponer también el progreso agregado por job (incluyendo ítems `Pending` aún en generación) y a identificar explícitamente el job de origen de cada ítem mostrado.
- `question-bank`: se añade el requisito de exponer y mostrar `CreatedAt`/`UpdatedAt` de una pregunta en su detalle/edición.

## Impact

- **Backend**: `IQuestionGenerationService` / `QuestionGenerationService` (nuevo método de progreso agregado por job), `QuestionGenerationController` (nuevo endpoint `GET`), `QuestionGenerationJobRepository` (nueva consulta de conteo por estado), `QuestionDto` y su mapeo (añadir `CreatedAt`/`UpdatedAt`).
- **Frontend**: `QuestionReview.razor` (indicador de estado agrupado por job + polling), `ApiService.cs` (nuevo método de cliente), `QuestionForm.razor` (mostrar fechas de solo lectura).
- **Sin cambios de esquema de base de datos**: los campos `CreatedAt`/`UpdatedAt` ya existen en `Question` vía `AuditableEntity`; no se requiere migración.
- **Sin SignalR ni infraestructura de tiempo real nueva**: el refresco es por polling desde el componente Blazor existente.
