## 1. Servidor

- [x] 1.1 Añadir `QuestionCount` y `TimeLimitMinutes` como parámetros opcionales al final de `ExamTokenValidationDto`, y rellenarlos en la respuesta válida de `ValidateTokenAsync` (design.md D1). Verificar con pruebas: la respuesta válida trae el número de preguntas y el tiempo límite; validar dos veces no crea ninguna sesión ni marca el token como usado; el inicio posterior da el tiempo completo.
- [x] 1.2 Crear `UserAnswer.OpenAnswerMaxLength` (4000), usarlo en `UserAnswerConfiguration` y republicarlo como `SubmitAnswerDto.OpenAnswerMaxLength` (design.md D2). Verificar que la solución compila y que `SchemaDriftTests` sigue pasando.
- [x] 1.3 Validar la longitud de la respuesta abierta en `SaveDraftAnswerAsync` y en `SubmitExamAsync`, con el orden de design.md D2. Verificar con pruebas: 4000 caracteres se guardan enteros; 4001 en el auto-guardado lanzan `ValidationException` y no cambian lo guardado; 4001 en un envío dentro de plazo lanzan `ValidationException`, sin resultado, sin cambios en las respuestas y con la sesión en curso; 4001 en un envío fuera de plazo se aceptan y se puntúa lo guardado.
- [x] 1.4 Quitar la derivación del `Username` en `GetOrCreateStudentAsync` (design.md D3). Verificar con pruebas: la cuenta nueva nace con `Username` nulo, y dos correos con la misma parte local en dominios distintos crean dos cuentas válidas.

## 2. Web

- [x] 2.1 En `TakeExam.razor`, pintar la bienvenida con los datos de la validación, pedir la sesión desde «Comenzar prueba» con protección contra el doble clic, mantener la reanudación directa y avisar de que el tiempo empieza al pulsar el botón y de que cerrar la página no lo para (design.md D1). Verificar que la Web compila sin avisos nuevos.
- [x] 2.2 En el área de texto de la respuesta abierta, añadir `maxlength`, el contador «N / 4000» y el aviso al llegar al máximo. Verificar que la Web compila sin avisos nuevos.

## 3. Documentación y cierre

- [x] 3.1 Actualizar `documentacion.md` y el comentario de `Users.Username` en `scripts/create_database.sql`: el alumno nace sin usuario, y la parte local del correo no es ni su usuario ni su contraseña. Revisar `README.md`. Verificar con una búsqueda que no queda ninguna afirmación de que el usuario o la contraseña del alumno salen de la parte local del correo.
- [x] 3.2 Ejecutar la batería completa y `openspec validate candidate-path-fixes --strict`. Verificar que pasan.
- [x] 3.3 Recorrido en local contra una base de prueba (nunca `TechEvalDb`) y sin credenciales de correo. Comprobar en la base de datos, además de en pantalla: abrir un enlace no crea sesión; al volver se ve otra vez la bienvenida con el tiempo completo; «Comenzar prueba» crea la sesión con su `StartedAt`; pegar 5000 caracteres deja 4000 en el área y en `UserAnswers`; la API responde `400` a una respuesta de 4001; las invitaciones de `x@a.test` y `x@b.test` crean dos alumnos con `Username` nulo.
