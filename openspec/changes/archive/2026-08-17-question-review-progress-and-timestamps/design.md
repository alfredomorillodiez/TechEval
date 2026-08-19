## Context

`/admin/questions/review` (`QuestionReview.razor`) hoy solo carga, una vez al entrar a la página, el resultado de `GET api/question-generation/pending-items`. Ese endpoint (`QuestionGenerationJobRepository.GetPendingReviewItemsAsync`) filtra a nivel de base de datos:

```csharp
Where(i =>
    (i.Status == Succeeded && i.Question!.QuestionReviewStatus == PendingReview)
    || i.Status == Failed)
```

Los ítems en `Pending` (aún generándose) quedan fuera por diseño — no hay endpoint que devuelva progreso agregado por job, ni lista de jobs. El worker (`QuestionGenerationWorker`) procesa un job a la vez, secuencialmente, y persiste el progreso ítem a ítem (`jobRepo.UpdateAsync(job, ct)` tras cada ítem), así que el estado en base de datos ya refleja el avance en tiempo casi real — solo falta exponerlo y consultarlo.

`Question` ya hereda `CreatedAt`/`UpdatedAt` de `AuditableEntity`, pero `QuestionDto` (compartido por `QuestionForm.razor` y `QuestionReview.razor`) no los mapea.

## Goals / Non-Goals

**Goals:**
- Exponer el progreso agregado de generación por job (incluyendo ítems `Pending`) a través de un nuevo endpoint de solo lectura.
- Mostrar en `/admin/questions/review` un indicador siempre visible del estado de generación, agrupado por job, refrescado por polling.
- Exponer `CreatedAt`/`UpdatedAt` en `QuestionDto` y mostrarlos en UTC, con minutos, en `QuestionForm.razor`.

**Non-Goals:**
- No se introduce SignalR ni ningún mecanismo de push en tiempo real; el refresco es exclusivamente por polling del lado del cliente Blazor.
- No se añade ningún indicador de generación fuera de `/admin/questions/review` (ni badge en `MainLayout`, ni en `QuestionList.razor` más allá de lo ya existente).
- No se añade conversión a hora local ni configuración de zona horaria; las fechas se muestran en UTC tal como se almacenan.
- No se introduce un campo `ReviewedAt` separado de `UpdatedAt`; distinguir "cuándo se aprobó/rechazó" de "cuándo se editó el contenido" queda fuera de alcance.
- No se pagina ni se limita el histórico de jobs mostrados; el indicador solo cubre jobs con actividad relevante (ver Decisión de alcance de jobs).

## Decisions

**1. Nuevo endpoint de progreso agregado por job, en vez de calcular el progreso en el cliente**
Se añade `GetActiveJobsProgressAsync` a `IQuestionGenerationService`, respaldado por una nueva consulta en `QuestionGenerationJobRepository` que agrupa `QuestionGenerationJobItem` por `JobId` y cuenta por `Status`. Se expone como `GET api/question-generation/jobs/progress`, devolviendo una lista de `QuestionGenerationJobProgressDto` (`JobId`, `Topic`, `CategoryName`, `Status` del job, `RequestedCount`, `PendingCount`, `SucceededCount`, `FailedCount`, `CreatedAt`).
*Alternativa descartada*: calcular el progreso en el Web combinando `pending-items` con algo más — no es viable porque los ítems `Pending` nunca llegan al cliente hoy; hace falta un endpoint nuevo que sí los cuente.

**2. Alcance de jobs incluidos: solo jobs con ítems aún no revisados o en curso**
El endpoint de progreso devuelve jobs que tengan al menos un ítem en `Pending`, o en `Succeeded`+`PendingReview` (listo para revisar pero no decidido), o `Failed`. Un job donde todos sus ítems ya fueron `Approved`/`Rejected` deja de aparecer — mantiene el indicador enfocado en "lo que aún requiere atención" y evita una lista que crece indefinidamente.

**3. Polling desde `QuestionReview.razor` con `PeriodicTimer`, sin nueva infraestructura**
El componente usa un `PeriodicTimer` (cada pocos segundos, p. ej. 5s) mientras esté montado, invocando `Api.GetActiveGenerationProgressAsync()` y refrescando tanto el indicador de estado como la lista de ítems pendientes de revisión. Se detiene el timer en `Dispose`/`IAsyncDisposable` para no dejar polling huérfano al navegar fuera de la página.
*Alternativa descartada*: SignalR — mayor complejidad (hub, conexión, ciclo de vida) para una ganancia de latencia irrelevante dado que el worker ya persiste incrementalmente y el polling de pocos segundos es indistinguible de tiempo real para este caso de uso administrativo.

**4. `QuestionDto` extendido con `CreatedAt`/`UpdatedAt`, mapeados en UTC sin conversión**
Se añaden ambos campos (`DateTime CreatedAt`, `DateTime? UpdatedAt`) al record `QuestionDto` y a su mapeo en `QuestionGenerationService.MapQuestionToDto` y en el servicio de preguntas usado por `QuestionForm`/`QuestionList`. En el Razor se formatean con minutos, p. ej. `@Question.CreatedAt.ToString("yyyy-MM-dd HH:mm") UTC`, sin conversión de zona horaria — el valor ya es UTC en base de datos (`DateTime.UtcNow`).

**5. Agrupación visual por job en el indicador de la bandeja de revisión**
El indicador se organiza como una lista de "jobs activos", cada uno mostrando: tema (`Topic`), categoría, contador `Succeeded/Failed/Pending de RequestedCount`, y estado del job (`Running`/`Completed`/`Failed`). Esto también resuelve, para la lista de ítems ya existente, el requisito ya documentado de "identificar de qué job proviene cada uno": cada ítem de la lista de revisión se anota con el `Topic`/`JobId` de su job de origen (dato que el DTO ya transporta pero que la UI no pintaba).

## Risks / Trade-offs

- **[Riesgo] Polling constante mientras la pestaña de revisión esté abierta genera carga extra en la API**, aunque baja (una query ligera de agregación cada pocos segundos, y solo mientras un admin tiene la página abierta) → Mitigación: consulta de agregación indexada por `JobId`/`Status`, sin cargar el contenido completo de las preguntas en cada tick; solo la lista completa de ítems (`pending-items`) se recarga cuando cambian los conteos, no en cada tick.
- **[Riesgo] Mostrar solo UTC puede confundir a un admin que espera hora local** → Mitigación: se etiqueta explícitamente "UTC" junto a la fecha; decisión ya validada con el usuario, no requiere resolución adicional.
- **[Riesgo] `QuestionGenerationJobItem` no tiene timestamp propio**, así que no se puede mostrar "hace cuánto se generó cada ítem individual", solo la fecha de creación del job → Aceptado como limitación conocida; no se añade migración de esquema para este cambio.

## Open Questions

Ninguna pendiente — alcance, mecanismo de refresco y formato de fecha ya fueron confirmados por el usuario.
