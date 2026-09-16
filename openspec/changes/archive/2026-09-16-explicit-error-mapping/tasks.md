## 1. Jerarquía

- [x] 1.1 Añadir en `src/TechEval.Application` las cuatro excepciones base: validación, no encontrado, conflicto y prohibido. Verificar que ninguna menciona HTTP ni códigos de estado.
- [x] 1.2 Hacer que las cinco específicas existentes hereden de la que les corresponde. Verificar que las pruebas que afirman sobre sus tipos siguen pasando sin tocarlas.

## 2. Servicios

- [x] 2.1 Sustituir en `ExamService` las dos `InvalidOperationException` de negocio por la base que toque. Verificar que la generación con preguntas insuficientes sigue explicando el motivo.
- [x] 2.2 Sustituir en `QuestionService` las dos de validación de opciones. Verificar con sus pruebas.
- [x] 2.3 Sustituir en `ExamTokenService` las de examen y sesión no encontrados. Verificar que "Token no encontrado" se queda como invariante interna y acaba en 500.
- [x] 2.4 Revisar la de `OpenQuestionReviewService`: "No se pudo recuperar el resultado corregido" es una invariante interna y se queda. Verificar leyendo el método.
- [x] 2.5 Comprobar que no queda ninguna `InvalidOperationException` de negocio en `src/TechEval.Application`. Verificar con una búsqueda.

## 3. Pipeline de errores

- [x] 3.1 Reescribir `ErrorHandlingMiddleware` para mapear solo la jerarquía y responder `ProblemDetails`. Verificar que cualquier otro tipo produce 500 con mensaje genérico.
- [x] 3.2 Comprobar que el mensaje de la excepción original no viaja al cliente en el caso de 500, y que sí queda en el log. Verificar leyendo el middleware.
- [x] 3.3 Quitar los `try/catch` de `QuestionsController`, `ExamSessionController` y `ReviewController`. Verificar que los `ProducesResponseType` se conservan.

## 4. Pruebas

- [x] 4.1 Añadir las pruebas del middleware: cada base produce su código, y un tipo ajeno produce 500 sin filtrar el mensaje. Verificar que pasan.
- [x] 4.2 Ejecutar `dotnet test TechEval.sln` completo. Verificar que las 111 pruebas anteriores siguen en verde sin modificar ninguna.

## 5. Cierre

- [x] 5.1 Reconstruir y arrancar la API. Verificar que el binario es el nuevo.
- [x] 5.2 Comprobar contra la API real el 400 de validación, el 404 de recurso inexistente, el 409 de conflicto y el 403 de prohibido. Verificar cada uno.
- [x] 5.3 Comprobar que el cuerpo de esas respuestas tiene forma de `ProblemDetails`. Verificar una de ellas.
- [x] 5.4 Comprobar que el cliente no se rompe con el cambio de formato del cuerpo de error. **Verificado leyendo `ApiService.PutAsync`, no en el navegador**: usa `EnsureSuccessStatusCode()`, atrapa la excepción y devuelve `null` sin leer el cuerpo, así que `QuestionForm` muestra su aviso de conflicto igual que antes. El formato del cuerpo no le llega.
