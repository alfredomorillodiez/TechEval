## Why

Hoy un candidato que recarga la página durante su prueba queda fuera de ella de forma definitiva. `ExamToken.IsValid` se define como `!IsUsed && !IsExpired`, y `StartSessionAsync` marca `IsUsed = true` en el momento de crear la sesión. Como la pantalla `/prueba/{Token}` llama primero a `GET /api/exam/validate/{token}`, el candidato recibe `isValid = false` con el mensaje "Este examen ya ha sido completado." aunque su sesión siga en curso y sin enviar.

Un refresco accidental, un cierre de pestaña, una pérdida de red o una batería agotada bastan para perder la prueba. Las respuestas auto-guardadas quedan huérfanas en base de datos y el candidato no puede recuperarlas ni terminar.

El propio spec de `exam-taking` ya obliga a reanudar: el escenario "Reintento de inicio con una sesión ya en progreso" manda devolver la sesión existente. Esa rama existe en el código (`ExamTokenService.cs:171-172`) pero es inalcanzable, porque la comprobación de `IsValid` de la línea 168 la precede. El spec es hoy internamente contradictorio y el código cumple solo la mitad que bloquea.

## What Changes

- El token deja de considerarse agotado por el simple hecho de haber creado una sesión. Pasa a distinguirse entre **sesión en curso** (`InProgress`, reanudable) y **prueba terminada** (`Completed`, cerrada).
- `GET /api/exam/validate/{token}` responde `isValid = true` mientras exista una sesión en estado `InProgress`, y sigue devolviendo "Este examen ya ha sido completado." cuando la sesión está `Completed`.
- `POST /api/exam/start/{token}` reanuda la sesión existente en lugar de rechazarla. La rama de reanudación que hoy es código muerto pasa a ser alcanzable.
- La expiración de la invitación (`ExpiresAt`) deja de aplicarse a una sesión ya iniciada. `ExpiresAt` gobierna **hasta cuándo se puede empezar** la prueba, no hasta cuándo se puede terminarla. Una vez dentro, manda el tiempo límite de la prueba.
- `ExamSessionInfoDto` incorpora `RemainingSeconds`, calculado en el servidor desde `ExamSession.StartedAt` y el tiempo límite del examen. La interfaz deja de inicializar el temporizador con el tiempo completo en cada carga. Sin esto, el arreglo convertiría la recarga en una forma de obtener tiempo extra ilimitado.
- `ExamSessionInfoDto` incorpora las respuestas ya guardadas de la sesión. La interfaz las restaura en sus borradores al reanudar. Sin esto, un envío posterior a la recarga sobrescribiría con valores vacíos las respuestas que el candidato ya había guardado.
- La pantalla `TakeExam.razor` entra directamente en estado de resolución —sin pasar por la bienvenida— cuando la sesión que recibe ya estaba en curso.
- El portal del alumno vuelve a mostrar la prueba en curso. Hoy su listado de pendientes filtra por `IsUsed = false`, así que en cuanto la prueba empieza desaparece de `/portal`. Es el mismo defecto visto desde otra pantalla: un candidato que perdió el correo se queda sin camino de vuelta.

Fuera de alcance, y anotado como trabajo posterior:

- La validación del tiempo límite en el servidor al recibir el envío. Este cambio hace que el tiempo restante lo calcule el servidor, pero el envío tardío se sigue aceptando. Es un hallazgo independiente.
- La comprobación de propiedad de la sesión en `answer` y `submit`. También es independiente y se trata aparte.

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities

- `exam-taking`: el requisito "Validación pública de token de examen" cambia el escenario "Token ya utilizado" — un token con sesión `InProgress` pasa a validar como correcto, y el rechazo queda reservado a la sesión `Completed`. El requisito "Inicio de sesión de examen de un solo uso" conserva su nombre pero cambia de contenido: la unicidad pasa a ser de la **sesión**, no del inicio, y se añaden los escenarios de reanudación tras recarga, de invitación expirada con sesión abierta y de tiempo ya agotado. Se añaden dos requisitos: el tiempo restante calculado en el servidor y la recuperación de las respuestas ya guardadas. El requisito "Envío final del examen" no cambia.
- `candidate-experience`: el requisito "Validación del enlace y pantalla de bienvenida" añade el escenario de reanudación, que entra directamente en las preguntas. El requisito "Inicio del examen con temporizador visible" pasa a inicializar el temporizador con el tiempo restante que devuelve el servidor, no con el tiempo límite completo. Se añade el requisito de restauración de las respuestas ya guardadas.
- `student-portal`: el requisito "Listado de pruebas pendientes del alumno autenticado" deja de excluir las invitaciones con `IsUsed = true` cuya sesión sigue en curso, y marca cada entrada según si está sin empezar o a medias. El requisito "Inicio de una prueba pendiente desde el portal" cubre también la reanudación.

## Impact

- **Dominio**: `ExamToken.cs` — `IsValid` deja de ser la única puerta. Se añade la noción de sesión reanudable, apoyada en `ExamSession.Status`. No hay cambio de esquema en base de datos: `SessionStatus.InProgress` y `ExamSession.StartedAt` ya existen.
- **Aplicación**: `ExamTokenService.cs` — `ValidateTokenAsync` (línea 120) y `StartSessionAsync` (línea 165) cambian su condición de entrada. `BuildSessionInfo` (línea 334) pasa a calcular el tiempo restante y a incluir las respuestas guardadas. `ExamSessionDto.cs` — `ExamSessionInfoDto` gana `RemainingSeconds` y la lista de respuestas guardadas.
- **Infraestructura**: `ExamTokenRepository.GetWithExamAndSessionAsync` ya incluye `ExamSession.UserAnswers`. Conviene comprobar que la carga cubre lo que necesita el nuevo DTO. `ExamTokenRepository.GetPendingByUserAsync` (línea 31) deja de filtrar por `!t.IsUsed` y pasa a admitir también los tokens cuya sesión está `InProgress`.
- **Portal del alumno**: `StudentPortalService.GetPendingAsync` y `PendingExamDto` incorporan el estado de la invitación. `Portal.razor` muestra "Continuar" en lugar de "Comenzar" para una prueba a medias.
- **API**: sin cambios de firma. `ExamSessionController` mantiene sus rutas y su carácter público.
- **Web**: `TakeExam.razor` — `OnInitializedAsync` decide entre bienvenida y reanudación; el temporizador se inicializa con `RemainingSeconds`; los borradores se rellenan con las respuestas recibidas.
- **Pruebas**: `tests/TechEval.Tests` no cubre hoy el ciclo validar → iniciar → recargar → reanudar. Se añaden pruebas de servicio sobre `ExamTokenService` para los cuatro estados del token.
- **Documentación**: `README.md` describe el auto-guardado pero no la reanudación. Se actualiza.
