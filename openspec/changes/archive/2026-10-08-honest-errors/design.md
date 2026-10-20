## Context

Ver `proposal.md` — Why. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- `ApiService.GetAsync` ya devuelve `null` ante cualquier fallo (HTTP no exitoso o excepción) y una lista, vacía o no, ante un éxito. La información que hace falta ya existe: lo que pierde la distinción es `?? new()` en las pantallas.
- Un `401` con sesión abierta lo trata `SessionExpiryHandler`, que cierra la sesión. Un `401` del login lo excluye expresamente. Las pantallas no tienen que tratar el `401` de sesión.
- `SendAsync<T>` devuelve `(Result, StatusCode, Error)`: el código `0` es una excepción (red), y `Error` extrae `error` o `detail`. El rechazo por límite de ritmo del servidor escribe un `ProblemDetails` con `detail`, así que el mensaje del `429` ya llega en `Error`.
- `ReservationKeeper` recibe `Func<Task<bool>> Renew` y `OnLost`. `Renew` devuelve `StatusCode == 200`, así que un `0` y un `409` dan el mismo `false`.
- Las dos pantallas de corrección (`Review.razor` del evaluador y `ReviewResult.razor` del administrador) pasan `OnLost` a un manejador que fija `_conflict`, y `_conflict` reemplaza el formulario: de ahí se pierde lo escrito.
- `QuestionForm` fija `IsActive = true` en `UpdateQuestionDto`, ignora el resultado de `CreateQuestionAsync` y navega siempre.
- La Web no tiene proyecto de pruebas.

## Goals / Non-Goals

**Goals:**

- Que ninguna pantalla afirme un estado del sistema que no ha podido comprobar.
- Que un fallo transitorio no destruya lo que la persona había escrito.

**Non-Goals:**

- Cambiar la API o sus códigos.
- Reintentos automáticos de las cargas: el usuario pulsa «Reintentar».
- Una infraestructura general de errores (cola de notificaciones, interceptores): aquí es un componente y una convención.

## Decisions

### D1. `LoadError` y la convención `null` = fallo

`Shared/LoadError.razor` recibe `What` (por ejemplo «las correcciones pendientes») y `OnRetry`, y pinta un `alert-warning` con el texto «No se pudo cargar *What*. Comprueba la conexión e inténtalo de nuevo.» y un botón «Reintentar». Con `role="alert"`.

En cada pantalla, el patrón es:

```
var result = await Api.GetXAsync();
_loadFailed = result is null;
if (result is not null) _x = result;
```

y en el marcado, `@if (_loadFailed) { <LoadError .../> } else if (_x.Count == 0) { vacío } else { tabla }`. Reintentar vuelve a ejecutar el método de carga y reinicia `_loading`.

Pantallas: `Evaluator/Queue`, `Evaluator/History`, `Admin/Results/PendingReviewList`, `Student/Portal` (un error por lista, para que una no tape a la otra), `Admin/Results/ResultList`, `Admin/Results/ResultsByExam`, `Admin/Users/UserList`, `Admin/Exams/ExamList`, `Admin/Exams/ExamDetail` (evaluadores y evaluadores asignables), y las categorías de `QuestionForm`, `QuestionList`, `ExamNew` y `GenerateExam`. `QuestionList` ya muestra un error para la lista de preguntas: se unifica con el componente y las categorías se suman. `ExamNew` carga preguntas y categorías: si falla cualquiera, error.

En el portal del alumno, una sola entrada con las dos listas vacías es la peor mentira posible («no tienes pruebas pendientes»), así que cada lista tiene su propio estado.

Alternativa descartada: un `ApiResult<T>` en todas las llamadas. Cambia más de cuarenta firmas para algo que ya expresa `null`.

### D2. Login con `SendAsync`

`ApiService.LoginAsync` pasa a devolver `(AuthResultDto? Result, int StatusCode, string? Error)`. `Login.razor`:

- `Result` presente → como hoy.
- `401` → «Credenciales incorrectas.».
- `429` → `Error` si viene, o «Demasiados intentos. Espera un momento antes de volver a intentarlo.»
- `0` → «No se pudo conectar con el servidor. Comprueba tu conexión.»
- Otro → «El servidor no pudo atender la petición. Inténtalo de nuevo en unos minutos.»

### D3. Renovación con tres resultados y reintento corto

`enum ReservationRenewal { Renewed, Lost, Unreachable }`. `RenewReviewReservationAsync` y `RenewEvaluationReservationAsync` lo devuelven: `200` → `Renewed`; `404` y `409` (y `403`) → `Lost`; todo lo demás → `Unreachable`.

`ReservationKeeper` cambia a `Func<Task<ReservationRenewal>> Renew`, mantiene `OnLost` y suma `EventCallback<bool> OnReachable` (false al fallar, true al recuperarse). El bucle:

- Cada 10 minutos renueva.
- `Lost` → `OnLost` y termina.
- `Unreachable` → `OnReachable(false)` y pasa a reintentar cada 30 segundos.
- `Renewed` tras un fallo → `OnReachable(true)` y vuelve a 10 minutos.

El intervalo corto es 30 s porque la reserva dura 30 minutos y se renueva a los 10: quedan 20 minutos para recuperarse, con margen para 40 intentos.

Las dos pantallas muestran un banner `alert-warning` cuando `_unreachable` es verdadero: «No se pudo renovar tu reserva: comprueba la conexión. Tu corrección sigue aquí.» El formulario no se toca.

### D4. El formulario de preguntas

- `_isActive` se carga con la pregunta (por defecto `true` en un alta) y viaja en `UpdateQuestionDto`. Un interruptor `form-switch` «Activa» lo cambia, y solo en edición. Con `_isActive == false`, un aviso dice «Esta pregunta está desactivada: no entra en pruebas nuevas.»
- `CreateQuestionAsync` y `UpdateQuestionAsync` pasan a `SendAsync` y devuelven `(Result, StatusCode, Error)`. `UpdateQuestionAsync` deja `PutAsync`, que con `EnsureSuccessStatusCode` perdía el cuerpo del error.
- Alta fallida: `_error = error ?? "No se pudo guardar la pregunta."`, `_saving = false` y no se navega.
- Edición fallida: `_error = error`. El texto «da la pregunta de baja y crea una nueva» solo se añade cuando el error menciona respuestas ya registradas. Se detecta con el código `409`, que es el que el servidor usa para ese caso (`AnswerInUseException : ConflictException`).
- Carga fallida en edición: `_loadFailed`, `LoadError` y no se muestra el formulario.
- La baja que falla en `QuestionList` muestra un aviso; la fila sigue.

## Risks / Trade-offs

- [Una pantalla que nadie recordó seguirá con `?? new()`] → La lista del proposal sale de buscar `?? new` en `Pages/` y `Shared/`. La tarea de cierre repite la búsqueda y no deja ninguna sin justificar.
- [El aviso de «sin respuesta» aparece por un fallo muy breve] → Desaparece solo en la siguiente renovación correcta, en 30 segundos como máximo.
- [Mostrar el mensaje del servidor en el `429` expone un texto interno] → Es el `ProblemDetails` que el propio servidor escribe para el cliente, en castellano y sin datos internos.
- [Dos errores a la vez en el portal ocupan espacio] → Es preferible a ocultar uno.

## Migration Plan

Solo Web. Desplegar la Web nueva. Una API anterior es compatible.
