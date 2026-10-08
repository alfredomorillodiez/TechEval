## Why

El spec de `open-question-review` afirma: «No existe corrección parcial: o se corrige todo el resultado, o no se modifica nada». Hoy eso solo es cierto frente a una corrección **inválida**, porque la validación ocurre antes de escribir. Frente a un fallo **durante** la escritura, es falso.

`BaseRepository` llama a `SaveChangesAsync` en cada operación. Una corrección de tres respuestas abiertas son cuatro confirmaciones independientes: una por cada `UserAnswer` y otra para el `ExamResult`. Si el proceso cae, la conexión se pierde o el tiempo de espera vence a mitad, quedan puntuaciones escritas y el resultado sin cerrar. El resultado sigue en `PendingReview` con parte de las notas ya puestas, y el corrector no tiene forma de saber cuáles.

El mismo patrón afecta a `SubmitExamAsync`, y allí ya ha costado trabajo real: los arreglos de D1 y D2 tuvieron que **tolerar** la ventana entre la escritura del `ExamResult` y la de `ExamSession.Status`, comprobando en dos sitios distintos que «una sesión con resultado está terminada». Esas comprobaciones son parches sobre un agujero que este cambio cierra.

## What Changes

- Se añade una unidad de trabajo (`IUnitOfWork`) que expone una ejecución transaccional. Envuelve varias escrituras en una sola transacción de base de datos: se confirman todas o ninguna.
- `OpenQuestionReviewService.SubmitReviewAsync` escribe las puntuaciones y cierra el resultado dentro de una transacción. La afirmación del spec pasa a ser cierta también ante un fallo a mitad.
- `ExamTokenService.SubmitExamAsync` escribe las respuestas, el resultado y el estado de la sesión dentro de una transacción. **Desaparece la ventana** que D1 y D2 tuvieron que tolerar.
- El envío de correo queda **fuera** de la transacción, en los dos casos. Es una llamada externa y lenta: mantener una transacción abierta mientras se espera al SMTP bloquea filas sin motivo. La corrección ya estaba diseñada así y no cambia.
- `BaseRepository` **no cambia**: sigue confirmando en cada operación. Dentro de una transacción explícita sobre el mismo `DbContext`, esas confirmaciones participan de ella. Así el alcance del cambio queda en dos métodos y no en todas las rutas de escritura de la aplicación.

Fuera de alcance:

- **No se retira el `SaveChanges` de los repositorios.** Esa es la causa de fondo, y está anotada como hallazgo aparte: hacerlo obliga a revisar cada servicio y cada ruta de escritura, con riesgo real de perder escrituras en un sitio olvidado. Este cambio hace atómicas las dos operaciones que lo necesitan hoy.
- No se retiran las comprobaciones de «sesión con resultado» que introdujeron D1 y D2. Dejan de ser imprescindibles, pero siguen protegiendo de los datos que ya existan en producción con la ventana abierta.

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities

- `open-question-review`: el requisito "Corrección atómica de todas las respuestas abiertas" precisa qué significa atómico. Hoy la palabra solo cubre el rechazo por validación; pasa a cubrir también el fallo durante la escritura, con sus escenarios.
- `exam-taking`: se **añade** el requisito "Atomicidad de la escritura del envío". No se toca "Envío final del examen", que otro cambio pendiente ya modifica.

## Impact

- **Dominio**: `IUnitOfWork` nuevo en `src/TechEval.Domain/Interfaces/Repositories`, junto a los repositorios.
- **Infraestructura**: implementación sobre `AppDbContext.Database.BeginTransactionAsync`. Registro en `DependencyInjection`. El `DbContext` es `Scoped` y los repositorios comparten instancia, así que una transacción abierta sobre él cubre a todos.
- **Aplicación**: `OpenQuestionReviewService.SubmitReviewAsync` y `ExamTokenService.SubmitExamAsync` envuelven sus escrituras. Ambos constructores reciben `IUnitOfWork`.
- **Pruebas**: cuatro montajes de prueba reciben el doble nuevo: `ExamSubmissionTests`, `ExamResubmissionTests`, `ExamTokenServiceTests` y `OpenQuestionReviewServiceTests`. Se añaden pruebas de que un fallo a mitad no deja nada escrito.
- **Base de datos**: sin cambios de esquema.
