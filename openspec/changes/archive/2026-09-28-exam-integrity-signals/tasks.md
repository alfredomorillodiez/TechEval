## 1. Esquema y dominio

- [x] 1.1 Añadir `ShuffleSeed` (`int?`) e `IntegrityLimitReached` (`bool`) a `src/TechEval.Domain/Entities/ExamSession.cs`, con un comentario que explique que una semilla nula marca una sesión anterior al cambio. Verificar que compila.
- [x] 1.2 Crear el enumerado `IntegrityEventType` (`PageLeft`, `PageReturned`, `Paste`) en `src/TechEval.Domain/Enums/` y la entidad `ExamIntegrityEvent` en `src/TechEval.Domain/Entities/`, con los campos de design.md D2. Añadir la colección `IntegrityEvents` a `ExamSession`. Verificar que compila.
- [x] 1.3 Configurar la entidad en `AppDbContext` o en su fichero de configuración: tabla `ExamIntegrityEvents`, FK a `ExamSessions` con borrado en cascada, índice por `ExamSessionId`, enumerado guardado como entero. Verificar que compila.
- [x] 1.4 Crear `scripts/add_integrity_columns.sql`, idempotente, con las dos columnas y la tabla, y con el bloque inverso comentado. Verificar que se ejecuta dos veces seguidas sin error sobre una base local.
- [x] 1.5 Llevar las mismas columnas y la tabla a `scripts/create_database.sql`. Verificar que `SchemaDriftTests` pasa.

## 2. Orden propio de cada sesión

- [x] 2.1 Generar `ShuffleSeed` con `RandomNumberGenerator.GetInt32` en `StartSessionAsync` al crear la sesión, y solo entonces. Verificar que la reanudación no cambia la semilla.
- [x] 2.2 Escribir la función de orden por hash de design.md D1, para preguntas y para opciones, con la vuelta al orden del examen cuando la semilla es nula. Verificar que es una función pura, sin estado ni dependencia de `System.Random`.
- [x] 2.3 Aplicarla en `BuildSessionInfo` y renumerar `SessionQuestionDto.Order` y `AnswerOptionDto.Order` con la posición en la sesión. Verificar que `SavedAnswersOf` sigue devolviendo las respuestas por su `QuestionId`.
- [x] 2.4 Revisar que la corrección de test, las copias congeladas de `UserAnswer`, el detalle de resultado y la corrección de abiertas no dependen del orden de la sesión. Verificar que siguen ordenando por el examen. Resultado: no ordenaban por el examen sino por `UserAnswer.Id`, el orden de guardado. `ResultService.GetDetailAsync` y `OpenQuestionReviewService.GetDetailAsync` ordenan ahora con `SessionOrder.ByExamOrder`, y sus consultas cargan `Exam.ExamQuestions`.
- [x] 2.5 Pruebas en `tests/TechEval.Tests/Services/`: dos llamadas con la misma semilla dan el mismo orden; semillas distintas dan órdenes distintos en un examen de diez preguntas; semilla nula da el orden del examen; una opción correcta movida a otra posición se corrige como correcta. Verificar que pasan.

## 3. Registro de señales en la API

- [x] 3.1 Crear `IntegrityEventInputDto` e `IntegrityReportDto` con sus tipos de resumen y de evento en `src/TechEval.Application/DTOs/`. Verificar que ningún DTO tiene sitio para el texto pegado.
- [x] 3.2 Crear el servicio de integridad en `src/TechEval.Application/Services/` con el método de registro de design.md D4: propiedad, 409 para sesión terminada o con resultado, 400 para un pegado fuera de una abierta del examen, límite de 500 con `IntegrityLimitReached`, acotado de `AwaySeconds` al tiempo límite y de `PastedChars` a 0..4000, hora UTC del servidor. Registrarlo en la inyección de dependencias. Verificar que compila.
- [x] 3.3 Añadir `IntegrityPolicy` (120 por minuto y dirección de origen) en `src/TechEval.API/RateLimiting.cs`. Verificar que `RateLimitRejectionTests` sigue pasando.
- [x] 3.4 Añadir `POST /api/exam/integrity/{sessionId}` en `ExamSessionController`, con `[Authorize(Roles = "Alumno")]` y la política nueva. Verificar en Swagger que aparece con 200, 400, 403 y 409.
- [x] 3.5 Pruebas del registro: sesión propia en curso guarda; sesión ajena da 403; sesión `Completed` y sesión con resultado dan 409; pegado sobre una pregunta de test da 400; la señal 501 responde con éxito, no se guarda y marca el límite; una duración negativa o excesiva se acota. Verificar que pasan.

## 4. Lectura para el corrector

- [x] 4.1 Añadir al servicio de integridad el método de lectura por resultado: `NotRecorded` si la semilla es nula, resumen, pegados por pregunta, cronología ordenada por hora y marca de límite. Verificar que un resultado inexistente produce 404 a través de `ErrorHandlingMiddleware`.
- [x] 4.2 Añadir `GET /api/results/{id}/integrity` con `[Authorize(Roles = "Admin")]`. Verificar que un alumno recibe 403.
- [x] 4.3 Pruebas de la lectura con el escenario de tres salidas y un pegado de la spec: 3 salidas, 215 segundos, 1 pegado, 820 caracteres, siete señales en orden. Añadir el caso sin señales y el caso anterior al cambio. Verificar que pasan.
- [x] 4.4 Comprobar que el acuse de envío, el portal del alumno y los correos al candidato no incluyen señales. Verificar con una búsqueda de los DTO afectados.

## 5. Interfaz del candidato

- [x] 5.1 Añadir `CandidateEmail` a `ExamSessionInfoDto` y rellenarlo desde `ExamToken.CandidateEmail` en `BuildSessionInfo`. Verificar que compila.
- [x] 5.2 Añadir el aviso de registro a la bienvenida de `src/TechEval.Web/Pages/Exam/TakeExam.razor`, antes del botón "Comenzar prueba", con los cuatro puntos de la spec. Verificar en el navegador.
- [x] 5.3 Añadir la marca de agua de design.md D6 a la pantalla de preguntas, solo en el estado `InProgress`. Verificar en el navegador que los clics y la escritura llegan a los controles, y que el lector de pantalla del sistema no la anuncia. Resultado: comprobado con Playwright que la capa tiene `aria-hidden="true"`, `pointer-events: none` y `user-select: none`, y que un clic sobre una opción llega a la opción. No se ha probado con un lector de pantalla real.
- [x] 5.4 Crear `src/TechEval.Web/wwwroot/js/exam-integrity.js` con el estado presente/ausente, la medida con `performance.now()` y la captura de `paste` sobre `textarea[data-question-id]`, y cargarlo en `index.html`. Verificar que un `blur` seguido de `visibilitychange` produce una sola salida.
- [x] 5.5 Añadir `data-question-id` al `textarea` de respuesta abierta. Registrar y anular el guion en el ciclo de vida del componente, junto a `examTimer`.
- [x] 5.6 Añadir los métodos `[JSInvokable]`, el envío por `ApiService` y la cola en memoria de hasta 50 señales, con reintento antes de cada envío nuevo. Verificar que un fallo de red no muestra ningún mensaje y que la señal llega después.
- [x] 5.7 Probar en el navegador: cambiar de pestaña, cambiar de ventana, pegar en una abierta y recargar la página. Verificar en la base de datos las señales guardadas, y que el orden de preguntas y opciones no cambia al recargar.

## 6. Interfaz del corrector

- [x] 6.1 Crear `src/TechEval.Web/Shared/IntegrityPanel.razor` con el resumen, la cronología desplegable, el texto de duración medida por el navegador y los estados "sin señales", "anterior al registro" y "límite alcanzado".
- [x] 6.2 Incluir el panel en `src/TechEval.Web/Pages/Admin/Results/ResultDetail.razor`. Verificar en el navegador con una prueba hecha en la tarea 5.7.
- [x] 6.3 Incluir el panel en `src/TechEval.Web/Pages/Admin/Results/ReviewResult.razor`, y los pegados de cada pregunta junto a su respuesta abierta. Verificar en el navegador.

## 7. Documentación y cierre

- [x] 7.1 Documentar en `README.md` y en `documentacion.md` las medidas, el guion aditivo nuevo y lo que las medidas no cubren: capturas, consulta del JSON y supresión de señales.
- [x] 7.2 Advertir en la documentación del banco de preguntas que las opciones se presentan en orden aleatorio, y que una opción que depende de su posición deja de tener sentido.
- [x] 7.3 Ejecutar `dotnet build` y `dotnet test` de la solución completa. Verificar que no hay fallos ni avisos nuevos.
- [x] 7.4 Ejecutar `openspec validate exam-integrity-signals --strict`. Verificar que pasa.
