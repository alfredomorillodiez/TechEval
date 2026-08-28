## Context

`ExamTokenService.SubmitExamAsync` corrige el examen en el momento del envío, persiste un `ExamResult` con veredicto y dispara el correo de resultado — todo en una sola operación. Las preguntas abiertas entran en ese cálculo con `IsCorrect = null` y cero puntos, y no existe ningún camino para revisarlas después. La consecuencia es un resultado cerrado y erróneo.

Restricciones que condicionan el diseño:

- **`Passed` es `bool` no-nullable y se lee en ~28 puntos** repartidos por 7 páginas Razor (`Dashboard`, `ResultList`, `ResultsByExam`, `ResultDetail`, `TakeExam`, `Student/Portal`) y 3 servicios. Cualquier estado intermedio que no se propague explícitamente se renderiza como «Reprobado».
- **No hay migraciones de EF Core.** El arranque usa `DbSeeder.EnsureCreatedAsync()`, que no hace nada sobre una base de datos existente. El proyecto ya resuelve la evolución de esquema con scripts SQL idempotentes en `scripts/`, y `scripts/add_user_link_columns.sql` es el precedente exacto de un cambio aditivo de columnas sobre datos vivos.
- **`BaseRepository` hace `SaveChanges()` en cada llamada** y no existe unidad de trabajo ni transacción. Cualquier operación multi-entidad se escribe por partes.
- **`ExamSessionController` no tiene autenticación.** Es un problema conocido e independiente; este diseño no lo resuelve, pero tampoco debe empeorarlo.
- Ya existe un patrón de revisión humana en el proyecto (`QuestionReview.razor` + `QuestionGenerationController`, con `pending-items` / `approve` / `reject`) que sirve de referencia de estilo para la cola y la pantalla de corrección.

Decisiones de producto ya cerradas por el responsable del proyecto: las abiertas en blanco se auto-puntúan a 0; al candidato solo se le comunica «pendiente de corrección» sin cifras; el administrador corrige todo el resultado de una vez, sin guardado parcial.

## Goals / Non-Goals

**Goals:**

- Que una pregunta abierta pueda obtener puntos, siempre, mediante corrección humana.
- Que ningún resultado con corrección pendiente se presente nunca como aprobado ni como suspenso, en ninguna superficie (pantalla final, portal del alumno, listados, dashboard, email).
- Que el administrador tenga una cola de trabajo priorizada por antigüedad y una pantalla de corrección con la información necesaria para puntuar con criterio.
- Que las métricas del dashboard dejen de contaminarse con puntuaciones parciales.
- Que el cambio se despliegue sobre la base de datos actual sin pérdida de datos y sin recrearla.

**Non-Goals:**

- Corrección asistida por IA de las respuestas abiertas. Es la evolución natural (la infraestructura de Ollama ya existe), pero se aborda como cambio posterior sobre estos cimientos.
- Filtro de tipo de pregunta en la generación automática de exámenes. Deja de ser urgente: con este cambio un examen generado con abiertas produce una cola de corrección, no una nota falsa.
- Introducir migraciones de EF Core. Sigue siendo la mejora estratégica correcta, pero no bloquea este cambio y mezclarla aquí ampliaría el radio de riesgo.
- Corregir los fallos de integridad del envío de examen (sesión sin autenticar, temporizador de cliente, hash de contraseñas sin sal).
- Notificar al reclutador cuando entra trabajo en la cola.

## Decisions

### `Passed` pasa a `bool?` en lugar de mantenerse `bool` junto a un `Status`

Un resultado pendiente no tiene veredicto. Representarlo con `Passed = false` es exactamente el bug que el cambio pretende evitar, y volvería a aparecer en cada una de las ~28 lecturas existentes.

*Alternativa considerada:* mantener `Passed` como `bool` y añadir solo `Status`, filtrando en cada consumidor. Se descarta porque el compilador guarda silencio: los puntos no adaptados se descubrirían de uno en uno en producción. Con `bool?`, el compilador enumera exhaustivamente el trabajo pendiente, que es justo lo que se necesita en un cambio con este radio.

El coste es un cambio de forma en tres DTO públicos (`ExamResultDto`, `ExamResultSummaryDto`, `CompletedExamDto`). Es un coste aceptable y explícito, marcado como **BREAKING** en la propuesta.

### El estado vive en `ExamResult.Status`, no se deriva de las respuestas

Se añade `ExamResultStatus { PendingReview = 1, Reviewed = 2 }` como columna persistida.

*Alternativa considerada:* derivarlo al vuelo con `UserAnswers.Any(a => a.AwardedPoints == null)`. Se descarta por dos motivos: obliga a cargar las respuestas en cada listado (los listados de resultados ya no paginan y cargan todo), y deja el estado sin punto de anclaje para `ReviewedAt` / `ReviewedByUserId`. Un estado explícito además permite indexar la cola.

Numeración desde `1` por coherencia con el resto de enums del dominio (`QuestionType`, `QuestionReviewStatus`, `DifficultyLevel`), que evitan deliberadamente el `0` por defecto de CLR.

### `AwardedPoints` en `UserAnswer` es la fuente de verdad de la puntuación

`IsCorrect` es `bool?` y solo expresa acierto/fallo — insuficiente para una abierta de 3 puntos donde el candidato merece 2. Se añade `AwardedPoints` (`int?`) y se rellena **también para las preguntas de test** en el envío (puntos completos o 0), no solo para las abiertas.

La razón principal es de **integridad histórica**, no de uniformidad. `Question.Points` es editable en cualquier momento desde `QuestionService.UpdateAsync`, y entre el envío de un examen y el cierre de su corrección manual pueden pasar días. Si el recálculo reconstruyera la parte de test recorriendo las preguntas (*«por cada test acertada, suma `Question.Points`»*), leería el valor **actual** del banco, no el vigente el día del examen: una pregunta reevaluada de 1 a 5 puntos inflaría retroactivamente el numerador de un resultado cuyo `ExamResult.TotalPoints` ya quedó congelado en el envío, produciendo porcentajes por encima del 100 %.

Con `AwardedPoints`, cada respuesta congela sus puntos en el momento del envío y `ObtainedPoints` es una suma directa. Numerador y denominador quedan congelados a la vez y siguen siendo comparables.

Como beneficios secundarios, el recálculo pasa a tener una sola rama en lugar de distinguir entre acierto automático y puntuación manual, y la pantalla de detalle necesita el campo de todos modos para mostrar «2 / 3» en cada respuesta abierta.

`IsCorrect` se conserva por compatibilidad y porque sigue siendo la información que se muestra en el detalle de respuestas de test.

`ReviewerComment` (`string?`) se añade en el mismo movimiento: es lo que hace defendible una nota cuando el candidato la cuestiona, y añadirlo después costaría un segundo script de esquema.

### La decisión de estado se toma sobre la composición del examen, no sobre las respuestas

`Status = PendingReview` si y solo si el examen contiene al menos una pregunta de tipo `OpenQuestion`, con independencia de lo que el candidato haya escrito.

*Alternativa considerada:* condicionarlo al contenido de las respuestas — pendiente solo si alguna abierta trae texto real, cerrando automáticamente el examen cuyas abiertas quedaron todas vacías. Se descarta por decisión de producto y porque el criterio de composición tiene dos ventajas concretas: el estado es **predecible en el momento de montar el examen** (al crear una prueba con abiertas ya se sabe que generará trabajo de corrección, en lugar de depender de lo que haga cada candidato), y **un humano siempre valida** una prueba con preguntas abiertas antes de emitir un veredicto, incluso cuando el candidato no respondió nada.

El coste es que un examen enviado en blanco ocupa un hueco en la cola. Se mitiga con la pre-puntuación: esas respuestas llegan al corrector ya a `0` y solo hay que confirmarlas.

Consecuencia sobre el alcance de la corrección: como las respuestas en blanco se pre-puntúan a `0` en el envío, «respuestas pendientes» **no** puede definirse como «las que tienen `AwardedPoints` nulo» — un examen íntegramente en blanco tendría cero pendientes y sin embargo está en la cola. La corrección abarca por tanto **todas las respuestas a preguntas abiertas del resultado**, y el `0` de los blancos es un valor por defecto que el corrector confirma o modifica, no un atajo que las excluya.

### La corrección es una única operación atómica sin estado intermedio

Un solo endpoint recibe la puntuación de **todas** las respuestas abiertas pendientes del resultado, valida el conjunto completo, y solo entonces persiste, recalcula y cierra. Si falta una respuesta, sobra una ajena, o algún valor sale del rango `[0, Question.Points]`, se rechaza la operación entera sin escribir nada.

*Alternativa considerada:* guardado parcial con un tercer estado `PartiallyReviewed`. Se descarta por decisión de producto y porque elimina toda una clase de problemas: no hay resultados a medio corregir, no hay que decidir qué pasa si un corrector abandona a mitad, y el recálculo tiene un único punto de entrada.

La protección frente a doble corrección es un `409 Conflict` cuando `Status` ya es `Reviewed`. Es control de concurrencia optimista suficiente para el volumen esperado; no se introduce `RowVersion`.

### La ausencia de transacción se compensa con el orden de escritura

`BaseRepository` no ofrece unidad de trabajo, y añadirla excede el alcance de este cambio. La corrección escribe en dos entidades (`UserAnswer` × N, y `ExamResult`), por lo que se establece un orden que hace que un fallo a medias sea recuperable:

1. Persistir los `AwardedPoints` y `ReviewerComment` de todas las respuestas.
2. Recalcular y persistir el `ExamResult` con `Status = Reviewed`.
3. Enviar el correo de resultado.

Si falla entre 1 y 2, el resultado permanece `PendingReview` con las respuestas ya puntuadas: el administrador vuelve a entrar, ve sus valores y reconfirma. Es idempotente en la práctica. El orden inverso dejaría un resultado cerrado con respuestas sin puntuar, que sí sería irrecuperable desde la interfaz.

El envío del correo va **después** del cierre y sus fallos se registran sin revertir: una corrección válida no debe perderse porque el SMTP esté caído.

### La bifurcación del correo añade un método al servicio de email

`IEmailService` gana `SendExamPendingReviewAsync(toEmail, toName, examTitle, ct)` — sin parámetros de puntuación, para que sea estructuralmente imposible filtrar cifras en el acuse. `SendExamResultAsync` se mantiene sin cambios de firma y pasa a invocarse desde dos sitios: el envío (cuando el resultado nace `Reviewed`) y el cierre de la corrección.

### El esquema se actualiza con un script SQL aditivo, siguiendo el precedente del proyecto

`scripts/add_review_columns.sql`, con la misma forma que `scripts/add_user_link_columns.sql`: guardas `COL_LENGTH` / `sys.foreign_keys` / `sys.indexes` antes de cada `ALTER`, ejecución repetible sin error, sin pérdida de datos.

Los resultados históricos reciben `Status = Reviewed` por defecto. Es la lectura correcta: son resultados ya cerrados de hecho, y marcarlos como pendientes los volcaría de golpe en la cola y distorsionaría el dashboard.

`scripts/create_database.sql` se actualiza en paralelo para que una base de datos creada desde cero y otra actualizada converjan en el mismo esquema — el requisito ya especificado en `deployment-ops`.

### El endpoint de envío deja de devolver el solucionario

`SubmitExamAsync` construye hoy un `AnswerReviewDto` con `CorrectAnswerText` e `IsCorrect` y lo devuelve por el endpoint anónimo `POST /api/exam/submit`. Con este cambio la respuesta al candidato pasa a ser un acuse mínimo (estado y, cuando procede, la puntuación), sin lista de respuestas.

No es un objetivo declarado del cambio, pero el rediseño de la respuesta lo hace inevitable y sería absurdo reconstruir la fuga a propósito. Se documenta como escenario en `candidate-experience` para que quede fijado y no se reintroduzca.

## Risks / Trade-offs

**`Passed` nullable rompe tres DTO y ~28 puntos de lectura** → Es el coste buscado, no un efecto colateral: el compilador convierte un riesgo silencioso en una lista de tareas. Se mitiga acometiendo la propagación en una sola pasada, guiada por el compilador, con la tarea de verificación de que ninguna superficie renderiza «Reprobado» ante un `null`.

**Sin transacción, un fallo entre la puntuación de respuestas y el cierre del resultado deja estado a medias** → El orden de escritura elegido hace ese estado recuperable y la reconfirmación idempotente. Aceptado conscientemente para no arrastrar la introducción de una unidad de trabajo dentro de este cambio.

**Dos administradores pueden abrir la misma corrección a la vez** → El segundo recibe `409 Conflict` y la interfaz refresca la cola. Se pierde su trabajo de puntuación, no los datos. Con el volumen actual (0 resultados pendientes hoy) el riesgo es teórico; si la cola crece, la mitigación siguiente sería un bloqueo blando por corrector.

**La cola puede envejecer sin que nadie lo note, dejando candidatos esperando indefinidamente** → Se mitiga parcialmente ordenando por antigüedad, mostrando los días transcurridos y sacando el contador de pendientes al dashboard. Una alerta activa al reclutador queda fuera de alcance y es la mejora natural si el problema aparece.

**El script SQL se ejecuta a mano y puede olvidarse en un despliegue** → Riesgo heredado del enfoque actual del proyecto, no introducido aquí. La aplicación fallará de forma visible al leer columnas inexistentes. Es un argumento más a favor de adoptar migraciones de EF Core como cambio independiente.

**El candidato recibe menos información que antes al terminar** → Es deliberado y decidido por producto: un acuse honesto es preferible a una nota provisional que después cambia. El resultado definitivo llega por email al cerrarse la corrección.

## Migration Plan

1. Ejecutar `scripts/add_review_columns.sql` sobre `TechEvalDb`. Aditivo, idempotente, sin pérdida de datos. Los resultados existentes quedan `Reviewed`.
2. Desplegar API y Web juntas. La API con `Passed` nullable y la Web anterior son incompatibles: una Web antigua contra la API nueva deserializaría `null` sobre un `bool` y fallaría. No hay despliegue parcial válido.
3. Verificar que el dashboard sigue mostrando las mismas métricas que antes del despliegue — con todos los resultados históricos en `Reviewed`, los números deben ser idénticos. Cualquier variación indica un error en el filtrado.
4. Verificar que la cola de pendientes aparece vacía.

**Rollback:** revertir el código de API y Web a la versión anterior. Las columnas añadidas pueden permanecer en la base de datos sin efecto — son nulables y la versión anterior las ignora. No se requiere script de reversión de esquema, lo que hace el rollback barato. Si se hubiera corregido algún resultado antes del rollback, su `ObtainedPoints` recalculado persiste, que es el comportamiento deseado.

## Open Questions

- **Aviso al generar un examen con preguntas abiertas.** Con la corrección disponible, avisar al administrador («esta prueba incluye 2 preguntas que requerirán corrección manual») pasa a ser útil en lugar de un aviso sin remedio. No se incluye aquí; conviene decidir si entra como mejora inmediata de UX en `exam-management`.
- **Antigüedad a partir de la cual una corrección pendiente debe escalarse.** El diseño expone los días transcurridos pero no define umbral ni alerta. Depende de un compromiso de servicio con el candidato que aún no existe.
- **Recorrido de la corrección asistida por IA.** `Question.SampleAnswer` ya alimenta al corrector humano y sería la entrada natural de un pre-scoring con Ollama sobre estos mismos campos (`AwardedPoints`, `ReviewerComment`). Conviene confirmar que la forma elegida aquí no estorba a ese cambio posterior antes de darlo por cerrado.
