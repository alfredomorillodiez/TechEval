## Context

Ver `proposal.md` — Why para la motivación. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- `OpenQuestionReviewService` hace hoy toda la corrección: la cola (`GetPendingAsync`), el detalle (`GetDetailAsync`) y el envío (`SubmitReviewAsync`), con la validación, la transacción, el recálculo y el correo. Sus DTO llevan `CandidateName` y `CandidateEmail`, y el envío devuelve `ExamResultDto`, que también los lleva.
- `ExamResultRepository.GetPendingReviewAsync` y `GetForReviewAsync` cargan el resultado con su prueba, su sesión y sus respuestas.
- `ExamIntegrityService.GetReportAsync` devuelve `IntegrityEventDto` con `OccurredAt`, la hora UTC de cada señal. `IntegrityPanel.razor` la muestra como `HH:mm:ss`.
- Las políticas viven en `Policies.cs` (`Gestion`, `Alumno`). ASP.NET combina con Y el `[Authorize]` de la clase y el del método.
- `IUnitOfWork.ExecuteInTransactionAsync` y el patrón de `AdminCountLock` (no hace nada si la base no es relacional) ya existen. Las pruebas usan la base en memoria de EF, que no admite `ExecuteUpdateAsync`.
- `ReviewResult.razor` contiene el formulario de corrección, con la validación de rango y el envío bloqueado.
- `SchemaDriftTests` exige que modelo y `create_database.sql` coincidan.

## Goals / Non-Goals

**Goals:**

- Que la identidad del candidato no pueda llegar al evaluador por ningún DTO, ni por error.
- Que la comprobación de acceso del evaluador esté en una sola consulta, usada por todas sus operaciones.
- Que la validación y el cierre de la corrección sigan siendo un solo código para el administrador y para el evaluador.
- Que dos correctores no puedan tomar el mismo resultado, ni siquiera a la vez.

**Non-Goals:**

- Ceguera frente al contenido: lo que el candidato escribe llega tal cual.
- Anonimato estadístico. En una prueba que hizo un solo candidato, el evaluador que sabe quién la hizo sabe de quién es. La ceguera quita la identidad de la pantalla; no la hace imposible de deducir.
- Reparto automático ni avisos por correo al evaluador.

## Decisions

### D1. Asignación: tabla puente con clave compuesta

Nueva entidad `ExamEvaluator`:

| Campo | Tipo | Uso |
|---|---|---|
| `ExamId` | `int` | PK compuesta; FK a `Exams`, borrado en cascada |
| `UserId` | `int` | PK compuesta; FK a `Users`, sin cascada |
| `AssignedAt` | `DateTime` | cuándo |
| `AssignedByUserId` | `int` | FK a `Users`, sin cascada |

La clave compuesta impide la asignación duplicada sin código. Asignar otra vez es un «no hacer nada». Los usuarios no se borran nunca (se desactivan), así que la FK a `Users` no necesita cascada, y así se evitan los caminos de cascada múltiples que SQL Server rechaza.

Endpoints en `ExamsController` (política `Gestion`):

- `GET /api/exams/{id}/evaluators`
- `POST /api/exams/{id}/evaluators/{userId}` — `400` si el usuario no es un evaluador activo
- `DELETE /api/exams/{id}/evaluators/{userId}`

La lista de evaluadores elegibles sale de `GET /api/users?role=Evaluador&active=true`, que ya existe.

### D2. Reserva: dos columnas en `ExamResult` y una escritura condicional

`ExamResult` gana `ReservedByUserId` (`int?`, FK a `Users` sin cascada) y `ReservedUntil` (`DateTime?`). Una tabla aparte no aporta nada: un resultado tiene como mucho una reserva.

Tomar la reserva es **una sola sentencia condicional**, no leer y después escribir:

```sql
UPDATE ExamResults
SET ReservedByUserId = @me, ReservedUntil = @now + 30 min
WHERE Id = @id AND Status = PendingReview
  AND (ReservedByUserId IS NULL OR ReservedByUserId = @me OR ReservedUntil < @now)
```

Si afecta a una fila, la reserva es de quien la pidió. Si afecta a cero, otra persona la tiene, y se lee `ReservedUntil` para el `409`. Así dos correctores que abren a la vez no pueden ganar los dos. Renovar es la misma sentencia.

El envío hace, dentro de su transacción, la misma comprobación: libera la reserva con una escritura condicional (`WHERE ... AND (ReservedByUserId = @me OR ReservedByUserId IS NULL OR ReservedUntil < @now)`). Cero filas significa que otra persona la tiene, y la transacción se revierte con `409`. Liberar sin corregir pone las dos columnas a nulo si son de quien lo pide, o siempre si lo pide un administrador.

Las sentencias van en `IExamResultRepository` (`TryReserveAsync`, `TryReleaseForSubmitAsync`, `ReleaseAsync`). En la base relacional usan `ExecuteUpdateAsync`. En la base en memoria, que no lo admite, hacen la misma comprobación sobre la entidad cargada: sirve para probar las reglas, no la concurrencia, que se prueba contra SQL Server.

Alternativas descartadas:
- **Leer, comprobar y escribir con el `DbContext`.** Dos peticiones simultáneas leen «libre» y las dos escriben. La última gana y la primera trabaja en balde, que es justo lo que la reserva quiere evitar.
- **Bloqueo de aplicación como en `AdminCountLock`.** Funciona, pero serializa todas las reservas de todos los resultados. La escritura condicional solo compite por la fila.
- **Concurrencia optimista con `rowversion`.** Obliga a una columna más y a traducir `DbUpdateConcurrencyException`; la condición en el `WHERE` dice lo mismo en una línea.

### D3. Un solo flujo de corrección, dos fachadas

La validación, la transacción, el recálculo, el cierre y el correo salen de `OpenQuestionReviewService` a una clase propia, `ReviewWorkflow`:

- `BuildReviewableAnswers(ExamResult)` → las respuestas abiertas en orden del examen, con enunciado y puntos congelados y la referencia.
- `ApplyAsync(ExamResult, SubmitReviewDto, reviewerId, releaseCheck)` → valida, escribe todo en una transacción (incluida la liberación condicional de D2) y envía el correo después.

`OpenQuestionReviewService` (administrador) y el nuevo `EvaluationService` (evaluador) llaman a `ReviewWorkflow` y solo se diferencian en dos cosas: cómo obtienen el resultado y qué DTO devuelven. El comportamiento del administrador no cambia, salvo la reserva.

### D4. Acceso del evaluador: una consulta

`IExamResultRepository.GetForEvaluatorAsync(resultId, evaluatorId, evaluatorEmail)` carga el resultado solo si:

- existe una fila `ExamEvaluators` con su `ExamId` y el `evaluatorId`, y
- `UserId` no es el evaluador y `CandidateEmail` no es su email.

Si devuelve nulo, el servicio lanza `NotFoundException`: inexistente, no asignado y propio dan el mismo `404`. La cola usa el mismo filtro en `GetPendingForEvaluatorAsync`. El historial no lo usa: filtra por `ReviewedByUserId`, porque no depende de la asignación.

El email del evaluador sale de la base, no del token, para no depender de un claim que el usuario pudo cambiar.

### D5. Ceguera: DTO propios, sin campos de identidad

El evaluador recibe tipos distintos, que no tienen dónde llevar la identidad:

- `EvaluatorQueueItemDto(ResultId, ExamTitle, CandidateAlias, CompletedOn, DaysWaiting, OpenAnswerCount, ReservedByOther, ReservedUntil)`
- `EvaluatorReviewDetailDto(ResultId, ExamTitle, CandidateAlias, CompletedOn, TotalPoints, AutoScoredPoints, Answers, ReservedUntil)`
- `EvaluatorReviewOutcomeDto(ResultId, CandidateAlias, ObtainedPoints, TotalPoints, ScorePercentage, Passed)`
- `EvaluatorHistoryItemDto` y `EvaluatorHistoryDetailDto`

`CompletedOn` y la fecha de corrección son `DateOnly`, así que la hora no puede viajar. El alias sale de `CandidateAlias.For(resultId)` → `Candidato R-{id}`.

Alternativa descartada: **el mismo DTO con los campos vacíos.** Un campo que existe acaba rellenándose por error en un cambio futuro. Un tipo sin el campo no puede. Una prueba serializa cada DTO del evaluador con un candidato de nombre y email conocidos y comprueba que ninguno de los dos aparece en el JSON.

### D6. Señales sin hora: el mismo DTO, con el tiempo relativo

`IntegrityEventDto` gana `ElapsedSeconds` y `OccurredAt` pasa a ser anulable. `ExamIntegrityService` gana `GetEvaluatorReportAsync(resultId)`, que rellena `ElapsedSeconds = OccurredAt - StartedAt` y deja `OccurredAt` a nulo. El informe del administrador rellena los dos. `IntegrityPanel.razor` muestra `mm:ss desde el inicio` cuando no hay `OccurredAt`.

Se reutiliza el tipo porque su contenido, salvo la hora, es el mismo, y porque así el panel no se duplica. La prueba de ceguera de D5 cubre también que `OccurredAt` llegue nulo.

### D7. Política y controlador del evaluador

`Policies.cs` gana `Evaluacion` (rol `Evaluador`). Nuevo `EvaluationController` en `api/evaluation`, con la política en la clase:

| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/api/evaluation/queue` | cola |
| GET | `/api/evaluation/{resultId}` | toma la reserva y devuelve el detalle |
| POST | `/api/evaluation/{resultId}/reservation` | renueva |
| DELETE | `/api/evaluation/{resultId}/reservation` | libera |
| POST | `/api/evaluation/{resultId}` | envía la corrección |
| GET | `/api/evaluation/{resultId}/integrity` | señales sin hora |
| GET | `/api/evaluation/history` · `/history/{resultId}` | historial |

Como todo el evaluador vive en su controlador, no hay que abrir ningún endpoint de `ResultsController` ni de `ReviewController`, y el problema de combinar atributos no aparece. `ReviewController` (administrador) gana `POST` y `DELETE /api/review/{resultId}/reservation`, y su `GET` toma la reserva.

### D8. Web: un formulario compartido

El formulario de corrección sale de `ReviewResult.razor` a `Shared/ReviewForm.razor`: recibe las respuestas y devuelve la corrección por un `EventCallback`. Lo usan `ReviewResult.razor` y la nueva `Pages/Evaluator/Review.razor`.

Las dos pantallas renuevan la reserva cada 10 minutos con un `PeriodicTimer` mientras siguen abiertas, y la liberan al pulsar «Cancelar». No la liberan al cerrar la pestaña: el navegador no garantiza esa petición, y la reserva caduca sola a los 30 minutos.

`Pages/Evaluator/Home.razor` se sustituye por `Queue.razor` en `/evaluacion`. Páginas nuevas: `Review.razor`, `History.razor` y `HistoryDetail.razor`. `ExamDetail.razor` gana la tarjeta de evaluadores; `Dashboard.razor`, el aviso; `PendingReviewList.razor`, la reserva y «Liberar».

### D9. Dashboard y trazabilidad

`DashboardStatsDto` gana `ExamsWithoutEvaluator` (`Id`, `Title`): pruebas activas con alguna pregunta abierta y sin filas en `ExamEvaluators`. Es una consulta agregada, en la línea de R2.

`ExamResultDto` gana `ReviewedByName` y `ReviewedAt`, que `ResultService.GetDetailAsync` rellena con un `Include` de `ReviewedByUser`. Solo lo recibe el administrador.

### D10. Cambio de rol

`UserManagementService.ChangeRoleAsync`, cuando el rol de origen es `Evaluador`, borra sus filas de `ExamEvaluators` y libera sus reservas (`ReleaseAllOfAsync(userId)`) dentro de la misma transacción del cambio.

## Risks / Trade-offs

- [Una prueba de un solo candidato, o una fecha que solo tiene un envío, permite deducir la identidad] → Es una limitación de la ceguera, no un fallo. La documentación lo dice. Quitar la fecha del todo impediría ordenar la cola por espera.
- [El `resultId` está en la ruta y es la base del alias] → El alias no pretende ocultar el identificador del resultado; pretende no ser el nombre. El identificador no identifica al candidato sin acceso de administrador.
- [Una pestaña en segundo plano frena el temporizador y la reserva caduca] → Al enviar, una reserva caducada que nadie tomó sigue valiendo. Si alguien la tomó, la pantalla muestra el `409` y el trabajo se pierde; es el mismo caso que hoy con dos administradores, pero ahora tiene que pasar media hora.
- [La base en memoria no ejercita la escritura condicional real] → Las reglas se prueban en memoria; la carrera de dos aperturas simultáneas se prueba contra SQL Server, como en `user-roles`.
- [Un evaluador desactivado conserva sus reservas hasta 30 minutos] → La cola del administrador las muestra y permite liberarlas.

## Migration Plan

1. Ejecutar `scripts/add_evaluator_columns.sql`. Es aditivo: el binario anterior sigue funcionando contra la base actualizada.
2. Desplegar la API y la Web nuevas.
3. Asignar evaluadores a las pruebas con preguntas abiertas. El dashboard lista las que falten.

Vuelta atrás: desplegar los binarios anteriores. La tabla y las columnas nuevas no les molestan. La cabecera del guion trae el SQL para quitarlas, si se quiere.
