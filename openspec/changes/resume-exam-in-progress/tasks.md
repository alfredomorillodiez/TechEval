## 1. Dominio y contratos

- [ ] 1.1 Renombrar `ExamToken.IsValid` a `CanStart` en `src/TechEval.Domain/Entities/ExamToken.cs`, con un comentario que aclare que responde a "¿se puede empezar?" y no a "¿se puede volver?". Verificar que la compilación falla solo en `QuestionConfiguration.cs` y `ExamTokenService.cs`.
- [ ] 1.2 Actualizar `builder.Ignore(t => t.IsValid)` a `builder.Ignore(t => t.CanStart)` en `src/TechEval.Infrastructure/Data/Configurations/QuestionConfiguration.cs:113`. Verificar que `dotnet build` vuelve a pasar sin avisos.
- [ ] 1.3 Añadir `RemainingSeconds` (int) y `SavedAnswers` (`List<SubmitAnswerDto>`) a `ExamSessionInfoDto` en `src/TechEval.Application/DTOs/ExamSessionDto.cs`. Verificar que la solución compila y que `SubmitAnswerDto` se reutiliza sin crear un record nuevo.
- [ ] 1.4 Añadir a `PendingExamDto` en `src/TechEval.Application/DTOs/StudentPortalDto.cs` una marca del estado de la invitación (sin empezar o a medias). Verificar que compila.

## 2. Validación del token

- [ ] 2.1 Reescribir `ValidateTokenAsync` en `src/TechEval.Application/Services/ExamTokenService.cs:120` para decidir según el estado de la sesión: sin sesión y con `CanStart`, válido; sesión `InProgress`, válido y con `SessionId`; sesión `Completed`, "Este examen ya ha sido completado."; sin sesión y expirado, "El enlace ha expirado."; token inexistente, "Token no válido.". Verificar contra los seis escenarios del requisito "Validación pública de token de examen".
- [ ] 2.2 Comprobar que un token expirado con sesión `InProgress` valida como correcto, y que `ExpiresAt` solo bloquea cuando no hay sesión. Verificar con la prueba del escenario "Token expirado con la sesión todavía en curso".

## 3. Inicio y reanudación de la sesión

- [ ] 3.1 Reescribir la guarda de `StartSessionAsync` en `ExamTokenService.cs:165`: dejar pasar el token que puede empezar y también el que tiene sesión `InProgress`. Verificar que la rama de reanudación de la línea 171 se alcanza, lo que hoy no ocurre.
- [ ] 3.2 Confirmar que la reanudación no toca `StartedAt`, no crea una segunda `ExamSession` y no altera ninguna `UserAnswer`. Verificar con una prueba que llame dos veces a `StartSessionAsync` y compare el identificador de sesión y el recuento de respuestas.
- [ ] 3.3 Rechazar con `null` —que el controlador traduce a 400— el token inexistente, el expirado sin sesión y el que tiene sesión `Completed`. Verificar que no se crea ninguna `ExamSession` en los tres casos.

## 4. Tiempo restante y respuestas guardadas

- [ ] 4.1 Calcular en `BuildSessionInfo` (`ExamTokenService.cs:334`) el tiempo restante como `max(0, (StartedAt + TimeLimitMinutes) - UtcNow)`. Verificar los tres escenarios del requisito "Tiempo restante calculado en el servidor", incluido que nunca devuelve un valor negativo.
- [ ] 4.2 Rellenar `SavedAnswers` en `BuildSessionInfo` desde `ExamSession.UserAnswers`, proyectando solo `QuestionId`, `SelectedAnswerId` y `OpenAnswer`. Verificar que una sesión nueva devuelve la lista vacía y que una reanudada devuelve sus respuestas.
- [ ] 4.3 Comprobar que `ExamTokenRepository.GetWithExamAndSessionAsync` carga las `UserAnswers` que necesita el DTO. Verificar con una prueba de integración o revisando la consulta generada; corregir el `Include` si falta.
- [ ] 4.4 Comprobar que el detalle de sesión no expone `IsCorrect`, `AwardedPoints` ni qué opción es la correcta. Verificar con la prueba del escenario "El detalle de sesión no revela el solucionario".

## 5. Portal del alumno

- [ ] 5.1 Cambiar el filtro de `GetPendingByUserAsync` en `src/TechEval.Infrastructure/Repositories/ExamTokenRepository.cs:31` para incluir los tokens usados cuya sesión está `InProgress`, y excluir los que tienen sesión `Completed`. Verificar con los cuatro escenarios del requisito "Listado de pruebas pendientes del alumno autenticado".
- [ ] 5.2 Rellenar en `StudentPortalService.GetPendingAsync` la marca de estado de cada entrada. Verificar que una prueba sin empezar y una a medias se distinguen en la respuesta.
- [ ] 5.3 Mostrar "Continuar" en lugar de "Comenzar" en `src/TechEval.Web/Pages/Student/Portal.razor` para una prueba a medias. Verificar abriendo `/portal` con una prueba empezada.

## 6. Interfaz de resolución

- [ ] 6.1 En `OnInitializedAsync` de `src/TechEval.Web/Pages/Exam/TakeExam.razor:264`, entrar en estado `InProgress` cuando la validación devuelve `SessionId` con valor, y en `Welcome` cuando llega nulo. Verificar que una recarga durante la prueba vuelve a las preguntas sin pasar por la bienvenida.
- [ ] 6.2 Inicializar `_remainingSeconds` con `session.RemainingSeconds` en lugar de `session.TimeLimitMinutes * 60`. Verificar que una prueba de 60 minutos recargada a los 12 arranca en 48 y no en 60.
- [ ] 6.3 Rellenar `_drafts` con las respuestas de `session.SavedAnswers` al reanudar. Verificar que las respuestas previas aparecen marcadas y que un envío inmediato posterior las conserva.
- [ ] 6.4 Arrancar el temporizador también en la rama de reanudación, no solo desde `StartExam()`. Verificar que el contador corre al volver a la prueba.
- [ ] 6.5 Enviar de inmediato, sin diálogo, cuando el tiempo restante recibido es cero. Verificar con el escenario "Reanudación sin tiempo restante".

## 7. Pruebas

- [ ] 7.1 Añadir `tests/TechEval.Tests/Services/ExamTokenServiceTests.cs` con la validación del token en sus cinco estados: sin sesión, sesión en curso, sesión terminada, expirado sin sesión y expirado con sesión en curso. Verificar que las cinco pruebas pasan.
- [ ] 7.2 Añadir pruebas de reanudación: dos llamadas a `StartSessionAsync` devuelven el mismo `SessionId`, conservan `StartedAt` y no duplican respuestas. Verificar que pasan.
- [ ] 7.3 Añadir pruebas del tiempo restante: sesión nueva, sesión a medias y sesión con el tiempo consumido. Verificar que el valor nunca es negativo.
- [ ] 7.4 Añadir una prueba del listado de pendientes del portal que cubra la prueba a medias y la ya enviada. Verificar que pasan.
- [ ] 7.5 Ejecutar `dotnet test TechEval.sln` completo. Verificar que las 26 pruebas existentes siguen en verde junto a las nuevas.

## 8. Cierre

- [ ] 8.1 Probar el recorrido completo en el navegador: abrir la invitación, empezar, responder tres preguntas, recargar, comprobar el temporizador y las respuestas, y enviar. Verificar que el resultado guardado contiene las tres respuestas.
- [ ] 8.2 Probar el mismo recorrido entrando desde `/portal` en lugar del enlace del correo. Verificar que la acción ofrecida es "Continuar".
- [ ] 8.3 Actualizar `README.md` para documentar la reanudación junto al auto-guardado. Verificar que el texto describe el comportamiento real.
- [ ] 8.4 Añadir a las notas de despliegue la consulta de sesiones abiertas de `design.md` — Migration Plan, y dejar constancia de que API y Web se despliegan juntas. Verificar que la nota existe antes de dar el cambio por terminado.
