## Context

Ver `proposal.md` — Why. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- Todo el cambio vive en `TakeExam.razor`, en `ApiService` y en dos scripts. El servidor ya hace lo que hace falta: `answer` responde `200`, `400`, `403` o `409`, y `submit` responde `200` o un error con mensaje.
- `ApiService.SaveAnswerAsync` devuelve `Task` y no mira la respuesta. Tampoco captura `HttpRequestException`: con la red caída, la excepción sube a `AutoSave`, que corre sin `await` en `SelectAnswer` y en el retardo de la abierta. En Blazor WebAssembly una excepción en una tarea sin observar no se ve en pantalla.
- `SubmitExamAsync` usa `PostAsync`, que devuelve `null` ante cualquier fallo y pierde el código y el mensaje. `TakeExam` pasa a `Completed` con `_result` nulo y la vista lee `_result!`.
- El encabezado ya es `sticky-top`, así que un botón en él está siempre a la vista.
- La Web no tiene proyecto de pruebas. La lógica que importa (qué se reintenta, qué cuenta como respondida) es pequeña y se concentra en una clase pura dentro de la propia página.
- El mapa de preguntas ya existe; solo distingue «respondida» de «no».

## Goals / Non-Goals

**Goals:**

- Que lo que el candidato ve sobre su guardado sea cierto.
- Que ningún fallo de red deje la pantalla rota ni pierda lo escrito.
- No cambiar el contrato de la API.

**Non-Goals:**

- Guardar las marcas en el servidor.
- Un modo sin conexión completo (cola persistente en el navegador).

## Decisions

### D1. `SaveAnswerAsync` devuelve un resultado de tres valores

`enum SaveOutcome { Saved, Rejected, Failed }`.

- `Saved`: éxito.
- `Rejected`: `400`, `403` o `409`. Un reintento no cambia nada. Es la misma regla que ya usa `RecordIntegrityEventAsync`.
- `Failed`: excepción de red, `429`, `5xx`, o cualquier otro código. Se reintenta.

El método nunca lanza: captura `HttpRequestException` y `TaskCanceledException`.

### D2. Un diccionario de pendientes por pregunta, y un reintento por temporizador

`TakeExam` mantiene `_unsaved: HashSet<int>` con las preguntas cuyo último guardado falló. `AutoSave(q)` envía el borrador **actual** de esa pregunta (nunca uno antiguo), de modo que un cambio posterior sustituye al pendiente. Según el resultado:

- `Saved`: quita `q` de `_unsaved` y fija `_lastSavedAt`.
- `Failed`: añade `q` a `_unsaved`.
- `Rejected`: quita `q` de `_unsaved` y no avisa de falta de conexión. El borrador sigue en pantalla.

El temporizador de un segundo que ya existe reintenta, cada 5 segundos, las preguntas de `_unsaved` si hay alguna y no hay otro reintento en vuelo. Reutilizarlo evita un segundo reloj. Al volver la conexión (evento `online`) se reintenta de inmediato.

El estado visible sale de tres datos: `_savesInFlight > 0` → «Guardando…»; `_unsaved.Count > 0` → «Sin guardar»; en otro caso, «Guardado» si hay `_lastSavedAt`, y nada antes del primer guardado.

### D3. La conexión viene del navegador, y un fallo la confirma

`navigator.onLine` y los eventos `online` y `offline` no son fiables del todo: dicen «hay red local», no «llega el servidor». Por eso el aviso fijo sale con **cualquiera** de dos condiciones: el navegador dice `offline`, o `_unsaved` no está vacío. Se apaga cuando se cumplen las dos contrarias.

Un script nuevo `wwwroot/js/exam-ui.js` expone `examUi.registerConnection(ref)` (invoca `OnConnectionChanged(bool)` al cambiar y al registrar) y `examUi.unregisterConnection()`. Se separa de `exam-timer.js`, que trata de visibilidad.

### D4. Las marcas viven en `localStorage`, por sesión

`examUi.getFlags(sessionId)` y `examUi.setFlags(sessionId, ids)` leen y escriben `techeval.flags.<sessionId>` como una lista de enteros. Todo va dentro de `try/catch`: si el almacenamiento falla, devuelve una lista vacía y la marca funciona solo en memoria. La clave incluye el identificador de sesión, que es único por candidato y prueba, así que dos pruebas no se mezclan. No se guardan datos personales ni texto.

Alternativa descartada: guardar la marca en el servidor. Exige columna y endpoint nuevos para un dato que no influye en nada.

### D5. «Finalizar» en el encabezado y diálogo con tres cifras

El botón va a la derecha del temporizador, y se mantiene el de la última pregunta. En pantallas estrechas el encabezado ya admite salto de línea (`flex-wrap`). El diálogo muestra respondidas, sin responder y marcadas. Se calcula con un único método `IsAnswered(draft)`, que usan el contador, la barra, el mapa y el diálogo, para que las cuatro cifras nunca discrepen.

### D6. Un envío fallido vuelve a la prueba

`ApiService.SubmitExamAsync` pasa a devolver `(Receipt, StatusCode, Error)` con `SendAsync`, igual que otras llamadas. En `TakeExam`:

- Éxito: pasa a `Completed`.
- Fallo: `_state` sigue en `InProgress`, `_submitting` vuelve a `false`, `_submitError` lleva el mensaje del servidor o «No se pudo enviar la prueba. Comprueba tu conexión e inténtalo de nuevo.», y el diálogo de confirmación muestra el error. El temporizador **no** se detiene por un fallo.
- Con el tiempo a cero, el tick del temporizador reintenta el envío cada 5 segundos mientras `_submitting` sea `false`.
- Si la respuesta es un `409` sobre una sesión ya enviada o un acuse repetido, el servidor ya devuelve `200`: no hace falta otro caso.

Tras un fallo se vuelven a programar los guardados pendientes: mientras la prueba no se cierre, el autoguardado sigue activo.

## Risks / Trade-offs

- [El indicador «Guardado» podría mentir si el servidor responde `200` y no persiste] → Lo da el propio `200`; no hay confirmación más fuerte sin otro endpoint.
- [Un reintento tardío escribe un borrador viejo sobre uno nuevo] → No ocurre: cada reintento lee el borrador actual de la pregunta.
- [Los eventos `online` y `offline` fallan en algunos navegadores] → El aviso no depende solo de ellos: un guardado fallido lo enciende igual.
- [Se pierden marcas si el candidato abre la prueba en otro navegador] → Aceptado: las marcas son una ayuda local.
- [El reintento cada 5 s puede sumar carga si muchos candidatos pierden la red a la vez] → El límite de ritmo del servidor devuelve `429`, que se trata como `Failed` y espera al siguiente intervalo.

## Migration Plan

Solo Web. Desplegar la Web nueva. Una API anterior es compatible: no cambia el contrato.
