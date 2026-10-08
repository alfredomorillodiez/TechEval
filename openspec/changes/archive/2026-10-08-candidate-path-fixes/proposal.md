## Why

El análisis del 08·10·2026 encontró tres defectos en el camino del candidato que la auditoría no había visto. Los tres le cuestan la prueba, o la entrada, a alguien que no ha hecho nada mal:

- **D8.** El reloj empieza al abrir el enlace, no al pulsar «Comenzar prueba». La pantalla pide la sesión al servidor antes de mostrar la bienvenida, y con la sesión nace `StartedAt`. Quien abre el correo para ver de qué va y vuelve pasado el tiempo límite encuentra su prueba enviada en blanco. La spec `candidate-experience` ya pide lo contrario: la sesión se solicita al pulsar «Comenzar prueba».
- **D9.** Una respuesta abierta de más de 4000 caracteres impide enviar la prueba. La columna `UserAnswers.OpenAnswer` es `NVARCHAR(4000)` y nada limita el texto antes de la base de datos. El autoguardado falla con `500` sin que el candidato lo note, y el envío también falla con `500` y se revierte entero.
- **D10.** Algunos candidatos no pueden abrir su invitación. La cuenta de alumno nace con la parte local del correo como `Username`, que tiene índice único. Si ya existe `maria.lopez@gmail.com`, abrir una invitación de `maria.lopez@hotmail.com` da `500` en cada intento, y desde la consola no hay forma de arreglarlo.

## What Changes

- **D8 — La sesión nace al pulsar «Comenzar prueba».** La validación del enlace devuelve además el número de preguntas y el tiempo límite, que es lo que muestra la bienvenida. La pantalla ya no pide la sesión al abrir el enlace: la pide el botón. Quien vuelve a una prueba ya empezada sigue entrando directo a las preguntas, con el tiempo que le quede. La bienvenida avisa de que el tiempo empieza a contar al pulsar el botón y de que cerrar la página no lo para.
- **D9 — La respuesta abierta tiene un máximo de 4000 caracteres.** El servidor rechaza con `400` y un mensaje claro una respuesta más larga, en el autoguardado y en el envío dentro de plazo, antes de escribir nada. El envío fuera de plazo no cambia: descarta lo que traiga y puntúa lo guardado, así que no se rechaza por longitud. El área de texto no admite más de 4000 caracteres y muestra cuántos lleva.
- **D10 — Las cuentas de alumno nuevas nacen sin `Username`.** El alumno no entra con usuario y contraseña: su acceso es el enlace de la invitación. Las cuentas que ya tienen `Username` lo conservan. El índice único está filtrado a los valores no nulos, así que admite cualquier número de cuentas sin nombre de usuario.

Fuera de alcance:

- Subir el límite de 4000 caracteres. Exige cambiar la columna en cada base desplegada.
- El resto de defectos del análisis: D11, D12, S8 y E12.
- Quitar `Username` del modelo, o vaciar el de las cuentas de alumno que ya existen.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `exam-taking`: la validación del enlace devuelve el número de preguntas y el tiempo límite, y no inicia la prueba. Nuevo límite de longitud de la respuesta abierta en el autoguardado y en el envío.
- `candidate-experience`: abrir el enlace no consume tiempo de la prueba. El área de texto de la respuesta abierta limita y cuenta los caracteres.
- `authentication`: la cuenta de alumno aprovisionada nace sin nombre de usuario.

## Impact

- **Dominio**: `UserAnswer.OpenAnswerMaxLength`, el tamaño de la columna. Lo usan la configuración de EF y la validación del servicio.
- **Aplicación**: en `ExamTokenService`, `ValidateTokenAsync` añade el número de preguntas y el tiempo límite; `SaveDraftAnswerAsync` y `SubmitExamAsync` validan la longitud; `GetOrCreateStudentAsync` deja de derivar el `Username`.
- **DTO**: `ExamTokenValidationDto` gana `QuestionCount` y `TimeLimitMinutes` como parámetros opcionales al final, así que las construcciones actuales no cambian. `SubmitAnswerDto` publica el máximo para la Web.
- **API**: sin endpoints nuevos. `answer` y `submit` pueden responder `400` por longitud.
- **Web**: en `TakeExam.razor`, la bienvenida lee la validación, el botón inicia la sesión y el área de texto limita y cuenta los caracteres.
- **Base de datos**: sin cambios de esquema. Solo cambia el comentario de `Users.Username` en `create_database.sql`.
- **Pruebas**: validación que no crea sesión, límite de longitud en el autoguardado y en el envío, y aprovisionamiento sin `Username` con dos correos de la misma parte local.
- **Documentación**: `documentacion.md` deja de decir que el usuario del alumno es la parte local del correo, y corrige el apartado de seguridad que aún dice que esa parte local es su contraseña inicial.
