## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- Hay doce `throw new InvalidOperationException` en el código. Diez son errores de negocio; dos son invariantes internas que no deberían ocurrir nunca.
- Ya existen cinco excepciones específicas —`InvalidReviewException`, `AlreadyReviewedException`, `AnswerInUseException`, `SessionAccessDeniedException`, `ExamTimeExpiredException`— y las pruebas afirman sobre sus tipos.
- Tres controladores atrapan excepciones y eligen el código de estado. El middleware elige otro para los mismos casos.
- `ErrorHandlingMiddleware` ya envuelve todo el pipeline.

## Goals / Non-Goals

**Goals:**

- Que un fallo de infraestructura no salga por la API como error del cliente, ni con su mensaje.
- Que el código de estado de un error se decida en un solo sitio.
- Que las pruebas existentes sigan valiendo sin tocarlas.

**Non-Goals:**

- No se revisa el registro de errores ni se añade identificador de correlación.
- No se cambian los mensajes que ve el candidato.
- No se toca la página de error del cliente.

## Decisions

### Cuatro excepciones base que expresan intención

`ValidationException`, `NotFoundException`, `ConflictException` y `ForbiddenException`. Ni más ni menos: son los cuatro desenlaces que la aplicación necesita distinguir hoy.

**Alternativa descartada**: una sola `DomainException` con una propiedad `StatusCode`. Es menos código, pero mete HTTP dentro de la capa de aplicación, que no sabe nada de HTTP ni debe saberlo. La intención sí es suya; el número no.

**Alternativa descartada**: mapear por nombre de tipo en el middleware. Funciona hasta que alguien renombra una clase.

### Las excepciones específicas se conservan y heredan

`AnswerInUseException` pasa a ser un `ConflictException`, `SessionAccessDeniedException` un `ForbiddenException`, y así con las cinco.

Podrían sustituirse por las bases y ahorrar cinco clases. Se conservan por dos motivos. Uno: su nombre dice qué pasó, no solo cómo termina, y eso vale al leer un `catch` o una prueba. Dos: las pruebas ya afirman sobre esos tipos, y cambiarlas sería tocar lo verificado para acomodar un refactor.

**Corrección sobre la previsión inicial:** este cambio sí obliga a tocar **tres** aserciones. `QuestionServiceTests` y `ExamServiceTests` afirmaban `InvalidOperationException` sobre casos que ahora lanzan `ValidationException`. No es acomodar la prueba al código: esas tres guardaban precisamente el contrato que este cambio corrige, y lo que comprueban —el rechazo y su motivo— no varía. Las cinco excepciones específicas sí conservan sus pruebas intactas, que era el objetivo de hacerlas heredar.

### El `catch` de los controladores desaparece

Con el middleware mapeando la jerarquía, los `try/catch` de los controladores son una segunda fuente de verdad que puede discrepar.

Se quitan. El controlador vuelve a hacer lo suyo: llamar al servicio y devolver el resultado. Los atributos `ProducesResponseType` se quedan, porque siguen describiendo lo que la operación puede responder y alimentan Swagger.

### Las invariantes internas suben a `500`

`"Token no encontrado."` dentro de `SubmitExamAsync` no es un error del cliente: si se llega ahí con una sesión válida, algo está roto en los datos. Se queda como `InvalidOperationException` y acaba en `500`, que es lo que es.

`"Sesión no encontrada."` sí es del cliente, y pasa a `NotFoundException`.

Separar las dos cosas es la mitad del valor de este cambio: hasta ahora ambas salían como `400`.

### `ProblemDetails` en lugar del objeto propio

El formato actual —`{ error, statusCode, timestamp }`— es propio y no lo entiende ninguna herramienta. `ProblemDetails` está normalizado, lo genera ASP.NET Core y lo consumen los clientes sin traducción.

El cliente Blazor no lee hoy el cuerpo de los errores más que para el caso de conflicto al guardar una pregunta, que se apoya en el código de estado y no en el cuerpo. El cambio de formato no lo rompe.

## Risks / Trade-offs

**Un error de negocio que se quede sin migrar pasará a devolver `500`** → Antes devolvía `400` con su mensaje, que al menos era legible. Mitigación: las doce apariciones están inventariadas y se revisan una a una; las pruebas de servicio cubren los casos principales.

**El cliente deja de recibir el mensaje de los errores no migrados** → Es el objetivo para los de infraestructura y un efecto no deseado si quedara alguno de negocio sin migrar. Mismo remedio: el inventario.

**No hay pruebas de API que verifiquen el mapeo** → El proyecto no tiene pruebas de recorrido. Mitigación: se verifica contra la API real cada código de estado, y el hallazgo de las pruebas de recorrido sigue abierto.

**`ProblemDetails` cambia la forma del cuerpo de error** → Cualquier consumidor externo que dependiese del formato anterior se rompería. No hay ninguno: el único cliente es la Web de este repositorio.

## Migration Plan

Sin cambios de esquema. Sin script SQL. Despliegue normal.

API y Web pueden desplegarse por separado: el cliente no depende del cuerpo de los errores.
