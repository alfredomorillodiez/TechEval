## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- `ExamTokenService.BuildSessionInfo` construye el detalle de la sesión en cada inicio y en cada reanudación. Ordena las preguntas por `ExamQuestion.Order` y las opciones por `Answer.Order`. Es el único punto donde se decide el orden que ve el candidato.
- La corrección de test ya compara `SelectedAnswerId` con la opción marcada como correcta. No depende de la posición en pantalla.
- Los resultados y la corrección leen de las copias congeladas en `UserAnswer` y ordenan por el examen. Este cambio no toca ese orden.
- `SaveDraftAnswerAsync` ya resuelve la propiedad de la sesión con `GetBySessionIdAsync` y `EnsureOwnedBy`, y el plazo con `IsPastDeadline`. El endpoint de señales reutiliza las dos comprobaciones.
- `exam-timer.js` ya escucha `visibilitychange`, `focus` y `pageshow` para recalcular el reloj. Es el mismo tipo de evento que necesitan las salidas de la página.
- El esquema lo crean `scripts/create_database.sql` y los guiones aditivos. `SchemaDriftTests` falla si el modelo y el guion no coinciden.
- La limitación de ritmo se define en `RateLimiting.cs`, con una ventana fija de un minuto por dirección de origen.
- `UserAnswer.OpenAnswer` tiene una longitud máxima de 4000 caracteres.

## Goals / Non-Goals

**Goals:**

- Que el orden de cada sesión sea estable sin guardar la permutación completa.
- Que una señal no se pueda atribuir a la sesión de otro candidato.
- Que el registro no pueda llenar la base de datos ni degradar la prueba.
- Que el corrector vea las señales en la misma pantalla donde decide.

**Non-Goals:**

- Impedir que el candidato manipule o suprima las señales. Un candidato con las herramientas de desarrollo puede bloquear el endpoint o borrar la marca de agua. La ausencia de señales no prueba nada, y la documentación lo dice.
- Clasificar las señales como sospechosas o no sospechosas. El sistema muestra datos; el corrector decide.

## Decisions

### D1. Semilla por sesión y orden por hash, no permutación guardada

`ExamSession` gana `ShuffleSeed` (`int?`). `StartSessionAsync` la genera con `RandomNumberGenerator.GetInt32` al crear la sesión. `BuildSessionInfo` ordena:

- las preguntas por `SHA-256(semilla ‖ "q" ‖ QuestionId)`;
- las opciones de cada pregunta por `SHA-256(semilla ‖ "a" ‖ QuestionId ‖ AnswerId)`.

El orden es una función pura de la semilla y de los identificadores. La misma sesión da el mismo orden en cada reanudación. Si `ShuffleSeed` es nulo, se usa el orden del examen: así las sesiones abiertas en el momento del despliegue no cambian de orden a mitad de la prueba.

`SessionQuestionDto.Order` pasa a ser la posición en la sesión (1..n). `AnswerOptionDto.Order` pasa a ser la posición en la sesión.

Alternativas descartadas:
- **Guardar la permutación en una tabla o en una columna de texto.** Funciona, pero añade una tabla o un formato que mantener, y hay que decidir qué pasa si el examen cambia de preguntas. La semilla es una columna `int` y no tiene ese problema.
- **`new Random(semilla)` y Fisher-Yates.** La secuencia de `System.Random` con semilla no es un contrato de .NET entre versiones. Una actualización del runtime podría cambiar el orden de una sesión abierta. El hash no depende del runtime.

### D2. Una entidad de señal con columnas tipadas

Nueva entidad `ExamIntegrityEvent`:

| Campo | Tipo | Uso |
|---|---|---|
| `Id` | `int` | clave |
| `ExamSessionId` | `int` | FK a `ExamSessions`, borrado en cascada |
| `Type` | `IntegrityEventType` | `PageLeft`, `PageReturned`, `Paste` |
| `QuestionId` | `int?` | pregunta en pantalla |
| `OccurredAt` | `DateTime` | hora UTC de recepción en el servidor |
| `AwaySeconds` | `int?` | solo en `PageReturned`, informado por el cliente y acotado |
| `PastedChars` | `int?` | solo en `Paste`, acotado a 0..4000 |

Índice por `ExamSessionId`. `ExamSession` gana `IntegrityLimitReached` (`bool`, por defecto `false`).

Alternativa descartada: una columna JSON con los datos de cada tipo. Es más flexible, pero el resumen del corrector necesita sumar duraciones y caracteres, y las columnas tipadas lo hacen sin deserializar.

### D3. El servidor fija la hora; el cliente informa la duración

La hora de la señal es la de recepción. Una hora del cliente se puede falsificar y depende del reloj del equipo. El inconveniente es que una señal reintentada llega con la hora del reintento. Por eso la duración de la ausencia no se calcula restando horas del servidor: la mide el cliente con `performance.now()` y la envía en la señal de vuelta. La interfaz del corrector la presenta como dato medido por el navegador.

### D4. Endpoint de señales en `ExamSessionController`, con su propia política de ritmo

`POST /api/exam/integrity/{sessionId}`, `[Authorize(Roles = "Alumno")]`, cuerpo `IntegrityEventInputDto(Type, QuestionId, AwaySeconds, PastedChars)`.

El servicio hace, en orden:
1. `GetBySessionIdAsync` y `EnsureOwnedBy`: 403 si la sesión es de otro.
2. Si la sesión está `Completed` o tiene `ExamResult`: `409`. Se reutiliza la excepción que ya produce 409 en el auto-guardado.
3. Si el tipo es `Paste`, la pregunta tiene que pertenecer al examen y ser abierta: `400` en otro caso.
4. Si la sesión tiene 500 señales: marca `IntegrityLimitReached` y responde 200 sin guardar.
5. Acota los valores y guarda.

No se rechaza por `IsPastDeadline`. Una señal que llega en los segundos de gracia, o justo antes del envío automático, sigue siendo información útil. El 409 depende del estado de la sesión, no del reloj.

Nueva política `IntegrityPolicy` en `RateLimiting.cs`: 120 peticiones por minuto y dirección de origen. Un candidato que cambia de ventana con rapidez produce dos señales por cambio; 120 deja margen para uso normal y corta un bucle. El cliente trata el 429 como cualquier otro fallo: guarda la señal y la reintenta.

Alternativa descartada: enviar las señales en el cuerpo del auto-guardado o del envío final. Las salidas ocurren sin que el candidato responda nada, y el envío final llega demasiado tarde si el candidato abandona.

### D5. La captura de eventos vive en un guion JavaScript; el envío, en .NET

Nuevo `wwwroot/js/exam-integrity.js`, con `register(dotNetRef)` y `unregister()`, igual que `exam-timer.js`. El guion:

- Mantiene un estado `presente`/`ausente`. Pasa a `ausente` con `visibilitychange` oculto o con `blur` de la ventana, y a `presente` con `visibilitychange` visible o con `focus`. Solo notifica los cambios de estado, así que un `blur` seguido de un `visibilitychange` produce una sola salida.
- Mide la ausencia con `performance.now()`.
- Escucha `paste` en fase de captura sobre el documento. Si el destino es un `textarea` con `data-question-id`, lee `clipboardData.getData('text').length` y notifica la longitud. Nunca pasa el texto a .NET.

`TakeExam.razor` recibe las notificaciones con métodos `[JSInvokable]`, añade el `QuestionId` de la pregunta actual para las salidas, y envía con `ApiService`. Una cola en memoria guarda lo que falla. Cada envío nuevo intenta primero la cola. La cola tiene un tope de 50 señales; si se llena, se descartan las más antiguas.

Alternativa descartada: `navigator.sendBeacon`. No permite la cabecera `Authorization` con el JWT. `fetch` con `keepalive` sí la permite, pero obliga a pasar el JWT al guion. El JWT ya lo gestiona `ApiService`, y no conviene duplicarlo en JavaScript. El coste es que una salida justo antes de cerrar la pestaña se puede perder. Se acepta: la vuelta posterior, si la hay, sí llega.

Alternativa descartada: `@onpaste` de Blazor. `ClipboardEventArgs` no da acceso al contenido, así que no permite saber la longitud.

### D6. Marca de agua como capa CSS con un SVG repetido

Una capa `position: fixed` que cubre la pantalla de preguntas, con `pointer-events: none`, `user-select: none` y `aria-hidden="true"`. El fondo es un SVG en línea con el nombre y el correo rotados unos −30° y opacidad baja, repetido con `background-repeat`. El SVG se genera en el componente con el texto codificado para XML.

`ExamSessionInfoDto` gana `CandidateEmail`, tomado de `ExamToken.CandidateEmail`.

Alternativa descartada: pintar las preguntas en un `canvas` con la marca incrustada. Rompe la accesibilidad y el zoom, y no protege el JSON de la API.

### D7. Lectura para el corrector en un endpoint propio y un componente compartido

`GET /api/results/{id}/integrity`, `[Authorize(Roles = "Admin")]`, en el controlador de resultados. Devuelve `IntegrityReportDto`:

- `Availability`: `NotRecorded` si la sesión no tiene `ShuffleSeed` (anterior al cambio), `Recorded` en otro caso.
- `LimitReached`.
- Resumen: `PageLeftCount`, `TotalAwaySeconds`, `PasteCount`, `TotalPastedChars`, `PastesByQuestion`.
- `Events`, ordenadas por `OccurredAt`.

`ShuffleSeed` nulo es el marcador de "anterior al cambio" porque toda sesión nueva la recibe. Evita una columna más.

Un componente `IntegrityPanel.razor` pinta el resumen y la cronología desplegable. `ResultDetail.razor` y `ReviewResult.razor` lo incluyen. `ReviewResult.razor` muestra además, junto a cada respuesta abierta, los pegados de esa pregunta a partir de `PastesByQuestion`.

Alternativa descartada: añadir las señales a `ExamResultDto` y a `PendingReviewDetailDto`. Obliga a cambiar dos DTO y sus requisitos existentes, y carga las señales en pantallas que no las necesitan, como los listados.

## Risks / Trade-offs

- **[Opciones que dependen de su posición]** Una opción como "Todas las anteriores" pierde sentido al permutar. Los guiones de ejemplo no tienen ninguna, pero el banco de un cliente puede tenerlas. → La documentación de preguntas lo advierte. La marca de orden fijo por pregunta queda como trabajo posterior.
- **[Falsos positivos]** Un candidato sale de la página por motivos legítimos: una notificación, un segundo monitor, las herramientas de accesibilidad. → Las señales no deciden nada, y el aviso y la pantalla del corrector lo dicen.
- **[Supresión de señales]** Un candidato puede bloquear el endpoint. → Aceptado y documentado. La ausencia de señales no es prueba de nada.
- **[Pegado de texto propio]** Un candidato puede cortar y pegar su propio texto dentro del área de respuesta. → El corrector ve la longitud junto a la respuesta y decide.
- **[Protección de datos]** El registro es un tratamiento de datos personales. → El aviso de la bienvenida informa antes de empezar. El sistema guarda longitudes, no textos. Las señales se borran en cascada con la sesión.
- **[El examen sigue completo en el JSON]** La permutación y la marca de agua no impiden leer el JSON de `start`. → Fuera de alcance; la entrega pregunta a pregunta queda como trabajo posterior.

## Migration Plan

1. Guion aditivo `scripts/add_integrity_columns.sql`, idempotente: añade `ExamSessions.ShuffleSeed` (`int NULL`), `ExamSessions.IntegrityLimitReached` (`bit NOT NULL DEFAULT 0`) y la tabla `ExamIntegrityEvents` con su FK en cascada y su índice.
2. `scripts/create_database.sql` recibe las mismas columnas y la tabla.
3. Despliegue de la API y de la web. Las sesiones abiertas en ese momento tienen `ShuffleSeed` nulo y conservan el orden del examen.

Vuelta atrás: la versión anterior de la aplicación ignora las columnas y la tabla nuevas. No hace falta revertir el esquema. Si se quiere, el guion aditivo incluye comentado el bloque inverso.
