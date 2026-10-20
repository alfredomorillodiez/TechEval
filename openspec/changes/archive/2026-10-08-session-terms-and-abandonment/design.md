## Context

Ver `proposal.md` — Why. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- `ExamTokenService` calcula el plazo con `token.Exam.TimeLimitMinutes` en `IsPastDeadline`, `RemainingSecondsOf` y `BuildSessionInfo`, y el veredicto con `exam.PassingScorePercentage` al enviar. `ReviewWorkflow.ApplyAsync` lee `exam.PassingScorePercentage` de la prueba actual. `ExamIntegrityService` usa `token.Exam.TimeLimitMinutes` para el tope de las salidas.
- `ValidateTokenAsync` ya carga el token con la prueba, la sesión y su resultado (`GetWithExamAndSessionAsync`). `ReviewWorkflow` ya tiene `result.ExamSession` cargada con sus respuestas.
- `SessionStatus.Abandoned = 4` existe y `ExamSessions.Status` lo admite: el guion documenta `4=Abandoned`. Ningún código lo escribe ni lo lee.
- `HasOpenSession` exige `InProgress` y sin resultado; `IsSessionFinished` es «hay sesión y no está abierta», así que una abandonada ya se trataría como terminada, pero con el mensaje de «completada».
- `GetPendingByUserAsync` ya filtra por `Status == InProgress`, así que una abandonada sale del portal sola.
- La API no tiene hoy ningún `BackgroundService`: el del pipeline de IA se retiró. El arranque (`Program.cs`) registra los servicios por capas y siembra la base.
- `SubmitExamAsync` solo mira si hay resultado, no el estado de la sesión: una sesión no abierta sin resultado pasaría la comprobación y crearía uno.
- SQL Server no admite fechas de abandono en el modelo, y no hace falta: `CompletedAt` queda nulo para una abandonada, y `Status` es suficiente.

## Goals / Non-Goals

**Goals:**

- Que lo que se le prometió a un candidato al empezar (tiempo y nota) no cambie por una edición posterior.
- Que una sesión muerta deje de parecer viva, sin perder lo que el candidato escribió.

**Non-Goals:**

- Generar resultados automáticos para las vencidas.
- Cambiar el ritmo de la ventana de gracia de 60 segundos.

## Decisions

### D1. Dos columnas nulables y dos métodos de acceso

`ExamSession.TimeLimitMinutesSnapshot` y `PassingScoreSnapshot`, `int?`. Dos métodos en la entidad:

```csharp
public int TimeLimitFor(Exam exam) => TimeLimitMinutesSnapshot ?? exam.TimeLimitMinutes;
public int PassingScoreFor(Exam exam) => PassingScoreSnapshot ?? exam.PassingScorePercentage;
```

Todos los sitios que leían el valor de la prueba pasan por ellos, así que no hay un tercer sitio que se olvide. `StartSessionAsync` rellena las dos columnas al crear la sesión y en ningún otro punto: la reanudación devuelve la sesión existente antes de llegar ahí.

Las sesiones anteriores quedan con nulos y usan los valores de la prueba, que es lo que hacían. No se rellenan con un `UPDATE` de copia: copiar el valor actual de la prueba a sesiones que ya empezaron sería fingir que siempre tuvieron ese valor, y no aporta nada.

**Por qué al empezar y no al invitar.** La invitación no dice cuánto dura ni qué nota hace falta, así que no hay nada prometido en ese momento. Al pulsar «Comenzar prueba» el candidato ve el tiempo y empieza a gastarlo: ahí nace el compromiso. Congelar al invitar, además, dejaría clavado un error que el administrador corrige antes de que nadie empiece.

Alternativa descartada: guardar los valores en `ExamToken`. Sería la invitación y no la sesión la que fija las condiciones, con el problema anterior, y un token puede no llegar a tener sesión.

### D2. El plazo y el veredicto usan los valores de la sesión

- `IsPastDeadline(token, timeLimitMinutes)`: el valor guardado de la sesión manda sobre el argumento y sobre la prueba.
- `BuildSessionInfo` y `RemainingSecondsOf`: `token.ExamSession.TimeLimitFor(token.Exam)`.
- `SubmitExamAsync`: `pct >= session.PassingScoreFor(exam)`.
- `ReviewWorkflow.ApplyAsync`: `result.ExamSession?.PassingScoreFor(exam) ?? exam.PassingScorePercentage`. Si `ExamSession` no está cargada, vuelve al valor de la prueba, que es lo que pasaba.
- `ExamIntegrityService`: el tope de salidas usa el tiempo de la sesión.
- `ValidateTokenAsync` **no** cambia: antes de empezar no hay sesión, y la bienvenida debe mostrar el tiempo actual de la prueba, que es el que se congelará al pulsar el botón.

### D3. El abandono: servicio de aplicación y trabajo de API

`ISessionMaintenanceService.MarkAbandonedAsync(DateTime nowUtc, TimeSpan grace, ct)` devuelve cuántas marcó. La lógica va en Aplicación para poder probarla con repositorios simulados; el `BackgroundService` solo la llama.

- `IExamTokenRepository.GetInProgressAsync` carga los tokens con una sesión `InProgress` (con su prueba y su resultado). Son pocas filas (las sesiones en curso), así que el cálculo del plazo se hace en memoria con las mismas funciones que usa el resto: no se traduce la aritmética de fechas a SQL.
- Se marca una sesión si `ExamResult is null` y `ahora > inicio + TimeLimitFor(prueba) + gracia`. La `gracia` es `AbandonAfterHours`.
- `Status = Abandoned`. `CompletedAt` queda nulo: no se completó.
- Cada sesión se guarda con su propio `UpdateAsync`. Un fallo en una no impide marcar las demás; se captura por sesión y se registra.

`AbandonedSessionWorker : BackgroundService` en la API:

- Lee `Sessions:AbandonAfterHours` y `Sessions:SweepMinutes` (D4).
- Con `AbandonAfterHours` a 0, termina sin hacer nada.
- Espera un minuto tras el arranque (que la API acabe de sembrar y atender) y después usa un `PeriodicTimer`.
- En cada vuelta crea un ámbito, resuelve el servicio y lo llama. Captura toda excepción (menos la cancelación), la registra y sigue con la vuelta siguiente: un fallo de la base de datos no tumba el servidor. Registra cuántas marcó cuando es más de cero.

La carrera con un envío concurrente existe en teoría (el candidato envía justo cuando el trabajo marca), pero solo es posible pasadas `AbandonAfterHours` horas del plazo, y para entonces el envío ya habría sido rechazado por el nuevo estado. No se añade bloqueo.

### D4. Configuración

Una clase `SessionSweepOptions { int AbandonAfterHours = 24; int SweepMinutes = 15; }` enlazada a la sección `Sessions`. Un método `Normalize()` sustituye los negativos de `AbandonAfterHours` por 24 y los menores de 1 de `SweepMinutes` por 15. 0 en `AbandonAfterHours` es el valor que desactiva y se conserva.

### D5. Qué dice y qué rechaza el sistema ante una abandonada

Excepción nueva `ExamAbandonedException : ConflictException` (409) con el mensaje «La prueba se cerró por inactividad.» (más «Pide una invitación nueva» en la validación, donde la lee el candidato).

- `ValidateTokenAsync`: si la sesión es `Abandoned`, mensaje propio antes del de «completada».
- `SaveDraftAnswerAsync`: si la sesión es `Abandoned`, lanza `ExamAbandonedException` (antes de la comprobación de «ya enviada», que sería engañosa).
- `SubmitExamAsync`: tras la comprobación de idempotencia (si hay resultado, se devuelve) y antes de escribir, rechaza una sesión `Abandoned`.
- `StartSessionAsync`: la sesión abandonada tiene token usado y no se puede iniciar; devuelve nulo como cualquier token consumido.

### D6. Lo que ve el administrador

`ExamSummaryDto` gana `int AbandonedCount = 0` al final. `GetAllAsync` lo calcula con los tokens de cada prueba, que `GetWithStatsAsync` ya carga con su sesión. `ExamList` pinta una insignia «N abandonadas» si es mayor que cero. `ExamDetail` añade bajo el tiempo y la nota el texto «Los cambios no afectan a las pruebas ya empezadas.».

## Risks / Trade-offs

- [La primera ejecución marca como abandonadas sesiones antiguas] → Es lo esperado, y la documentación lo dice. Esas sesiones no tenían salida: llevaban semanas «en curso».
- [Un candidato con la pestaña abierta durante días envía y recibe `409`] → Pasadas 24 horas de plazo, la prueba ya es suya de ninguna forma útil. El mensaje le dice qué hacer.
- [Valores guardados que nadie revisa] → Las dos columnas solo se escriben al empezar la sesión, y los métodos de acceso las centralizan.
- [Un `AbandonAfterHours` demasiado corto cierra pruebas de candidatos que vuelven tarde a enviar] → El valor por defecto de 24 horas deja un día entero; es configurable.
- [El trabajo corre en cada instancia si hay varias de la API] → Marcar es idempotente (la segunda ve `Abandoned` y no hace nada).

## Migration Plan

1. Hacer una copia de la base de datos.
2. Ejecutar `scripts/add_session_terms_columns.sql` (aditivo e idempotente).
3. Desplegar la API. La API anterior es compatible con la base ya actualizada.
4. La primera revisión del trabajo marcará las sesiones vencidas hace más de 24 horas. Para revisarlas antes: `SELECT COUNT(*) FROM dbo.ExamSessions WHERE Status = 1`.

Para volver atrás: desplegar la API anterior; las columnas nuevas no le molestan. Las sesiones marcadas como `Abandoned` seguirían así; para reabrirlas hace falta `UPDATE dbo.ExamSessions SET Status = 1 WHERE Status = 4`.
