## 1. Contrato y consulta

- [x] 1.1 Añadir en `src/TechEval.Application/DTOs/QuestionDto.cs` un record para las opciones de actualización con `Id` opcional, y usarlo en `UpdateQuestionDto`. Verificar que la solución compila y que `CreateQuestionDto` sigue con su record sin identificador.
- [x] 1.2 Añadir a `IQuestionRepository` un método que devuelva los identificadores de `Answer` ya referenciados en `UserAnswers` para una pregunta dada. Verificar que compila.
- [x] 1.3 Implementarlo en `QuestionRepository` con una consulta sobre `UserAnswers` filtrada por `QuestionId`. Verificar con una prueba de repositorio contra EF en memoria.

## 2. Servicio

- [x] 2.1 Añadir una excepción de dominio para el conflicto de opción referenciada, junto a las que ya existen en el proyecto. Verificar que compila.
- [x] 2.2 Reescribir `QuestionService.UpdateAsync` (línea 70): consultar las opciones referenciadas, emparejar por `Id`, actualizar en su sitio, insertar las nuevas y borrar solo las que sobran y no están referenciadas. Verificar que `Answers.Clear()` desaparece del método.
- [x] 2.3 Rechazar con la excepción nueva si alguna opción a eliminar está referenciada, **antes** de modificar la entidad. Verificar que una edición rechazada no cambia ni el enunciado ni las demás opciones.
- [x] 2.4 Comprobar que cambiar cuál es la opción correcta funciona sobre una pregunta ya respondida, y que no toca ningún `AwardedPoints`. Verificar con su prueba.

## 3. API y pantalla

- [x] 3.1 Traducir la excepción nueva a `409 Conflict` en `QuestionsController.Update`. Verificar que la respuesta lleva el mensaje y no un error interno.
- [x] 3.2 Conservar el `Id` de cada opción en `AnswerModel` de `src/TechEval.Web/Pages/Admin/Questions/QuestionForm.razor` al cargar la pregunta, y enviarlo al guardar. Verificar que una edición desde la pantalla conserva los identificadores.
- [x] 3.3 Mostrar el mensaje de conflicto al administrador en lugar de un fallo genérico. Verificar provocando el caso desde la pantalla.

## 4. Pruebas

- [x] 4.1 Añadir la prueba de edición de una pregunta ya respondida: se completa y no se emite ningún borrado de opciones. Verificar que pasa.
- [x] 4.2 Añadir la prueba de cambio de opción correcta sobre una pregunta respondida. Verificar que pasa.
- [x] 4.3 Añadir la prueba de opción nueva añadida y de opción no referenciada eliminada. Verificar que pasan.
- [x] 4.4 Añadir la prueba del rechazo por opción referenciada: excepción de conflicto y entidad sin tocar. Verificar que pasa.
- [x] 4.5 Ejecutar `dotnet test TechEval.sln` completo. Verificar que las 59 pruebas anteriores siguen en verde junto a las nuevas.

## 5. Cierre

- [x] 5.1 Reconstruir la solución completa antes de arrancar la API, por lo dicho en el hallazgo E8. Verificar que el binario es el nuevo.
- [x] 5.2 Repetir contra la API real el `PUT` sobre la pregunta 6, que hoy devuelve 500. Verificar que responde 200 y que la pregunta queda editada.
- [x] 5.3 Provocar el caso de conflicto contra la API real convirtiendo a abierta una pregunta ya respondida. Verificar que responde 409 con mensaje, no 500.
- [x] 5.4 Comprobar en base de datos que los identificadores de las opciones de la pregunta 6 no han cambiado y que sus `UserAnswers` siguen apuntando a ellos. Verificar con una consulta.
