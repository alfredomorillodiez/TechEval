## 1. Base común

- [x] 1.1 Crear `Shared/LoadError.razor` (design.md D1). Verificar que la Web compila.
- [x] 1.2 `ApiService`: `LoginAsync`, `CreateQuestionAsync` y `UpdateQuestionAsync` devuelven `(Result, StatusCode, Error)` con `SendAsync`; `enum ReservationRenewal` y los dos métodos de renovación (design.md D2, D3, D4). Verificar que la Web compila y que no queda ningún uso del contrato anterior.

## 2. Listados

- [x] 2.1 Aplicar el patrón de D1 a `Evaluator/Queue`, `Evaluator/History`, `Admin/Results/PendingReviewList` y `Student/Portal`. Verificar en el navegador con la API parada: cada pantalla muestra el error y «Reintentar», no el mensaje de vacío; con la API levantada y sin datos, muestran su mensaje de vacío.
- [x] 2.2 Aplicar el patrón de D1 a `ResultList`, `ResultsByExam`, `UserList`, `ExamList`, `ExamDetail`, `QuestionList` (categorías y lista), `QuestionForm` (categorías), `ExamNew` y `GenerateExam`. Verificar en el navegador con la API parada. Después buscar `?? new` en `Pages/` y `Shared/`: no queda ninguna carga de la API sin tratar.

## 3. Acceso, reserva y preguntas

- [x] 3.1 `Login.razor` con los cuatro mensajes de D2. Verificar en el navegador: contraseña errónea, `429` forzado, API parada y `500` simulado.
- [x] 3.2 `ReservationKeeper` con tres resultados y reintento cada 30 s, y el aviso en `Review.razor` y `ReviewResult.razor` (design.md D3). Verificar en el navegador con un corte simulado de la renovación: el formulario conserva lo escrito, aparece el aviso, al volver la red desaparece; con un `409` simulado sí se reemplaza el formulario.
- [x] 3.3 `QuestionForm` y `QuestionList` según design.md D4. Verificar en el navegador y en la base de datos: editar una pregunta desactivada sin tocar el interruptor la deja desactivada; el alta fallida conserva el formulario; la edición fallida muestra el mensaje del servidor; la pregunta que no carga no deja guardar.

## 4. Cierre

- [x] 4.1 Actualizar `documentacion.md` si describe estos comportamientos. Verificar con una búsqueda.
- [x] 4.2 Ejecutar la batería completa y `openspec validate honest-errors --strict`. Verificar que pasan.
- [x] 4.3 Recorrido final en el navegador integrado contra una base de prueba (nunca `TechEvalDb`) y sin credenciales de correo.
