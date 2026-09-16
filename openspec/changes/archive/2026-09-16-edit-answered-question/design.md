## Context

Ver `proposal.md` — Why para la motivación y para la reproducción del 16·09·2026.

Lo que condiciona el enfoque:

- `UpdateQuestionDto` lleva hoy `List<CreateAnswerDto>`, y `CreateAnswerDto` es `(Text, IsCorrect, Order)`. **No hay identificador en el contrato de actualización.** Por eso el servicio no tiene con qué emparejar y opta por borrar y recrear.
- La pantalla de edición sí conoce los identificadores: `GetQuestionAsync` devuelve `AnswerDto(Id, Text, IsCorrect, Order)`. `QuestionForm.razor` los descarta al construir su `AnswerModel`.
- `UserAnswer.SelectedAnswerId → Answer` está configurada con `Restrict`, y en la base de datos real es `NO ACTION`. Esa clave foránea es correcta: protege el registro de lo que respondió cada candidato.
- `ErrorHandlingMiddleware` traduce `InvalidOperationException` a 400 con su mensaje. El servicio ya usa esa excepción para sus validaciones de opciones.

## Goals / Non-Goals

**Goals:**

- Que corregir una errata en una pregunta ya usada funcione.
- Que ninguna opción elegida por un candidato desaparezca nunca.
- Que el intento imposible se rechace con una explicación, no con un 500.

**Non-Goals:**

- No se versionan las preguntas. Editar el enunciado sigue cambiando lo que la ficha de resultados muestra como pregunta formulada. Es un problema real y distinto.
- No se toca la clave foránea ni se relaja a `SET NULL`. Perder la referencia sería perder el dato.
- No se revisa el mapeo general de excepciones del middleware.

## Decisions

### Emparejar por identificador, no por posición

`UpdateQuestionDto` pasa a llevar opciones con `Id` opcional: con valor, se actualiza esa opción; sin valor, se crea.

**Alternativa descartada**: emparejar por `Order`. No necesita tocar el contrato, y para una pregunta tipo test con cuatro opciones fijas casi siempre acierta. Pero «casi siempre» es la parte mala: si el administrador reordena las opciones, el emparejamiento por posición asigna a una opción el texto de otra, y las respuestas ya registradas pasan a apuntar a algo que el candidato no eligió. Falla en silencio y corrompe datos. El identificador no se presta a eso.

El coste es un campo más en el DTO y tres líneas en la pantalla de edición, que ya tiene el dato a mano.

### Rechazar el borrado de una opción referenciada, no evitarlo

Cuando el administrador guarda sin una opción que algún candidato eligió, hay tres salidas posibles: borrar igual —imposible, la clave foránea lo impide—, conservarla en silencio, o rechazar.

**Se elige rechazar.** Conservar en silencio dejaría una pregunta que no se parece a lo que el administrador guardó, sin decírselo: creería haberla convertido a respuesta abierta y seguiría siendo tipo test. Un rechazo con motivo es información; un guardado que miente, no.

El rechazo se traduce a `409 Conflict`. Es el mismo criterio que la corrección de abiertas: 409 cuando alguien intenta cambiar algo que el estado del sistema no permite cambiar.

### La comprobación se hace antes de escribir nada

El servicio pregunta primero qué identificadores de opción están referenciados, y decide con esa lista antes de tocar la entidad. Así el rechazo no deja la pregunta a medio editar, que es lo que hoy ocurre: el 500 llega después de haber modificado el enunciado en memoria.

Cuesta una consulta por edición. Es una pantalla de administración y la consulta va sobre una columna indexada.

### El veredicto de una opción sí se puede cambiar

Marcar como correcta otra opción de una pregunta ya respondida se permite, y no recalcula nada. Las notas ya emitidas están congeladas en `UserAnswer.AwardedPoints`, por decisión del cambio de corrección manual. Así que corregir el solucionario afecta a los exámenes futuros y deja en paz a los pasados.

Es lo correcto: recalcular por detrás cambiaría notas ya comunicadas a candidatos.

## Risks / Trade-offs

**El administrador no puede convertir a abierta una pregunta tipo test ya usada** → Es la consecuencia buscada, pero le deja sin salida si de verdad lo necesita. Mitigación: puede dar de baja la pregunta y crear una nueva, que es lo que conserva la coherencia del histórico. El mensaje de rechazo debería sugerirlo.

**Editar el enunciado sigue reescribiendo el pasado** → Este cambio arregla el 500 pero no el problema de fondo: la ficha de resultados muestra el texto actual. Un administrador puede corregir una errata y, sin saberlo, cambiar lo que consta que se preguntó. Queda como hallazgo nuevo, no resuelto aquí.

**Un cliente antiguo que no envíe identificadores** → Sus opciones se tomarían todas por nuevas, y las existentes por eliminadas: el guardado sería rechazado si la pregunta ya se respondió, y recrearía las opciones si no. Sólo hay un cliente y se despliega junto a la API, así que el caso no se da en la práctica. No se añade compatibilidad hacia atrás por algo que no va a ocurrir.

## Migration Plan

Sin cambios de esquema. Sin script SQL. La clave foránea se mantiene como está.

API y Web se despliegan juntas: el contrato de actualización cambia de forma.
