## 1. Consulta de dueño y plazo

- [x] 1.1 Añadir a `IExamTokenRepository` un método que devuelva el `ExamToken` de una sesión, con su examen y su sesión cargados. Verificar que compila.
- [x] 1.2 Implementarlo en `ExamTokenRepository` con los `Include` necesarios para resolver `UserId`, `StartedAt` y `TimeLimitMinutes`. Verificar con una prueba de repositorio contra EF en memoria.

## 2. Excepciones y servicio

- [x] 2.1 Añadir dos excepciones de dominio: una para la sesión ajena y otra para el plazo agotado, junto a las que ya existen en el proyecto. Verificar que compila.
- [x] 2.2 `SaveDraftAnswerAsync` recibe el identificador del usuario que llama, resuelve la sesión y rechaza si el dueño no coincide o si el `UserId` del token es nulo. Verificar con sus pruebas.
- [x] 2.3 `SaveDraftAnswerAsync` rechaza además el guardado fuera de plazo, con el margen de gracia de 60 segundos. Verificar que no escribe nada en ese caso.
- [x] 2.4 `SubmitExamAsync` recibe el identificador del usuario y rechaza la sesión ajena antes de puntuar nada. Verificar que no crea `ExamResult` ni cierra la sesión.
- [x] 2.5 `SubmitExamAsync` detecta el envío fuera de plazo y cierra el examen con las respuestas ya guardadas, sin escribir las que trae el cuerpo. Verificar con su prueba.
- [x] 2.6 Declarar el margen de gracia en un solo sitio, con nombre, para que no aparezca un 60 suelto en dos métodos. Verificar leyendo el servicio.

## 3. API

- [x] 3.1 Marcar `answer` y `submit` con `[Authorize(Roles = "Alumno")]` en `ExamSessionController`, dejando `validate` y `start` públicos. Verificar que los dos primeros responden 401 sin cabecera.
- [x] 3.2 Pasar el identificador del usuario desde el token a las dos llamadas del servicio. Verificar que el candidato legítimo sigue pudiendo resolver su prueba.
- [x] 3.3 Traducir la excepción de sesión ajena a `403` y la de plazo agotado a `409`. Verificar que ninguna llega como error interno.

## 4. Pruebas

- [x] 4.1 Añadir la prueba de guardado sobre la sesión de otro candidato: se rechaza y no escribe nada. Verificar que pasa.
- [x] 4.2 Añadir la prueba de envío sobre la sesión de otro candidato: se rechaza sin crear resultado. Verificar que pasa.
- [x] 4.3 Añadir la prueba de sesión cuyo token no tiene `UserId`: se rechaza. Verificar que pasa.
- [x] 4.4 Añadir la prueba de guardado fuera de plazo y la de guardado dentro del margen de gracia. Verificar que la primera rechaza y la segunda escribe.
- [x] 4.5 Añadir la prueba de envío fuera de plazo: cierra con lo ya guardado e ignora el cuerpo. Verificar que pasa.
- [x] 4.6 Ejecutar `dotnet test TechEval.sln` completo. Verificar que las 92 pruebas anteriores siguen en verde junto a las nuevas.

## 5. Cierre

- [x] 5.1 Reconstruir la solución completa antes de arrancar la API, por lo dicho en el hallazgo E8. Verificar que el binario es el nuevo.
- [x] 5.2 Comprobar contra la API real que `answer` y `submit` sin cabecera `Authorization` responden 401. Verificar que antes respondían 200.
- [x] 5.3 Comprobar contra la API real que el JWT de un candidato no sirve para escribir en la sesión de otro. Verificar que responde 403.
- [x] 5.4 Comprobar en el navegador que el recorrido completo del candidato sigue funcionando: abrir la invitación, responder, recargar y enviar. Verificar que no aparece ningún 401 ni 403.
- [x] 5.5 Comprobar contra la API real que un guardado con la sesión fuera de plazo responde 409. Verificar envejeciendo `StartedAt` en base de datos.
