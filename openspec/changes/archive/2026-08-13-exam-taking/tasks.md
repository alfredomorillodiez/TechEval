# Tasks: exam-taking

## 1. Dominio

- [x] 1.1 Crear entidad `ExamSession` (Id, ExamTokenId, StartedAt, CompletedAt, Status, relaciones con ExamToken/UserAnswers/ExamResult)
- [x] 1.2 Crear entidad `UserAnswer` (Id, ExamSessionId, QuestionId, SelectedAnswerId, OpenAnswer, IsCorrect, AnsweredAt)
- [x] 1.3 Crear enum `SessionStatus` (InProgress, Completed, Expired, Abandoned)

## 2. Validación y ciclo de vida del token

- [x] 2.1 Implementar `ValidateTokenAsync` verificando existencia, expiración (`IsExpired`) y uso previo (`IsUsed`) del `ExamToken`
- [x] 2.2 Implementar `StartSessionAsync`: crear `ExamSession` únicamente si el token es válido (`IsValid`), marcando el token como usado (`IsUsed`, `UsedAt`)
- [x] 2.3 Manejar el caso de sesión ya existente en `StartSessionAsync` devolviendo la sesión previa en lugar de duplicarla
- [x] 2.4 Construir el DTO de detalle de sesión (`ExamSessionInfoDto`) con preguntas y opciones ordenadas

## 3. Auto-guardado de respuestas

- [x] 3.1 Implementar `SaveDraftAnswerAsync` con lógica de upsert (crear o actualizar `UserAnswer` por sesión y pregunta)
- [x] 3.2 Exponer endpoint público `POST /api/exam/answer/{sessionId}`

## 4. Envío final de examen

- [x] 4.1 Implementar `SubmitExamAsync` para persistir todas las respuestas finales de la sesión
- [x] 4.2 Marcar la `ExamSession` como `Completed` y registrar `CompletedAt` al finalizar el envío
- [x] 4.3 Exponer endpoint público `POST /api/exam/submit`

## 5. API pública

- [x] 5.1 Crear `ExamSessionController` en `api/exam` sin autenticación JWT
- [x] 5.2 Exponer `GET /api/exam/validate/{token}`
- [x] 5.3 Exponer `POST /api/exam/start/{token}` con manejo de error 400 para token inválido o expirado
