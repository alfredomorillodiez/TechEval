# Proposal: exam-taking

## Why

Con `exam-delivery` los candidatos ya reciben por email un enlace con un token único hacia su examen. Falta, sin embargo, la pieza que convierte ese enlace en una experiencia de examen real: validar que el token siga siendo utilizable, abrir una sesión de examen ligada a ese token, permitir que el candidato vaya respondiendo pregunta por pregunta sin perder su progreso si cierra el navegador o pierde la conexión, y dejar constancia de que el examen fue enviado para su corrección.

Al tratarse de un flujo público (sin autenticación JWT, accesible solo mediante el token del enlace), necesitamos reglas explícitas de un solo uso por token, expiración y persistencia incremental de respuestas para evitar que un candidato pierda su trabajo o que un enlace se reutilice indebidamente.

## What Changes

- Endpoint público `GET /api/exam/validate/{token}` que verifica que el token exista, no haya expirado y no haya sido usado, devolviendo el título del examen y el nombre del candidato para la pantalla previa al inicio.
- Endpoint público `POST /api/exam/start/{token}` que crea la `ExamSession` asociada al token, marca el token como usado (`IsUsed = true`, `UsedAt`) para invalidarlo para futuros usos, y devuelve el detalle de la sesión (preguntas, tiempo límite, hora de inicio). Si ya existe una sesión en progreso para ese token, la reutiliza en vez de crear una nueva.
- Endpoint público `POST /api/exam/answer/{sessionId}` para auto-guardado: persiste o actualiza la `UserAnswer` de una pregunta concreta dentro de la sesión cada vez que el candidato responde, sin esperar al envío final.
- Endpoint público `POST /api/exam/submit` que recibe el conjunto completo de respuestas de la sesión y marca el examen como enviado (`SessionStatus.Completed`, `CompletedAt`). La corrección automática y el cálculo de la puntuación final quedan fuera del alcance de esta capacidad (ver `exam-results`).
- Modelo de estados de sesión (`SessionStatus`: InProgress, Completed, Expired, Abandoned) para reflejar el ciclo de vida de la sesión de examen.

## Capabilities

### New Capabilities

- `exam-taking`: Flujo público que permite a un candidato validar su token de acceso, iniciar su sesión de examen de un solo uso, ir guardando sus respuestas automáticamente pregunta por pregunta, y enviar el examen completo al finalizar.

### Modified Capabilities

(ninguna)

## Impact

- Nuevas entidades de dominio: `ExamSession`, `UserAnswer`, enum `SessionStatus`.
- Nuevo controlador público `ExamSessionController` (`api/exam/*`), sin autenticación JWT.
- Nuevo servicio de aplicación `ExamTokenService` (interfaz `IExamTokenService`) que orquesta validación de token, inicio de sesión, auto-guardado y envío.
- Depende de `exam-delivery` para la existencia previa del `ExamToken` y su envío por email.
- Sienta la base para `exam-results` (corrección automática y cálculo de puntuación), que consumirá las `UserAnswer` registradas durante esta capacidad.
