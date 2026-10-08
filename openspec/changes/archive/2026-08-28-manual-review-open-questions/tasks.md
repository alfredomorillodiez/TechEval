## 1. Dominio y esquema

- [x] 1.1 Crear el enum `ExamResultStatus { PendingReview = 1, Reviewed = 2 }` en `src/TechEval.Domain/Enums/`
- [x] 1.2 Añadir a `ExamResult` las propiedades `Status` (`ExamResultStatus`), `ReviewedAt` (`DateTime?`) y `ReviewedByUserId` (`int?`) con su navegación `ReviewedByUser`
- [x] 1.3 Cambiar `ExamResult.Passed` de `bool` a `bool?`
- [x] 1.4 Añadir a `UserAnswer` las propiedades `AwardedPoints` (`int?`) y `ReviewerComment` (`string?`)
- [x] 1.5 Actualizar las configuraciones EF de `ExamResult` y `UserAnswer` en `src/TechEval.Infrastructure/Data/Configurations/` (longitud de `ReviewerComment`, clave foránea e índice de `ReviewedByUserId`, índice sobre `Status` para la cola)
- [x] 1.6 Crear `scripts/add_review_columns.sql` siguiendo el patrón idempotente de `scripts/add_user_link_columns.sql`, asignando `Status = Reviewed` a los resultados históricos
- [x] 1.7 Actualizar `scripts/create_database.sql` con las columnas nuevas de `ExamResults` y `UserAnswers`
- [x] 1.8 Ejecutar `scripts/add_review_columns.sql` sobre la base de datos local y verificar que los 2 resultados existentes quedan en `Reviewed` sin pérdida de datos

## 2. Corrección en el envío del examen

- [x] 2.1 En `ExamTokenService.SubmitExamAsync`, fijar `AwardedPoints` de las preguntas de test (puntos completos si acierta, `0` si no) y calcular `ObtainedPoints` como suma de `AwardedPoints`
- [x] 2.2 Pre-puntuar a `AwardedPoints = 0` las respuestas abiertas cuyo `OpenAnswer` sea nulo, vacío o solo espacios (`string.IsNullOrWhiteSpace`), sin que ello exima al resultado de pasar por la cola
- [x] 2.3 Dejar `AwardedPoints = null` e `IsCorrect = null` en las respuestas abiertas con contenido real
- [x] 2.4 Decidir `Status` por la composición del examen: `PendingReview` si el examen contiene al menos una pregunta de tipo `OpenQuestion`, `Reviewed` en caso contrario
- [x] 2.5 Calcular `Passed` solo cuando `Status = Reviewed`; dejarlo `null` cuando quede corrección pendiente
- [x] 2.6 Sustituir la respuesta del envío por un acuse sin lista de respuestas, eliminando `CorrectAnswerText` e `IsCorrect` del payload que recibe el candidato
- [x] 2.7 Añadir `SendExamPendingReviewAsync(toEmail, toName, examTitle, ct)` a `IEmailService` e implementarla en `SmtpEmailService` con una plantilla sin cifras
- [x] 2.8 Bifurcar la notificación del envío: acuse si `PendingReview`, correo de resultado si `Reviewed`

## 3. Servicio de corrección manual

- [x] 3.1 Crear los DTO de la cola (resumen con candidato, prueba, fecha, días de espera y número de respuestas por corregir)
- [x] 3.2 Crear los DTO del detalle de corrección (enunciado, respuesta del candidato, puntos máximos, puntuación pre-asignada, `SampleAnswer`) y del envío de corrección (`UserAnswerId`, `AwardedPoints`, `ReviewerComment`)
- [x] 3.3 Añadir al repositorio de resultados las consultas de cola (`Status = PendingReview`, orden por `CompletedAt` ascendente) y de detalle con respuestas y preguntas incluidas
- [x] 3.4 Crear `IOpenQuestionReviewService` / `OpenQuestionReviewService` con las operaciones de cola, detalle y corrección
- [x] 3.5 Implementar la validación de la corrección: cobertura de **todas** las respuestas a preguntas abiertas del resultado (incluidas las que están en blanco con `0` pre-asignado), pertenencia de cada `UserAnswer` al resultado, y rango `[0, Question.Points]` de cada puntuación — rechazando la operación entera si algo falla
- [x] 3.6 Implementar el rechazo con `409 Conflict` cuando el resultado ya está en `Reviewed`
- [x] 3.7 Implementar el recálculo y cierre en el orden definido en el diseño: persistir respuestas, recalcular y cerrar el resultado con `ReviewedAt`/`ReviewedByUserId`, y solo después enviar el correo
- [x] 3.8 Capturar y registrar los fallos del envío de correo sin revertir la corrección
- [x] 3.9 Registrar el servicio nuevo en la inyección de dependencias de `Program.cs`

## 4. API

- [x] 4.1 Exponer el endpoint de cola de pendientes, restringido a `[Authorize(Roles = "Admin")]`
- [x] 4.2 Exponer el endpoint de detalle de corrección, con `404` si no existe y `409` si ya está corregido
- [x] 4.3 Exponer el endpoint de corrección atómica, tomando el `ReviewedByUserId` del claim del administrador autenticado
- [x] 4.4 Añadir comentarios XML de documentación a los endpoints nuevos para que aparezcan en Swagger

## 5. Propagación de `Passed` nullable y estado de corrección

- [x] 5.1 Actualizar `ExamResultDto`, `ExamResultSummaryDto` y `CompletedExamDto` con `Passed` nullable y el estado de corrección
- [x] 5.2 Añadir `AwardedPoints` y `ReviewerComment` a `AnswerReviewDto`
- [x] 5.3 Añadir el contador de pendientes de corrección a `DashboardStatsDto`
- [x] 5.4 En `ResultService.GetDashboardStatsAsync`, calcular media y tasa de aprobación solo sobre resultados `Reviewed` y añadir el recuento de pendientes
- [x] 5.5 Actualizar `ResultService.GetAllAsync`, `GetByExamAsync` y `GetDetailAsync` para propagar estado y puntuación otorgada
- [x] 5.6 Actualizar `StudentPortalService.GetCompletedAsync` para exponer el estado sin nota ni veredicto en los pendientes
- [x] 5.7 Recorrer con el compilador todos los puntos de lectura de `Passed` y confirmar que ninguno renderiza «Reprobado» ante un `null`

## 6. Interfaz de administración

- [x] 6.1 Crear la página `/admin/results/pending` con la cola ordenada por antigüedad, días de espera, recuento de respuestas por corregir y estado vacío
- [x] 6.2 Crear la pantalla de corrección con enunciado, respuesta del candidato, `SampleAnswer` de referencia, campo de puntos y campo de comentario por respuesta
- [x] 6.3 Bloquear el envío de la corrección mientras falte alguna puntuación o algún valor esté fuera de rango, señalando qué falta
- [x] 6.4 Manejar el `409 Conflict` mostrando aviso de resultado ya corregido y refrescando la cola
- [x] 6.5 Añadir al dashboard la tarjeta de correcciones pendientes con acceso directo a la cola
- [x] 6.6 Actualizar `ResultList.razor` y `ResultsByExam.razor`: etiqueta «Pendiente de corrección», KPIs sobre corregidos, filtro con el tercer estado y exclusión de los pendientes del filtro por rango de nota
- [x] 6.7 Actualizar `ResultDetail.razor` para mostrar el estado, los puntos otorgados y el comentario del corrector
- [x] 6.8 Añadir el enlace a la cola en la navegación de administración

## 7. Experiencia del candidato y portal del alumno

- [x] 7.1 Actualizar `TakeExam.razor` para mostrar el acuse «pendiente de corrección» sin puntuación, porcentaje, veredicto ni indicadores visuales de éxito o fracaso
- [x] 7.2 Mantener la pantalla de resultado actual cuando el examen se cierra como `Reviewed`
- [x] 7.3 Actualizar `Student/Portal.razor` para mostrar «Pendiente de corrección» en lugar de la nota en los resultados pendientes

## 8. Pruebas y verificación

- [x] 8.1 Test: examen solo de test se cierra como `Reviewed` con `Passed` calculado
- [x] 8.2 Test: examen con una abierta respondida queda `PendingReview` con `Passed = null`
- [x] 8.3 Test: examen con todas las abiertas en blanco queda igualmente `PendingReview`, con esas respuestas pre-puntuadas a `AwardedPoints = 0`
- [x] 8.4 Test: corrección completa recalcula `ObtainedPoints`, `ScorePercentage` y `Passed`, y cierra el resultado
- [x] 8.5 Test: corrección incompleta, con puntuación fuera de rango o con respuesta ajena se rechaza sin persistir nada
- [x] 8.6 Test: corrección sobre un resultado ya `Reviewed` devuelve conflicto
- [x] 8.7 Test: el dashboard excluye los resultados pendientes del promedio y de la tasa de aprobación
- [x] 8.8 Test: editar `Question.Points` después del envío no altera el `ObtainedPoints` del resultado al cerrar su corrección
- [x] 8.9 Verificar manualmente el flujo completo contra la aplicación en ejecución: enviar una prueba con una abierta, comprobar el acuse al candidato y el correo sin cifras, corregir desde la cola y comprobar el resultado definitivo y su correo
- [x] 8.10 Verificar que las métricas del dashboard no cambian respecto a antes del despliegue, con todos los resultados históricos en `Reviewed`
