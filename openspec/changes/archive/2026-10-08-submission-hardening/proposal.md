## Why

El análisis del 08·10·2026 encontró tres defectos en el envío de la prueba. Dos pueden dejar al candidato sin su resultado o con una ficha que no es la suya. El tercero deja la base de datos abierta a puntuar dos veces la misma respuesta.

- **D11.** Si Microsoft Graph falla después de confirmar el envío, el candidato recibe `500`. La prueba ya está guardada y puntuada, pero la pantalla le dice que ha fallado. Si repite el envío, el segundo intento devuelve el acuse sin enviar correo (es idempotente), así que el correo no sale nunca. La corrección manual ya tolera este fallo; el envío no.
- **S8.** `SaveDraftAnswerAsync` no mira si la sesión ya se envió. Mientras no pase el plazo, se puede reescribir una `UserAnswer` de una prueba con resultado. En una prueba de test la nota no cambia (está congelada), pero sí la ficha que ve el corrector. En una abierta, el corrector puede puntuar un texto que el candidato cambió después de enviar.
- **D12.** `UserAnswers` no tiene índice único sobre `(ExamSessionId, QuestionId)`. Dos autoguardados de la misma pregunta que coinciden en el tiempo crean dos filas. La corrección suma `AwardedPoints` de todas las respuestas, así que la pregunta se puntúa dos veces. La tabla tampoco tiene índices de apoyo para las consultas por sesión y por pregunta.

## What Changes

- **D11 — El fallo del correo no cambia la respuesta del envío.** `SubmitExamAsync` envía el correo después de confirmar y captura el fallo, igual que `ReviewWorkflow`. El candidato recibe siempre el acuse con `200`. `GraphEmailService` ya registra el fallo en el log antes de relanzarlo. La cancelación de la petición no se captura.
- **S8 — Una prueba enviada no admite más respuestas.** `SaveDraftAnswerAsync` rechaza con `409` y un mensaje claro cuando la sesión ya tiene resultado o ya no está en curso. La comprobación va después de la de propiedad (`403`) y antes de las de plazo (`409`) y longitud (`400`). La Web ya ignora los `409` del autoguardado.
- **D12 — Una sola respuesta por pregunta y sesión.**
  - Índice único `IX_UserAnswers_ExamSessionId_QuestionId` e índice `IX_UserAnswers_QuestionId`, en el modelo de EF y en `create_database.sql`.
  - Un guion aditivo nuevo, `scripts/add_useranswers_unique_index.sql`, idempotente. Si la base ya tiene filas duplicadas, **no** las borra: se detiene con un error que las lista, para que una persona decida cuál conserva.
  - El autoguardado tolera la carrera: si la inserción falla porque otra petición insertó antes, relee la fila y la actualiza.

Fuera de alcance:

- Reintentar el correo del envío más tarde, o guardar los fallos para enviarlos después. Exige una cola o una tabla nueva.
- La invitación (`SendExamAsync`), que guarda el token antes de enviar el correo: si Graph falla, queda un token huérfano y cada reintento crea otro. Es otro hallazgo.
- E12 y el resto de hallazgos abiertos.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `exam-taking`: el autoguardado rechaza una sesión ya enviada; el fallo del correo no altera la respuesta del envío; una pregunta tiene una sola respuesta por sesión.
- `deployment-ops`: nuevo guion aditivo del índice único, y `create_database.sql` lo recoge.

## Impact

- **Aplicación**: en `ExamTokenService`, `SaveDraftAnswerAsync` comprueba el estado de la sesión y tolera la carrera de inserción; `SubmitExamAsync` captura el fallo del correo. Excepción nueva `ExamAlreadySubmittedException : ConflictException`.
- **Infraestructura**: `UserAnswerConfiguration` declara los dos índices.
- **API**: sin endpoints nuevos. `answer` puede responder `409` por sesión enviada. `submit` deja de responder `500` por fallo de correo.
- **Web**: sin cambios. `SaveAnswerAsync` no lanza ante un `409`.
- **Base de datos**: dos índices nuevos en `UserAnswers`. Hay que ejecutar el guion en cada base desplegada. La API anterior sigue siendo compatible con la base nueva.
- **Pruebas**: envío con correo caído, autoguardado sobre sesión enviada, carrera de inserción, y deriva entre modelo y guion para los índices.
- **Documentación**: `documentacion.md` y `README.md` citan el guion nuevo.
