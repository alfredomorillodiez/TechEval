## Why

Dos defectos del ciclo de vida de una prueba, vistos en el análisis del 08·10·2026.

- **Nota de corte y tiempo no se congelan.** `ExamTokenService` calcula el plazo y el tiempo restante con `Exam.TimeLimitMinutes` **en cada llamada**, y la nota de corte con `Exam.PassingScorePercentage` al enviar y al cerrar una corrección. Si un administrador edita la prueba mientras hay candidatos haciéndola o esperando corrección:
  - A quien está resolviendo, el plazo le cambia por debajo: acortar el tiempo puede dejar su prueba fuera de plazo (se envía vacía), y alargarlo regala minutos.
  - Dos candidatos de la misma convocatoria que entregaron con la misma nota reciben veredicto distinto, según corrigió el evaluador antes o después de la edición. Lo mismo pasa con la nota de corte entre el envío de una prueba solo de test y el cierre de otra con abiertas.
- **Nada marca una sesión como `Abandoned`.** El valor existe en `SessionStatus`, pero ningún código lo escribe. Quien abre la prueba y se va, o no vuelve, deja una sesión `InProgress` para siempre: aparece como «en curso» en el portal del alumno, cuenta en la prueba como empezada y nadie sabe que está muerta.

## What Changes

- **Las condiciones de una sesión se congelan al empezar.** `ExamSession` guarda el tiempo límite y la nota de corte vigentes cuando nace (`TimeLimitMinutesSnapshot`, `PassingScoreSnapshot`). A partir de ahí:
  - El plazo, el tiempo restante y la ventana de gracia usan el tiempo guardado.
  - El veredicto, al enviar y al cerrar la corrección de una abierta, usa la nota guardada.
  - Editar la prueba cambia lo que verán las sesiones **nuevas** y no toca las ya empezadas.
  - Las sesiones anteriores al cambio no tienen valores guardados y siguen usando los de la prueba, como hasta ahora.
- **Las sesiones abandonadas se marcan.** Un trabajo en segundo plano de la API marca como `Abandoned` toda sesión `InProgress` sin resultado cuyo plazo venció hace más de `Sessions:AbandonAfterHours` horas (24 por defecto; 0 desactiva el trabajo). Se revisa cada `Sessions:SweepMinutes` minutos (15 por defecto). Una sesión abandonada:
  - Conserva sus respuestas guardadas y no genera resultado.
  - Ya no se puede reanudar: abrir su enlace dice que la prueba se cerró por inactividad y que hay que pedir una invitación nueva.
  - Rechaza el autoguardado y el envío con `409` y ese mensaje.
  - Sale de las pruebas pendientes del alumno.
- **El administrador ve las abandonadas.** La tarjeta de cada prueba muestra cuántas sesiones se abandonaron, y el formulario de edición avisa de que los cambios de tiempo y nota no afectan a las pruebas ya empezadas.

Fuera de alcance:

- Cerrar automáticamente una sesión vencida con lo que tuviera guardado (generar un resultado sin que el candidato envíe). Es otra decisión de negocio.
- Reabrir una sesión abandonada. La salida es una invitación nueva.
- Congelar otros datos de la prueba (título, preguntas).

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `exam-taking`: las condiciones de la sesión se congelan al empezar; las sesiones abandonadas se marcan y se rechazan.
- `exam-management`: editar una prueba no cambia las sesiones ya empezadas; las abandonadas se cuentan en el listado.
- `deployment-ops`: guion aditivo de las dos columnas nuevas y configuración del trabajo.

## Impact

- **Dominio**: `ExamSession.TimeLimitMinutesSnapshot` y `PassingScoreSnapshot` (nulos para sesiones anteriores), con métodos `TimeLimitFor(exam)` y `PassingScoreFor(exam)`.
- **Aplicación**: `ExamTokenService` (inicio, plazo, tiempo restante, veredicto, mensajes y rechazos para abandonadas), `ReviewWorkflow`, `ExamIntegrityService`, `SessionMaintenanceService` nuevo, y `AbandonedCount` en `ExamSummaryDto`.
- **Infraestructura**: `IExamTokenRepository.GetInProgressAsync`.
- **API**: `AbandonedSessionWorker` (servicio en segundo plano) y su configuración. Sin endpoints nuevos. Respuestas `409` con mensaje nuevo.
- **Web**: aviso en la edición de la prueba; contador en la tarjeta.
- **Base de datos**: dos columnas nuevas, nulables, en `ExamSessions`. Guion aditivo `add_session_terms_columns.sql` y `create_database.sql`. **La primera ejecución del trabajo marcará como abandonadas las sesiones que ya llevaban días en curso.**
- **Pruebas**: congelación del plazo y de la nota, sesiones sin valores, el trabajo de abandono con sus umbrales, y los rechazos.
