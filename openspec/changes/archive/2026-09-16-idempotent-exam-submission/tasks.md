## 1. Servidor

- [x] 1.1 Extraer de `SubmitExamAsync` un método que construya el `ExamSubmissionReceiptDto` desde un `ExamResult` ya guardado y el título del examen, respetando que un resultado `PendingReview` viaja sin cifras. Verificar que los dos caminos de salida actuales lo reutilizan y que las pruebas existentes de `ExamSubmissionTests` siguen pasando.
- [x] 1.2 Añadir en `SubmitExamAsync`, tras cargar el examen, la consulta del `ExamResult` de esa sesión y la salida anticipada con su acuse. Verificar que el segundo envío devuelve el mismo `ResultId` que el primero.
- [x] 1.3 Comprobar que la salida anticipada no escribe nada: ni `UserAnswer`, ni `ExamResult`, ni el estado de la sesión, ni correo. Verificar con las comprobaciones de los dobles de prueba.

## 2. Cliente

- [x] 2.1 Salir de `SubmitExamAsync` en `src/TechEval.Web/Pages/Exam/TakeExam.razor` si `_submitting` ya está activo. Verificar que el temporizador no lanza un segundo envío mientras hay uno en vuelo.

## 3. Pruebas

- [x] 3.1 Añadir a `tests/TechEval.Tests/Services` las pruebas del segundo envío: mismo `ResultId`, sin segundo `ExamResult`, sin tocar respuestas y sin correo. Verificar que pasan.
- [x] 3.2 Añadir la prueba del segundo envío con respuestas distintas: el resultado original no cambia. Verificar que pasa.
- [x] 3.3 Añadir la prueba de la sesión a medio cerrar: resultado escrito y estado todavía `InProgress`. Verificar que devuelve el acuse y no intenta crear otro resultado.
- [x] 3.4 Añadir la prueba del acuse repetido de una prueba `PendingReview`: sin puntuación, sin porcentaje y sin veredicto. Verificar que pasa.
- [x] 3.5 Ejecutar `dotnet test TechEval.sln` completo. Verificar que las 51 pruebas anteriores siguen en verde junto a las nuevas.

## 4. Cierre

- [x] 4.1 Probar en el navegador el envío repetido contra la API real. Verificar que la segunda petición responde 200 con el mismo resultado y que no aparece ningún 500 en el log.
- [x] 4.2 Comprobar en base de datos que la sesión reenviada conserva un único `ExamResult`. Verificar con una consulta de conteo.
