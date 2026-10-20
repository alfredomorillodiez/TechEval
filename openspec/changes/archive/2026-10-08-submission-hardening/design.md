## Context

Ver `proposal.md` — Why para la motivación. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- `SubmitExamAsync` confirma la transacción y después llama a `SendExamPendingReviewAsync` o a `SendExamResultAsync`, sin capturar nada. `GraphEmailService.SendAsync` registra el fallo en el log y lo relanza. El middleware convierte lo que no hereda de `ApplicationException` en `500`. `ReviewWorkflow` ya captura el fallo del mismo correo con un `catch` vacío y un comentario.
- `SaveDraftAnswerAsync` carga el token con `GetBySessionIdAsync`, que incluye `Exam` y `ExamSession` pero **no** `ExamSession.ExamResult`. Por eso `HasOpenSession`, que mira `ExamResult is null`, no sirve tal cual en ese camino: siempre vería el resultado como nulo. `SubmitExamAsync` ya resuelve la misma duda consultando `_resultRepo`, porque el estado de la sesión y el resultado se escriben juntos pero pueden quedar a medias.
- El autoguardado lee la fila de la pregunta y, si no existe, la inserta. Son dos operaciones sin cerrojo ni índice único: dos peticiones que coinciden leen «no hay fila» y las dos insertan.
- `BaseRepository.AddAsync` añade la entidad al contexto y llama a `SaveChangesAsync`. Si falla, la entidad **sigue rastreada** en estado `Added`. El `DbContext` es uno por petición, así que cualquier `SaveChangesAsync` posterior de esa petición reintentaría insertarla y volvería a fallar.
- El esquema lo crean los guiones de `scripts/`. `UserAnswerConfiguration` no declara índices propios, pero EF crea por convención los de las claves foráneas; el guion no los tiene. `SchemaDriftTests` solo compara nombres de tablas y columnas, no índices.

## Goals / Non-Goals

**Goals:**

- Que el candidato vea el acuse de su envío ya confirmado, pase lo que pase con el correo.
- Que una prueba enviada no vuelva a cambiar.
- Que la base de datos impida por sí misma dos respuestas para la misma pregunta de una sesión.
- Que el guion de actualización nunca borre datos de una base de producción.

**Non-Goals:**

- Garantizar que el correo sale. Eso exige una cola con reintentos.
- Arreglar `SendExamAsync` (token huérfano si Graph falla).
- Deduplicar automáticamente las filas que ya existan.

## Decisions

### D1. El correo del envío se captura, como en la corrección

Se envuelve el envío del correo, en las dos ramas, en un `try` con `catch (Exception ex) when (ex is not OperationCanceledException)` y un comentario que remite a `ReviewWorkflow`. Quien llama recibe el acuse. Se extrae un método privado `TryNotifyAsync(Func<Task>)` para no repetir el bloque en las dos ramas.

Se deja pasar `OperationCanceledException`: si el cliente cancela la petición, no hay nadie a quien responder, y silenciarla ocultaría el cierre del servidor.

Alternativas descartadas:

- **Reenviar el correo en el segundo envío (idempotente).** El segundo envío existe para devolver el acuse sin efectos secundarios; la spec lo exige. Además no hay forma de saber si el correo salió.
- **Una cola o una tabla de correos pendientes.** Es la solución completa, pero exige tabla, trabajador y política de reintentos. Queda para otra propuesta.
- **Registrar el fallo en `ExamResult`.** Cambia el esquema para un dato que hoy está en el log.

### D2. El autoguardado rechaza la sesión ya enviada con `409`

Excepción nueva `ExamAlreadySubmittedException : ConflictException`, junto a `ExamTimeExpiredException`, con el mensaje «La prueba ya se ha enviado.». El `409` lo mapea el middleware sin cambios, y la Web ya ignora los `409` del autoguardado.

En `SaveDraftAnswerAsync` la comprobación va **después** de la de propiedad (un ajeno recibe `403`, no un dato sobre el estado de una sesión ajena) y **antes** de la de plazo y de la de longitud: una sesión enviada no admite nada, tenga el texto el tamaño que tenga.

La sesión se da por enviada si `Status != InProgress` o si existe un `ExamResult` de esa sesión. Se consulta `_resultRepo`, como hace el envío, en lugar de ampliar `GetBySessionIdAsync`: no cambia la forma de lo que devuelve el repositorio para el resto de usuarios.

Alternativas descartadas:

- **Incluir `ExamResult` en `GetBySessionIdAsync` y reutilizar `HasOpenSession`.** Funciona, pero carga la relación en cada autoguardado, que es la ruta más frecuente de la API, y la consulta del resultado es una lectura por índice único.
- **Responder `200` y no escribir.** El cliente no se entera, y el candidato cree que su cambio quedó guardado.

### D3. Índice único, y autoguardado tolerante a la carrera

**Esquema.** En `UserAnswerConfiguration`: `HasIndex(a => new { a.ExamSessionId, a.QuestionId }).IsUnique().HasDatabaseName("IX_UserAnswers_ExamSessionId_QuestionId")` y `HasIndex(a => a.QuestionId).HasDatabaseName("IX_UserAnswers_QuestionId")`. El índice único cubre también las consultas por sesión, así que no hace falta otro sobre `ExamSessionId` solo. Los mismos dos índices van a `create_database.sql`, después del `CREATE TABLE`, y a un guion aditivo.

**Guion aditivo** `scripts/add_useranswers_unique_index.sql`:

- Sigue el patrón de los demás guiones aditivos: `SET QUOTED_IDENTIFIER ON`, `USE TechEvalDb`, comprobaciones `IF NOT EXISTS` sobre `sys.indexes`, y un bloque de vuelta atrás comentado.
- Antes de crear el índice único, busca parejas duplicadas. Si hay alguna, lista `ExamSessionId`, `QuestionId`, el número de filas y los `Id`, y termina con `RAISERROR` de gravedad 16 **sin** crear el índice y sin tocar filas. Decidir cuál conservar corresponde a una persona: las dos filas pueden tener puntos otorgados por un corrector.
- El índice sobre `QuestionId` no puede fallar por datos, así que se crea siempre.

**Autoguardado.** En la rama de inserción de `SaveDraftAnswerAsync`:

```
try { AddAsync(nueva) }
catch (Exception) when (no es cancelación)
{
    releer la fila de esa pareja;
    si existe → actualizarla con el contenido de esta petición;
    si no   → relanzar el fallo original.
}
```

El `catch` no depende de EF: la capa de aplicación no referencia `DbUpdateException`, y el único modo de que una inserción de este tipo funcione al releer es que otra petición la haya hecho. Si no hay fila al releer, el fallo era otro (por ejemplo, la base caída) y se relanza intacto.

**Rastreo de la entidad fallida.** `BaseRepository.AddAsync` desacopla la entidad (`EntityState.Detached`) si `SaveChangesAsync` falla, y relanza. Sin eso, la actualización posterior de la fila releída intentaría insertar de nuevo la entidad fallida y fallaría igual. Solo cambia la ruta de error.

Alternativas descartadas:

- **Capturar `DbUpdateException` y comprobar el número de error 2601 o 2627.** Es más exacto, pero lleva SQL Server a la capa de aplicación o exige una abstracción nueva para un único uso. La relectura distingue el caso por lo que importa: si ya existe la fila.
- **`MERGE` o un procedimiento almacenado de «upsert».** Cambia el patrón de acceso a datos del proyecto para un solo método.
- **Borrar los duplicados en el guion y quedarse con el más reciente.** Pierde datos en producción sin que nadie decida. Un duplicado con puntos otorgados no se puede reconstruir.
- **Un índice único con `IGNORE_DUP_KEY`.** Descarta la segunda fila en silencio: el último texto del candidato podría perderse.

## Risks / Trade-offs

- [El correo del envío puede fallar sin que nadie lo note, porque ya no hay `500`] → El fallo queda en el log de `GraphEmailService`, como el de la corrección. Mientras no haya cola, es una limitación aceptada y conocida. Una cola de reintentos es la propuesta siguiente.
- [Un envío tardío, con la petición ya en vuelo, llega justo después del envío anterior y el autoguardado recibe `409`] → Es lo correcto: el envío ya cerró la sesión. La Web cancela los retardos pendientes al enviar, y ignora los `409`.
- [El guion aditivo se detiene si hay duplicados] → Es la intención. El mensaje lista `Id`s y puntos. Con duplicados reales, una persona borra la fila sobrante y reejecuta el guion.
- [Un autoguardado y el envío coinciden y el envío falla por el índice] → La transacción del envío se revierte y el candidato puede reenviar, como ante cualquier otro fallo de escritura. El rechazo de S8 hace esta ventana muy estrecha.
- [`AddAsync` desacopla la entidad al fallar en cualquier repositorio] → Solo cambia qué queda rastreado tras un fallo, que ya era un estado inconsistente.

## Migration Plan

1. Hacer una copia de la base de datos.
2. Ejecutar `scripts/add_useranswers_unique_index.sql`. Si lista duplicados, resolverlos a mano y reejecutar.
3. Desplegar la API. La API anterior es compatible con la base ya actualizada: solo ve el índice si intenta insertar un duplicado.

Para volver atrás: desplegar la API anterior y, si hace falta, quitar los índices con el bloque comentado del guion.
