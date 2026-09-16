## Why

**Los endpoints del examen no comprueban de quién es la sesión.** `ExamSessionController` no lleva `[Authorize]` y opera con el `sessionId` que llega en la ruta o en el cuerpo. Con un número entero —secuencial y por tanto adivinable— cualquiera puede llamar a `POST /api/exam/answer/{id}` y sobrescribir las respuestas de otro candidato, o a `POST /api/exam/submit` y cerrarle el examen antes de tiempo. No hace falta ningún token ni ninguna credencial.

La pieza para arreglarlo ya existe y no se usa: `GET /api/exam/validate/{token}` devuelve un JWT con rol `Alumno`, y la interfaz lo guarda. Solo falta exigirlo y comprobar la propiedad.

**El temporizador solo existe en el navegador.** `ExamSession.StartedAt` está en la base de datos, pero el servidor no lo mira al recibir respuestas ni al recibir el envío. Un candidato que detenga el temporizador desde las herramientas del navegador dispone de tiempo ilimitado, y el cambio que calcula el tiempo restante en el servidor se lo muestra pero no se lo impone.

## What Changes

- `POST /api/exam/answer/{sessionId}` y `POST /api/exam/submit` exigen el JWT de alumno que ya emite la validación de la invitación. Siguen sin exigir contraseña: el candidato no tiene y no la necesita.
- Los dos comprueban que la sesión pertenece al usuario del token. Si no, responden `403`. Una sesión cuyo `ExamToken` no tiene `UserId` se trata como ajena: dueño desconocido es dueño distinto.
- `GET /api/exam/validate/{token}` y `POST /api/exam/start/{token}` siguen siendo públicos. Ahí la credencial es el token del enlace, que es lo que el candidato recibe por correo.
- **Guardar una respuesta fuera de plazo se rechaza.** Pasado el tiempo límite del examen, `answer` responde `409` y no escribe nada.
- **Enviar fuera de plazo se acepta, pero se ignora lo que traiga.** El examen se cierra y se puntúa con las respuestas que ya estaban guardadas. Así un candidato que vuelve tarde no pierde su trabajo, y uno que trabaja de más no gana nada por hacerlo.
- El plazo admite un margen de gracia de 60 segundos, para que el auto-envío del temporizador y la latencia de la red no caigan del lado malo.

Fuera de alcance:

- El límite de intentos sobre los endpoints públicos, que es otro hallazgo.
- El cierre automático de las sesiones abandonadas, que siguen abiertas indefinidamente.

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities

- `exam-taking`: se **añaden** los requisitos "Propiedad de la sesión de examen" y "Vigencia del plazo del examen en el servidor". No se tocan los requisitos existentes, que otros cambios pendientes ya modifican.
- `authentication`: el requisito "Autorización por rol Alumno en endpoints del portal" se amplía a los endpoints de resolución del examen.

## Impact

- **Dominio**: `IExamTokenRepository` gana un método que devuelve el token de una sesión con su examen, para resolver dueño y plazo en una consulta.
- **Infraestructura**: `ExamTokenRepository` lo implementa.
- **Aplicación**: `ExamTokenService.SaveDraftAnswerAsync` y `SubmitExamAsync` reciben el identificador del usuario que llama y comprueban propiedad y plazo. Dos excepciones de dominio nuevas.
- **API**: `ExamSessionController` marca `answer` y `submit` con `[Authorize(Roles = "Alumno")]` y traduce las excepciones nuevas a `403` y `409`.
- **Web**: sin cambios. `ApiService` ya envía la cabecera `Authorization` desde que `TakeExam` guarda la sesión que devuelve la validación, y eso ocurre antes de la primera llamada a `answer`.
- **Base de datos**: sin cambios.
- **Pruebas**: se añaden las de propiedad ajena, sesión sin dueño, guardado fuera de plazo y envío fuera de plazo.
