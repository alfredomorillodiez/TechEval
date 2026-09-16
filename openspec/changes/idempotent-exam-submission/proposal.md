## Why

Enviar dos veces la misma prueba devuelve un error 500. `SubmitExamAsync` no comprueba si la sesión ya se cerró, y `ExamResult` tiene relación uno a uno con `ExamSession`: el segundo `INSERT` viola la restricción única y la excepción sube sin controlar.

El caso llega solo. El temporizador de `TakeExam.razor` llama a `SubmitExamAsync` al llegar a cero sin mirar `_submitting`, así que basta que el tiempo se agote mientras la petición del candidato está en vuelo. Un reintento de red hace lo mismo.

Para el candidato el efecto es peor que un error técnico: su prueba **sí** se guardó en el primer envío, pero la pantalla le dice que algo falló. No tiene forma de saber si su trabajo llegó.

## What Changes

- `POST /api/exam/submit` pasa a ser idempotente. Si la sesión ya tiene un `ExamResult`, el sistema devuelve el acuse de ese resultado en lugar de intentar crear otro.
- El segundo envío **no** recalcula la nota, no toca las respuestas guardadas y no reenvía ningún correo. La prueba se corrigió una vez y esa corrección es la que vale.
- El acuse reconstruido respeta la regla que ya existe: si el resultado está `PendingReview`, viaja sin cifras ni veredicto.
- La detección se apoya en la existencia del `ExamResult`, no en `ExamSession.Status`. Es el mismo criterio que usa la reanudación desde el cambio `resume-exam-in-progress`, y cubre la ventana en la que el resultado está escrito pero el estado todavía no.
- `TakeExam.razor` deja de enviar dos veces: `SubmitExamAsync` sale de inmediato si ya hay un envío en curso. Es el cinturón que acompaña a los tirantes del servidor.

Fuera de alcance:

- La transaccionalidad del envío. La causa de fondo es que `BaseRepository` confirma en cada operación, lo que deja la ventana entre la escritura del resultado y la del estado. Ese es un hallazgo aparte.
- La validación del tiempo límite en el servidor al recibir el envío.

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities

- `exam-taking`: el requisito "Envío final del examen" pasa a ser idempotente y gana los escenarios de segundo envío, de segundo envío con la sesión a medio cerrar y de no reenvío de correo.
- `candidate-experience`: el requisito "Auto-envío al agotarse el tiempo" deja de disparar un segundo envío cuando ya hay uno en curso.

## Impact

- **Aplicación**: `ExamTokenService.SubmitExamAsync` (línea 220) comprueba el resultado existente antes de puntuar, y devuelve su acuse. Se añade un método que construye el acuse desde un `ExamResult` ya guardado, reutilizado por los dos caminos de salida.
- **Web**: `TakeExam.razor` — `SubmitExamAsync` sale si `_submitting` ya está activo.
- **Base de datos**: sin cambios. La restricción uno a uno entre `ExamResult` y `ExamSession` se mantiene: es la que garantiza que no haya dos resultados, y ahora el código la respeta en vez de chocar con ella.
- **Pruebas**: `ExamSubmissionTests` cubre el envío, no el reenvío. Se añaden pruebas de segundo envío en `tests/TechEval.Tests/Services`.
