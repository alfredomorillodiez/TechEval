## Why

Varias pantallas de la Web muestran un fallo como si fuera otra cosa. Quien usa la aplicación toma decisiones con una información falsa.

- **Listados vacíos que son errores.** Casi todas las pantallas cargan con `await Api.GetXAsync() ?? new()`. Con la API caída, la lista llega vacía y la pantalla dice «No hay correcciones pendientes. Todas las pruebas con preguntas abiertas están corregidas.» Lo mismo pasa en la bandeja del evaluador, el historial, el portal del alumno, resultados, usuarios, pruebas y evaluadores de una prueba.
- **Login.** Una respuesta `429` (demasiados intentos) sale como «Credenciales incorrectas.» El usuario cree que su contraseña es mala, y reintenta, y empeora el bloqueo. Un fallo de red sale igual.
- **Reserva.** `ReservationKeeper` renueva la reserva cada 10 minutos. Si la red falla en esa petición, la renovación devuelve `false` y la pantalla trata la reserva como perdida: reemplaza el formulario por un aviso, y el evaluador pierde lo que había escrito en una corrección larga.
- **Preguntas.**
  - Editar una pregunta desactivada la reactiva sin avisar: el formulario envía `IsActive = true` fijo.
  - Si el alta de una pregunta falla, el formulario ignora la respuesta y navega al listado: la pregunta escrita se pierde sin aviso.
  - Un fallo al editar siempre dice «da la pregunta de baja», aunque la causa sea la red o la sesión.
  - Si no se carga una pregunta para editarla, el formulario se muestra vacío y se puede guardar.
  - Dar de baja una pregunta que falla no dice nada.

## What Changes

- **Un componente de error de carga.** `LoadError` muestra «No se pudo cargar *lo que sea*» con un botón «Reintentar». Todas las pantallas que cargan una lista distinguen «la API devolvió una lista vacía» de «la API falló» y, en el segundo caso, muestran el componente en lugar del vacío. Una lista de verdad vacía sigue mostrando su mensaje.
- **Login con motivo.** Un `401` dice «Credenciales incorrectas.». Un `429` muestra el mensaje del servidor, que dice cuándo reintentar. Un fallo de red dice que no se pudo conectar con el servidor. Cualquier otro error dice que el servidor falló e invita a reintentar.
- **La renovación de la reserva distingue tres resultados.** Renovada; perdida (`404`, `409`: otra persona la tiene o ya no existe); y sin respuesta (red, `429`, `5xx`). Solo «perdida» reemplaza el formulario. «Sin respuesta» deja el formulario con todo lo escrito, muestra un aviso («No se pudo renovar tu reserva: comprueba la conexión. Tu corrección sigue aquí.») y reintenta cada 30 segundos hasta que vuelve a responder.
- **Preguntas honestas.**
  - La edición conserva el estado activo de la pregunta y ofrece un interruptor explícito «Activa». Un aviso dice cuando la pregunta está desactivada.
  - Un alta que falla se queda en el formulario con el error del servidor y todo lo escrito.
  - Un fallo al editar muestra el mensaje real del servidor. La sugerencia de dar de baja solo aparece cuando el servidor dice que es por respuestas ya registradas.
  - Una pregunta que no carga muestra un error y no deja guardar.
  - Una baja que falla muestra un aviso.

Fuera de alcance:

- Cambios en la API. Todo se resuelve en la Web.
- Los listados de la gestión de categorías (la pantalla no existe todavía: es un cambio posterior).
- Buscadores, ordenación y filtros (cambio posterior).

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `admin-console`: error de carga distinto del listado vacío; preguntas que no se pierden ni se reactivan sin avisar.
- `authentication`: el login dice por qué falla.
- `evaluator-review`: la renovación de la reserva no borra la corrección ante un fallo de red.
- `question-bank`: la edición conserva el estado activo.

## Impact

- **Web**: un componente nuevo (`Shared/LoadError.razor`); `ApiService` (`LoginAsync` y los dos métodos de renovación devuelven más información; alta y edición de preguntas devuelven código y mensaje); `Login.razor`; `ReservationKeeper.razor`; `Review.razor` y `ReviewResult.razor`; `QuestionForm.razor`, `QuestionList.razor` y los listados de la lista anterior.
- **API y dominio**: sin cambios.
- **Pruebas**: la Web no tiene proyecto de pruebas. La verificación es un recorrido en el navegador con la API parada, con un `429` simulado y con un corte de red en la renovación.
