## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- `ExamResult` tiene relación uno a uno con `ExamSession` (`ExamSessionConfiguration`). Esa restricción es correcta y se mantiene: es lo que garantiza que una prueba no tenga dos notas.
- `SubmitExamAsync` entra por `_sessionRepo.FindAsync`, que no carga navegaciones. La sesión llega sin su `ExamResult`.
- El cambio `resume-exam-in-progress` ya estableció el criterio "una sesión con resultado está terminada", para cubrir la ventana entre las dos confirmaciones del envío. Este cambio aplica el mismo criterio en el otro extremo del mismo camino.
- `ExamSubmissionReceiptDto` ya distingue el acuse con cifras del acuse pendiente. Reconstruirlo desde un resultado guardado no exige ningún contrato nuevo.

## Goals / Non-Goals

**Goals:**

- Que reenviar la misma prueba nunca produzca un error.
- Que el candidato vea su resultado real, no un fallo, cuando el envío se duplica.
- Que la prueba se corrija una sola vez, con un solo correo.

**Non-Goals:**

- No se hace transaccional el envío. La ventana entre la escritura del resultado y la del estado sigue existiendo; este cambio la tolera en vez de cerrarla.
- No se valida en el servidor que el envío llegue dentro de plazo.
- No se comprueba la propiedad de la sesión en `submit`.

## Decisions

### Devolver el acuse existente, no un 409

**Alternativa descartada**: lanzar una excepción de dominio que la API traduzca a `409 Conflict`, como hace la corrección de abiertas con `AlreadyReviewedException`.

Ahí el 409 es correcto porque quien corrige dos veces intenta **cambiar** algo ya cerrado, y necesita enterarse. Aquí no: el candidato no intenta nada, es su propio navegador quien repite la petición. Un error en pantalla le haría creer que su prueba se perdió, cuando está guardada.

La regla que separa los dos casos: si el reintento no pretende cambiar nada, la respuesta correcta es el estado actual, no un error.

### La detección mira el resultado, no el estado de la sesión

`SubmitExamAsync` consulta `ExamResult` por `ExamSessionId` en lugar de leer `ExamSession.Status`.

Cuesta una consulta más en el camino de envío. A cambio cubre los dos casos con una sola comprobación: la sesión cerrada del todo, y la sesión con resultado escrito cuyo estado no llegó a actualizarse. Mirar `Status` dejaría fuera la segunda, que es justo la que provoca el 500 más difícil de reproducir.

Es además el mismo criterio que usa `HasOpenSession` para decidir si una prueba es reanudable. Dos sitios, una sola definición de "terminada".

### La guarda va después de cargar el examen

El acuse necesita el título del examen, que ya se carga tres líneas más arriba para puntuar. Colocar la guarda antes obligaría a una consulta extra solo para el título.

El coste es que el camino duplicado hace tres consultas antes de salir. Es el camino raro, y a cambio no se duplica ninguna carga.

### El cliente también se guarda de sí mismo

`TakeExam.razor` sale de `SubmitExamAsync` si `_submitting` ya está activo. El servidor ya no rompe, pero una petición que no se envía es mejor que una que se descarta.

## Risks / Trade-offs

**Una consulta más en cada envío** → El envío ya hace cuatro consultas y varias escrituras. Una lectura indexada por `ExamSessionId` no cambia el orden de magnitud. `ExamResults` tiene índice por esa columna a través de la relación uno a uno.

**El segundo envío silencia respuestas nuevas** → Si el candidato enviara respuestas distintas en el segundo intento, se descartan sin avisar. Es lo correcto: la prueba está corregida y aceptar cambios posteriores sería peor. El escenario queda escrito en el spec para que nadie lo tome por un descuido.

**La ventana entre las dos confirmaciones sigue abierta** → Este cambio la tolera en los dos extremos —reanudar y reenviar— pero no la cierra. Mientras `BaseRepository` confirme por operación, seguirá ahí.

## Migration Plan

Sin cambios de esquema. Sin script SQL. Despliegue normal de API y Web.

La guarda del cliente y la del servidor son independientes: cada una funciona sin la otra, así que el orden de despliegue no importa.
