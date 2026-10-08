## Why

Editar una pregunta tipo test que algún candidato ya haya respondido devuelve un error 500. `QuestionService.UpdateAsync` hace `question.Answers.Clear()` y recrea las opciones desde cero. La relación `Question → Answers` es en cascada, así que EF emite un `DELETE` de las opciones antiguas. Pero `UserAnswer.SelectedAnswerId` apunta a `Answer` con `NO ACTION`, y SQL Server bloquea el borrado.

Reproducido el 16·09·2026 contra la base de datos real: un `PUT /api/questions/6` **sin cambiar absolutamente nada** devuelve 500, con `FK_UserAnswers_Answers` en el log.

La consecuencia es incómoda: solo falla en las preguntas que ya se han usado, que son justo las que un administrador querría corregir después de ver los resultados. Una errata detectada gracias a que todos los candidatos fallan una pregunta es hoy inarreglable.

## What Changes

- `UpdateAsync` deja de borrar y recrear. Empareja cada opción recibida con la que ya existe y la actualiza en su sitio. Solo inserta las opciones nuevas y solo borra las que sobran.
- El emparejamiento va por identificador. `UpdateQuestionDto` pasa a llevar el `Id` de cada opción, que la pantalla de edición ya recibe del servidor y hoy descarta. Una opción sin `Id` es nueva.
- Si el cambio exige borrar una opción que algún candidato ya eligió, el sistema lo **rechaza con un mensaje claro** en lugar de fallar contra la base de datos. Ocurre al pasar una pregunta de tipo test a abierta, o al quitar opciones de una pregunta ya usada.
- `QuestionForm.razor` conserva el identificador de cada opción al cargar la pregunta y lo devuelve al guardar.
- El borrado de una opción no referenciada sigue funcionando igual que hoy.

Fuera de alcance:

- **Editar el enunciado de una pregunta ya respondida sigue cambiando el histórico.** La ficha de resultados muestra el texto actual de la pregunta, no el que vio el candidato. Este cambio no lo corrige: hace falta versionar las preguntas, y eso es un cambio mayor. Queda anotado como hallazgo nuevo.
- La corrección del `500` genérico del middleware ante cualquier `InvalidOperationException`.

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities

- `question-bank`: la edición de preguntas no está hoy especificada — el spec cubre creación, filtrado, baja lógica y validación, pero no la actualización. Se **añade** el requisito "Edición de una pregunta sin destruir su histórico de respuestas", con los escenarios de pregunta ya respondida, de opción referenciada que no puede desaparecer, de opción nueva y de opción no referenciada que sí se puede borrar.

## Impact

- **Aplicación**: `QuestionService.UpdateAsync` (línea 70) sustituye `Answers.Clear()` por el emparejamiento por `Id`. Se añade la comprobación de opciones referenciadas y una excepción de dominio para el rechazo. `QuestionDto.cs` — `UpdateQuestionDto` pasa a llevar opciones con `Id` opcional.
- **Dominio**: se necesita saber si una opción ha sido elegida por algún candidato. `IQuestionRepository` gana un método que devuelve los identificadores de opción ya referenciados en `UserAnswers`.
- **Infraestructura**: `QuestionRepository` implementa esa consulta.
- **API**: `QuestionsController.Update` traduce la nueva excepción a `409 Conflict`. Sin cambios de ruta.
- **Web**: `QuestionForm.razor` — `AnswerModel` conserva el `Id`; el guardado lo envía; el error de conflicto se muestra al administrador.
- **Base de datos**: sin cambios. La clave foránea `NO ACTION` se mantiene: es la que protege el histórico, y el código pasa a respetarla en vez de chocar con ella.
- **Pruebas**: `QuestionServiceTests` cubre la validación de opciones, no la edición de una pregunta usada. Se añaden pruebas de los tres casos nuevos.
