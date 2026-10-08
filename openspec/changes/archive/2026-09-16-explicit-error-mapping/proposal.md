## Why

`ErrorHandlingMiddleware` traduce **cualquier** `InvalidOperationException` a `400 Bad Request` con su mensaje tal cual. Esa excepción la lanzan los servicios de negocio para sus validaciones, pero también EF Core, `System.Linq` y media biblioteca del ecosistema.

De ahí salen dos problemas a la vez. El cliente recibe como «error tuyo» lo que puede ser un fallo de infraestructura, y recibe además su **mensaje interno**: nombres de entidad, detalles de consulta, cualquier cosa que la biblioteca haya puesto ahí. Un error de conexión a base de datos puede llegar al navegador de un candidato como un 400 con el texto que EF Core decidiera.

La forma correcta ya existe en el propio proyecto y no se generalizó: la corrección de abiertas define `InvalidReviewException` y `AlreadyReviewedException`, y la edición de preguntas define `AnswerInUseException`. Cada una dice lo que es. Lo que falta es que todos los casos de negocio lo hagan, y que el resto sea un `500` genérico.

Hay además una segunda fuente de verdad: tres controladores atrapan esas excepciones y eligen el código de estado por su cuenta. El mismo tipo de error puede acabar mapeado de dos formas según por dónde salga.

## What Changes

- Se introduce una jerarquía corta de excepciones de aplicación que expresan **intención**, no implementación: validación, no encontrado, conflicto y prohibido. Las excepciones específicas que ya existen pasan a heredar de ellas.
- Las `InvalidOperationException` que hoy representan errores de negocio —preguntas insuficientes, respuestas mal formadas, examen no encontrado— se sustituyen por la de la jerarquía que corresponda.
- `ErrorHandlingMiddleware` mapea **solo** esa jerarquía. Todo lo demás pasa a `500` con un mensaje genérico, y el detalle queda en el log.
- El código de estado se decide en **un solo sitio**. Los tres controladores que hoy atrapan excepciones dejan de hacerlo, así que el mismo error produce siempre la misma respuesta salga por donde salga.
- Las respuestas de error adoptan el formato `ProblemDetails` de la norma, en lugar del objeto propio `{ error, statusCode, timestamp }`.

Fuera de alcance:

- La revisión del resto del pipeline de errores: registro estructurado, identificador de correlación, página de error del cliente.
- Los mensajes de error que ve el candidato, que se quedan como están.

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities

- `project-architecture`: se **añade** el requisito "Los errores se traducen a HTTP en un solo sitio", con el mapeo por intención y el cierre del `500` genérico.

## Impact

- **Aplicación**: fichero nuevo con la jerarquía. `ExamService`, `ExamTokenService`, `QuestionService` y `OpenQuestionReviewService` sustituyen sus `InvalidOperationException` de negocio. Las excepciones específicas pasan a heredar de la base que les toca.
- **API**: `ErrorHandlingMiddleware` mapea por tipo y responde `ProblemDetails`. `QuestionsController` y `ExamSessionController` y `ReviewController` pierden sus bloques `try/catch`.
- **Web**: `QuestionForm.razor` interpreta hoy el fallo de guardado como conflicto de opción en uso. Sigue funcionando, porque el código de estado no cambia.
- **Pruebas**: las que afirman sobre el tipo de excepción siguen valiendo, porque los tipos específicos se conservan. Se añaden las del mapeo.
