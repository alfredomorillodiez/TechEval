## Why

Las preguntas abiertas suman a `TotalPoints` pero nunca pueden obtener puntos: `SubmitExamAsync` fija `IsCorrect = null` y no existe en todo el código ningún flujo de corrección manual. El resultado se calcula, se marca como aprobado o suspenso y se envía por email de inmediato, con los puntos de las abiertas contados como cero de forma permanente e irreversible.

Las especificaciones actuales ya recogen esta contradicción: `exam-results` afirma que las abiertas quedan «pendientes de revisión humana» y, dos requisitos más abajo, que la puntuación se calcula y se notifica «al completar la corrección de todas las preguntas» — un paso que nunca se implementó. El impacto es medible: en el banco actual hay 10 preguntas abiertas de 229 elegibles, y un examen generado automáticamente con 10 preguntas tiene un 36,6 % de probabilidad de incluir al menos una. En la combinación categoría `iECS` + dificultad `Avanzado`, las 3 preguntas disponibles son abiertas: el candidato obtiene 0 % garantizado con independencia de lo que responda.

Todavía no ha causado daño (0 respuestas abiertas entregadas en el histórico), por lo que existe margen para cerrarlo antes de que llegue a candidatos reales.

## What Changes

- **BREAKING** — `ExamResult.Passed` pasa de `bool` a `bool?`. Un resultado pendiente de corrección no tiene veredicto; hoy `false` significaría «Reprobado» en las ~28 lecturas repartidas por 7 páginas Razor y 3 servicios. La nulabilidad fuerza al compilador a señalar cada punto de consumo en lugar de dejar que el error aparezca en producción.
- Nuevo estado del resultado (`ExamResultStatus`): `PendingReview` o `Reviewed`. `SubmitExamAsync` lo decide según la **composición del examen**: si el examen contiene al menos una pregunta de tipo abierto, el resultado queda `PendingReview` con independencia de lo que haya respondido el candidato. Un examen compuesto solo por preguntas de test se cierra automáticamente, como hasta ahora.
- Nuevo almacenamiento de puntuación parcial por respuesta: `UserAnswer.AwardedPoints` (`int?`) y `UserAnswer.ReviewerComment` (`string?`). Sin ellos el corrector solo puede otorgar todo o nada, que es justo lo que anula el valor de una pregunta abierta. `AwardedPoints` se rellena también para las preguntas de test en el envío, congelando la puntuación obtenida frente a ediciones posteriores de `Question.Points`.
- Las respuestas abiertas dejadas **en blanco** (vacías o solo espacios) se pre-puntúan a 0 en el envío. No es un atajo que evite la revisión: el resultado sigue entrando en la cola y el corrector recibe esas respuestas precargadas a 0 para limitarse a confirmarlas.
- El email al candidato se bifurca: acuse de recibo «pendiente de corrección» al enviar (sin cifras), y el correo de resultado ya existente solo al finalizar la corrección.
- Nueva cola de corrección para administradores, con listado de resultados pendientes y pantalla de corrección que muestra el enunciado, la respuesta del candidato y el `SampleAnswer` de referencia.
- La corrección es **atómica**: un único envío puntúa todas las abiertas pendientes del resultado, recalcula `ObtainedPoints`, `ScorePercentage` y `Passed`, marca `Reviewed` y dispara el email. No existe estado intermedio de corrección parcial.
- El dashboard deja de contaminar sus métricas con resultados pendientes (media y tasa de aprobados se calculan solo sobre `Reviewed`) y añade el recuento de pendientes.
- La pantalla final del candidato y el portal del alumno muestran «Pendiente de corrección» en lugar de una nota provisional.
- Efecto colateral deliberado: la respuesta de `SubmitExamAsync` deja de incluir `CorrectAnswerText` e `IsCorrect` hacia el candidato, cerrando la filtración del solucionario por el endpoint anónimo de envío.

## Capabilities

### New Capabilities
- `open-question-review`: corrección manual de respuestas abiertas — cola de resultados pendientes, otorgación de puntos parciales con comentario del corrector, recálculo atómico del resultado y trazabilidad de quién corrigió y cuándo.

### Modified Capabilities
- `exam-results`: el ciclo de vida del resultado incorpora estado `PendingReview`/`Reviewed`; `Passed` pasa a ser opcional mientras hay corrección pendiente; el cálculo de puntuación se desacopla del envío; la notificación por email se bifurca en acuse y resultado; las estadísticas del dashboard excluyen los resultados pendientes.
- `candidate-experience`: la pantalla de confirmación muestra «pendiente de corrección» sin cifras cuando el examen contiene abiertas respondidas, en lugar del resultado final.
- `student-portal`: el listado de pruebas realizadas distingue los resultados pendientes de corrección de los ya cerrados, sin mostrar nota provisional.
- `admin-console`: nueva página de cola de correcciones pendientes y pantalla de corrección; los listados de resultados y sus filtros contemplan el tercer estado.
- `deployment-ops`: el script SQL aditivo que actualiza el esquema de una base de datos existente cubre las columnas nuevas de `ExamResults` y `UserAnswers`.

## Impact

**Dominio** — `ExamResult` (+`Status`, `ReviewedAt`, `ReviewedByUserId`; `Passed` nullable), `UserAnswer` (+`AwardedPoints`, `ReviewerComment`), nuevo enum `ExamResultStatus`.

**Aplicación** — `ExamTokenService.SubmitExamAsync` (decisión de estado, auto-cero de blancos, bifurcación del email, dejar de devolver el solucionario); `ResultService` (estadísticas filtradas por `Reviewed`); `StudentPortalService`; nuevo servicio de corrección; DTOs `ExamResultDto`, `ExamResultSummaryDto`, `CompletedExamDto`, `DashboardStatsDto`, `AnswerReviewDto`.

**API** — nuevos endpoints de cola y corrección bajo `ResultsController` o controlador propio, ambos `[Authorize(Roles = "Admin")]`.

**Infraestructura** — `IEmailService` gana `SendExamPendingReviewAsync`; `SmtpEmailService` la plantilla correspondiente; configuración EF de las columnas nuevas.

**Web** — nueva página de cola y de corrección; `Dashboard.razor`, `ResultList.razor`, `ResultsByExam.razor`, `ResultDetail.razor`, `TakeExam.razor` y `Student/Portal.razor` deben manejar `Passed` nulo y el estado pendiente.

**Base de datos** — cambio aditivo y no destructivo sobre `TechEvalDb`, siguiendo el patrón idempotente ya establecido en `scripts/add_user_link_columns.sql`. No requiere migraciones de EF Core ni recrear la base de datos.

**Fuera de alcance** — corrección asistida por IA sobre las respuestas abiertas; filtro de tipo de pregunta en la generación automática de exámenes (deja de ser urgente al desaparecer el resultado erróneo, queda como mejora de UX); los fallos de integridad del envío de examen (autenticación de sesión, temporizador de servidor, hash de contraseñas) son independientes y no se abordan aquí.
