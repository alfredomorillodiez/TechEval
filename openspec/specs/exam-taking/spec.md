# exam-taking Specification

## Purpose
TBD - created by archiving change exam-taking. Update Purpose after archive.

## Requirements

### Requirement: Validación pública de token de examen
El sistema SHALL exponer un endpoint público (sin autenticación) que permita comprobar si un token de examen es válido antes de mostrar la interfaz de examen al candidato. La validez del token SHALL depender del estado de su sesión de examen, no del hecho de haberla creado: un token cuya sesión sigue en curso SHALL validar como correcto, y solo una sesión ya terminada SHALL cerrar el acceso.

#### Scenario: Token válido
- **GIVEN** un `ExamToken` existente que no ha expirado y que no tiene ninguna `ExamSession` asociada
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = true`, el título del examen y el nombre del candidato asociado al token
- **AND** el sistema no devuelve ningún identificador de sesión

#### Scenario: Token con sesión en curso
- **GIVEN** un `ExamToken` con una `ExamSession` asociada en estado `InProgress`
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = true`, el título del examen, el nombre del candidato y el identificador de la sesión en curso
- **AND** el sistema NO responde con el mensaje "Este examen ya ha sido completado."

#### Scenario: Token inexistente
- **GIVEN** un valor de token que no corresponde a ningún `ExamToken` almacenado
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = false` y el mensaje "Token no válido."

#### Scenario: Token expirado
- **GIVEN** un `ExamToken` cuya fecha de expiración (`ExpiresAt`) es anterior al momento actual y que no tiene ninguna `ExamSession` asociada
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = false` y el mensaje "El enlace ha expirado."

#### Scenario: Token expirado con la sesión todavía en curso
- **GIVEN** un `ExamToken` cuya fecha de expiración es anterior al momento actual, pero cuya `ExamSession` está en estado `InProgress`
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = true` y el identificador de la sesión en curso
- **AND** el sistema trata `ExpiresAt` como el plazo para empezar la prueba, no como el plazo para terminarla

#### Scenario: Token ya utilizado
- **GIVEN** un `ExamToken` marcado como `IsUsed = true` cuya `ExamSession` está en estado `Completed`
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = false` y el mensaje "Este examen ya ha sido completado."
- **AND** el rechazo se apoya en el estado de la sesión, no en `IsUsed` por sí solo

#### Scenario: Sesión en curso que ya tiene resultado
- **GIVEN** una `ExamSession` en estado `InProgress` que sin embargo ya tiene un `ExamResult` asociado, porque el envío se interrumpió entre la escritura del resultado y la del estado
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = false` y el mensaje "Este examen ya ha sido completado."
- **AND** una sesión con resultado NUNCA se considera reanudable, sea cual sea su estado

### Requirement: Inicio de sesión de examen de un solo uso
El sistema SHALL crear como máximo una `ExamSession` por cada `ExamToken`. Al iniciar la sesión, el sistema MUST marcar el token como usado para impedir que genere una segunda sesión. Mientras esa sesión siga en estado `InProgress`, el sistema SHALL devolverla al candidato que vuelve, en lugar de rechazarlo o de crear otra.

#### Scenario: Inicio exitoso de una nueva sesión
- **GIVEN** un `ExamToken` válido (no expirado, no usado) sin sesión de examen previa
- **WHEN** el candidato solicita `POST /api/exam/start/{token}`
- **THEN** el sistema crea una nueva `ExamSession` en estado `InProgress` asociada a ese token
- **AND** el sistema marca el `ExamToken` como usado (`IsUsed = true`) y registra `UsedAt` con la fecha/hora actual
- **AND** el sistema devuelve el detalle de la sesión: identificador de sesión, título del examen, nombre del candidato, tiempo límite en minutos, hora de inicio, tiempo restante y el listado ordenado de preguntas con sus opciones de respuesta

#### Scenario: Reintento de inicio con una sesión ya en progreso
- **GIVEN** un `ExamToken` que ya tiene una `ExamSession` en estado `InProgress` (por ejemplo, el candidato recargó la página, cerró la pestaña o perdió la conexión)
- **WHEN** el candidato solicita `POST /api/exam/start/{token}` de nuevo
- **THEN** el sistema SHALL devolver el detalle de la sesión existente, con su identificador original, en lugar de crear una nueva sesión
- **AND** el sistema SHALL NOT modificar `StartedAt` ni reiniciar el cómputo del tiempo
- **AND** el sistema SHALL NOT crear ninguna `UserAnswer` ni alterar las ya guardadas

#### Scenario: Reanudación de una sesión cuyo tiempo ya se agotó
- **GIVEN** un `ExamToken` con una `ExamSession` en estado `InProgress` cuyo `StartedAt` más el tiempo límite del examen es anterior al momento actual
- **WHEN** el candidato solicita `POST /api/exam/start/{token}`
- **THEN** el sistema devuelve el detalle de la sesión con un tiempo restante de cero
- **AND** el sistema no amplía el plazo ni reinicia el temporizador

#### Scenario: Reanudación de una sesión cuya invitación ha expirado
- **GIVEN** un `ExamToken` con `ExpiresAt` anterior al momento actual y una `ExamSession` en estado `InProgress`
- **WHEN** el candidato solicita `POST /api/exam/start/{token}`
- **THEN** el sistema devuelve el detalle de la sesión existente
- **AND** la expiración de la invitación no interrumpe una prueba ya empezada

#### Scenario: Intento de reanudar una sesión que ya tiene resultado
- **GIVEN** una `ExamSession` en estado `InProgress` con un `ExamResult` ya asociado
- **WHEN** el candidato solicita `POST /api/exam/start/{token}`
- **THEN** el sistema MUST rechazar la operación, sin devolver la sesión y sin crear otra
- **AND** así se impide un segundo `ExamResult` sobre la misma sesión, que la relación uno a uno no admite

#### Scenario: Intento de iniciar sesión con token inválido, expirado o ya usado
- **GIVEN** un token que no existe, un token expirado sin sesión asociada, o un token cuya sesión está en estado `Completed`
- **WHEN** el candidato solicita `POST /api/exam/start/{token}`
- **THEN** el sistema MUST rechazar la operación con un error 400 y el mensaje "Token inválido o expirado." sin crear ninguna `ExamSession`

### Requirement: Auto-guardado de respuestas individuales
El sistema SHALL permitir guardar la respuesta de una pregunta concreta dentro de una sesión de examen en curso, de forma incremental, sin requerir el envío completo del examen. La fecha de respuesta SHALL registrarse siempre en tiempo universal coordinado (UTC), igual que el resto de marcas de tiempo del modelo.

#### Scenario: Primera respuesta a una pregunta
- **GIVEN** una `ExamSession` en progreso sin respuesta previa registrada para una pregunta determinada
- **WHEN** el candidato envía `POST /api/exam/answer/{sessionId}` con el identificador de la pregunta y la respuesta seleccionada u abierta
- **THEN** el sistema crea una nueva `UserAnswer` asociada a esa sesión y pregunta con la respuesta proporcionada

#### Scenario: Corrección de una respuesta ya guardada
- **GIVEN** una `ExamSession` en progreso con una `UserAnswer` ya registrada para una pregunta determinada
- **WHEN** el candidato envía `POST /api/exam/answer/{sessionId}` de nuevo para la misma pregunta con una respuesta distinta
- **THEN** el sistema SHALL actualizar la `UserAnswer` existente (respuesta seleccionada u abierta y fecha de respuesta) en lugar de crear un duplicado

#### Scenario: La hora de la respuesta no queda por delante de la de la sesión
- **GIVEN** un servidor cuyo huso horario local está adelantado respecto a UTC
- **WHEN** un candidato responde a una pregunta dentro de una sesión iniciada momentos antes
- **THEN** el sistema SHALL registrar `AnsweredAt` en UTC, de forma que nunca resulte anterior a `StartedAt` ni posterior al cierre de la sesión por efecto del huso horario

### Requirement: Envío final del examen
El sistema SHALL permitir al candidato enviar el conjunto completo de respuestas de su sesión para marcarla como finalizada, dejando la corrección automática y el cálculo de puntuación a cargo de otra capacidad. El envío SHALL ser idempotente: una sesión que ya tiene resultado devuelve ese resultado en lugar de crear otro, sin recalcular la nota ni reenviar ninguna notificación.

#### Scenario: Envío exitoso del examen
- **GIVEN** una `ExamSession` en progreso
- **WHEN** el candidato envía `POST /api/exam/submit` con el identificador de sesión y el conjunto de respuestas
- **THEN** el sistema persiste cada respuesta enviada (creando o actualizando la `UserAnswer` correspondiente a cada pregunta)
- **AND** el sistema marca la sesión como `Completed` y registra `CompletedAt` con la fecha/hora actual

#### Scenario: Segundo envío de la misma sesión
- **GIVEN** una `ExamSession` que ya tiene un `ExamResult` asociado
- **WHEN** se solicita `POST /api/exam/submit` de nuevo con ese identificador de sesión
- **THEN** el sistema responde `200 OK` con el acuse del resultado ya guardado
- **AND** el sistema SHALL NOT crear un segundo `ExamResult`
- **AND** el sistema SHALL NOT modificar la nota, el veredicto ni ninguna `UserAnswer`

#### Scenario: Segundo envío con respuestas distintas
- **GIVEN** una `ExamSession` que ya tiene un `ExamResult` asociado
- **WHEN** se solicita `POST /api/exam/submit` con un conjunto de respuestas diferente al del primer envío
- **THEN** el sistema devuelve el acuse del resultado original sin alterarlo
- **AND** la prueba se corrige una sola vez: el primer envío es el que cuenta

#### Scenario: Segundo envío sobre una sesión a medio cerrar
- **GIVEN** una `ExamSession` con `ExamResult` asociado pero todavía marcada `InProgress`, porque el envío se interrumpió entre la escritura del resultado y la del estado
- **WHEN** se solicita `POST /api/exam/submit` con ese identificador de sesión
- **THEN** el sistema devuelve el acuse del resultado ya guardado
- **AND** la detección se apoya en la existencia del resultado, no en el estado de la sesión

#### Scenario: El segundo envío no reenvía correo al candidato
- **GIVEN** una `ExamSession` que ya tiene resultado y cuyo correo de resultado o de acuse ya se envió
- **WHEN** se solicita `POST /api/exam/submit` de nuevo
- **THEN** el sistema SHALL NOT enviar ningún correo adicional al candidato

#### Scenario: El acuse repetido de una prueba pendiente sigue sin cifras
- **GIVEN** una `ExamSession` cuyo `ExamResult` está en estado `PendingReview`
- **WHEN** se solicita `POST /api/exam/submit` de nuevo
- **THEN** el acuse devuelto lleva el estado pendiente y SHALL NOT incluir puntuación, porcentaje ni veredicto

#### Scenario: Envío referenciando una sesión inexistente
- **GIVEN** un identificador de sesión que no corresponde a ninguna `ExamSession` almacenada
- **WHEN** se solicita `POST /api/exam/submit` con ese identificador
- **THEN** el sistema MUST rechazar la operación indicando que la sesión no fue encontrada, sin registrar ningún resultado

### Requirement: Tiempo restante calculado en el servidor
El detalle de sesión que devuelve `POST /api/exam/start/{token}` SHALL incluir el tiempo restante en segundos, calculado en el servidor a partir de `ExamSession.StartedAt` y del tiempo límite del examen. El cliente SHALL NOT ser la fuente de verdad del tiempo transcurrido: recargar la página no puede devolver al candidato el tiempo completo.

#### Scenario: Primera entrada en la sesión
- **WHEN** el candidato inicia una sesión nueva
- **THEN** el tiempo restante devuelto equivale al tiempo límite del examen expresado en segundos

#### Scenario: Reanudación a mitad de la prueba
- **GIVEN** una `ExamSession` en curso iniciada hace 12 minutos, de un examen con 60 minutos de tiempo límite
- **WHEN** el candidato reanuda la sesión
- **THEN** el tiempo restante devuelto es de 48 minutos expresados en segundos, con una tolerancia de unos pocos segundos

#### Scenario: Reanudación con el tiempo consumido
- **GIVEN** una `ExamSession` en curso cuyo tiempo límite ya ha transcurrido por completo
- **WHEN** el candidato reanuda la sesión
- **THEN** el tiempo restante devuelto es cero, y nunca un valor negativo

### Requirement: Recuperación de las respuestas ya guardadas
El detalle de sesión que devuelve `POST /api/exam/start/{token}` SHALL incluir las respuestas que el candidato ya tenía guardadas en esa sesión, con la pregunta a la que corresponden y la opción seleccionada o el texto abierto introducido. Sin ellas, un envío posterior a la reanudación sobrescribiría con valores vacíos el trabajo ya guardado.

#### Scenario: Sesión nueva sin respuestas
- **WHEN** el candidato inicia una sesión recién creada
- **THEN** el detalle de sesión incluye una lista de respuestas guardadas vacía

#### Scenario: Reanudación con respuestas previas
- **GIVEN** una `ExamSession` en curso con tres respuestas ya guardadas por auto-guardado
- **WHEN** el candidato reanuda la sesión
- **THEN** el detalle de sesión incluye esas tres respuestas, cada una con su identificador de pregunta y su contenido
- **AND** las preguntas sin responder no aparecen en esa lista

#### Scenario: El detalle de sesión no revela el solucionario
- **WHEN** el detalle de sesión incluye las respuestas ya guardadas
- **THEN** el sistema SHALL NOT indicar si alguna de ellas es correcta, ni incluir la puntuación otorgada, ni marcar cuál de las opciones de cada pregunta es la correcta

### Requirement: Atomicidad de la escritura del envío
El sistema SHALL escribir en una sola transacción de base de datos todo lo que produce el envío de una prueba: las `UserAnswer` de cada pregunta, el `ExamResult` con su puntuación y el cierre de la `ExamSession`. Si cualquiera de esas escrituras falla, ninguna SHALL quedar persistida. El envío del correo al candidato SHALL quedar fuera de esa transacción, porque es una llamada externa y mantener la transacción abierta mientras se espera bloquearía filas sin motivo.

#### Scenario: Envío correcto
- **GIVEN** una sesión en progreso con sus respuestas
- **WHEN** el candidato envía la prueba y todas las escrituras tienen éxito
- **THEN** quedan persistidas las respuestas, el resultado y el cierre de la sesión
- **AND** el correo se envía después de confirmar la transacción

#### Scenario: Fallo al escribir el resultado
- **GIVEN** una sesión en progreso con sus respuestas ya puntuadas en memoria
- **WHEN** la escritura del `ExamResult` falla
- **THEN** el sistema SHALL revertir también las `UserAnswer` escritas en ese envío
- **AND** la sesión sigue en `InProgress`, sin resultado asociado
- **AND** el candidato puede reanudar y volver a enviar

#### Scenario: Fallo al cerrar la sesión
- **GIVEN** un envío en el que el `ExamResult` ya se ha escrito dentro de la transacción
- **WHEN** falla la actualización de `ExamSession.Status`
- **THEN** el sistema SHALL revertir también el `ExamResult`
- **AND** no queda ninguna sesión con resultado y estado `InProgress` a la vez

#### Scenario: El fallo del correo no revierte el envío
- **GIVEN** un envío cuya transacción ya se ha confirmado
- **WHEN** falla el envío del correo al candidato
- **THEN** el resultado SHALL seguir persistido, porque el correo queda fuera de la transacción

### Requirement: Propiedad de la sesión de examen
El sistema SHALL exigir el JWT de alumno que emite la validación de la invitación para guardar respuestas (`POST /api/exam/answer/{sessionId}`) y para enviar la prueba (`POST /api/exam/submit`), y MUST comprobar que la sesión referida pertenece al usuario de ese token antes de escribir nada.

Los identificadores de sesión son secuenciales y por tanto adivinables, así que el identificador por sí solo NEVER SHALL bastar como prueba de propiedad.

Una sesión cuyo `ExamToken` no tiene `UserId` SHALL tratarse como ajena: un dueño desconocido no es el que llama.

La validación del token (`GET /api/exam/validate/{token}`) y el inicio de la sesión (`POST /api/exam/start/{token}`) SHALL seguir siendo públicos, porque ahí la credencial es el token del enlace que el candidato recibe por correo.

#### Scenario: El candidato opera sobre su propia sesión
- **GIVEN** un candidato autenticado con el JWT que le devolvió la validación de su invitación
- **WHEN** guarda una respuesta o envía la prueba de su propia sesión
- **THEN** el sistema procesa la operación con normalidad

#### Scenario: Petición sin token a un endpoint de resolución
- **WHEN** se envía `POST /api/exam/answer/{sessionId}` o `POST /api/exam/submit` sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized` sin escribir nada

#### Scenario: Intento de escribir en la sesión de otro candidato
- **GIVEN** dos candidatos con sesiones distintas, y el JWT válido del primero
- **WHEN** el primero envía `POST /api/exam/answer/{sessionId}` con el identificador de sesión del segundo
- **THEN** el sistema MUST responder `403 Forbidden`
- **AND** el sistema SHALL NOT crear ni modificar ninguna `UserAnswer` de esa sesión

#### Scenario: Intento de cerrar el examen de otro candidato
- **GIVEN** el JWT válido de un candidato
- **WHEN** envía `POST /api/exam/submit` con el identificador de sesión de otro
- **THEN** el sistema MUST responder `403 Forbidden`
- **AND** el sistema SHALL NOT crear ningún `ExamResult` ni cerrar esa sesión

#### Scenario: Sesión sin dueño registrado
- **GIVEN** una `ExamSession` cuyo `ExamToken` tiene `UserId` nulo
- **WHEN** cualquier candidato autenticado intenta operar sobre ella
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Token de administrador contra un endpoint de resolución
- **GIVEN** un JWT válido de un usuario con `IsAdmin = true`
- **WHEN** se envía una petición a `answer` o `submit` con ese token
- **THEN** el sistema MUST responder `403 Forbidden`, porque el rol exigido es `Alumno`

### Requirement: Vigencia del plazo del examen en el servidor
El sistema MUST comprobar en el servidor que el plazo del examen sigue vigente, calculado desde `ExamSession.StartedAt` más el tiempo límite del examen, con un margen de gracia de 60 segundos que absorbe la latencia de la red y el auto-envío del temporizador. El reloj del cliente NEVER SHALL ser la autoridad sobre el tiempo transcurrido.

Fuera de plazo, guardar una respuesta SHALL rechazarse y enviar la prueba SHALL aceptarse ignorando lo que traiga: el examen se cierra y se puntúa con las respuestas que ya estaban guardadas.

#### Scenario: Guardado de respuesta dentro de plazo
- **GIVEN** una sesión cuyo tiempo límite no se ha agotado
- **WHEN** el candidato guarda una respuesta
- **THEN** el sistema la persiste con normalidad

#### Scenario: Guardado de respuesta fuera de plazo
- **GIVEN** una sesión cuyo tiempo límite se agotó hace más del margen de gracia
- **WHEN** el candidato guarda una respuesta
- **THEN** el sistema MUST responder `409 Conflict` y SHALL NOT escribir ni crear ninguna `UserAnswer`

#### Scenario: Guardado dentro del margen de gracia
- **GIVEN** una sesión cuyo tiempo límite se agotó hace menos de 60 segundos
- **WHEN** el candidato guarda una respuesta
- **THEN** el sistema la persiste, porque el margen absorbe la latencia de una petición legítima ya en vuelo

#### Scenario: Envío fuera de plazo se cierra con lo ya guardado
- **GIVEN** una sesión cuyo tiempo límite se agotó hace más del margen de gracia, con tres respuestas guardadas antes de agotarse
- **WHEN** el candidato envía la prueba, incluyendo en el cuerpo respuestas distintas o adicionales
- **THEN** el sistema SHALL cerrar el examen y puntuarlo con las tres respuestas ya guardadas
- **AND** el sistema SHALL NOT escribir las respuestas que trae el envío
- **AND** el candidato que vuelve tarde no pierde su trabajo, y el que trabaja de más no gana nada por ello

#### Scenario: Envío dentro de plazo
- **GIVEN** una sesión cuyo tiempo límite sigue vigente
- **WHEN** el candidato envía la prueba
- **THEN** el sistema escribe las respuestas del envío y puntúa con ellas, como hasta ahora

### Requirement: Congelación de lo preguntado en el momento del envío
Al cerrar el envío de un examen, el sistema SHALL guardar en cada `UserAnswer` una copia del enunciado de la pregunta, del texto de la opción elegida, del texto de la opción correcta y de los puntos máximos de la pregunta, tal como estaban en ese momento. Las consultas de resultados y de corrección SHALL leer esa copia y no la pregunta actual, de forma que editar una pregunta más adelante no altere lo que consta en los exámenes ya cerrados.

#### Scenario: Editar el enunciado no cambia un resultado ya cerrado
- **GIVEN** un examen cerrado cuya ficha de resultados muestra el enunciado de una pregunta
- **WHEN** un administrador edita el texto de esa pregunta en el banco
- **THEN** la ficha del resultado SHALL seguir mostrando el enunciado que se le formuló al candidato

#### Scenario: Editar las opciones no cambia lo que consta que se eligió
- **GIVEN** un examen cerrado en el que el candidato eligió una opción de una pregunta de test
- **WHEN** un administrador edita el texto de esa opción, o marca como correcta una opción distinta
- **THEN** la ficha del resultado SHALL seguir mostrando el texto de la opción que el candidato eligió y el de la que era correcta entonces

#### Scenario: Editar los puntos no descuadra la nota mostrada
- **GIVEN** un examen cerrado en el que una pregunta valía diez puntos
- **WHEN** un administrador cambia el valor de esa pregunta a cinco puntos
- **THEN** la ficha del resultado SHALL seguir mostrando diez como máximo de esa pregunta, de forma que los puntos otorgados nunca superen el máximo que se muestra

#### Scenario: Respuestas anteriores al cambio
- **GIVEN** respuestas registradas antes de que existiera esta copia
- **WHEN** un administrador consulta su ficha de resultados
- **THEN** el sistema SHALL mostrar el texto con el que se rellenaron esas filas al aplicar el cambio, que es el mismo que mostraba antes

### Requirement: Límite de ritmo en la validación del enlace de examen
El sistema SHALL limitar el número de peticiones a `GET /api/exam/validate/{token}` por dirección de origen dentro de una ventana de tiempo, y SHALL responder `429 Too Many Requests` con `ProblemDetails` y cabecera `Retry-After` al superarlo.

El límite SHALL ser más permisivo que el del inicio de sesión: el endpoint es anónimo por diseño —la credencial es el token del enlace— y un candidato legítimo lo llama varias veces al recargar la página o al volver a su prueba.

#### Scenario: Un candidato que recarga su enlace no queda bloqueado
- **GIVEN** un candidato que abre su invitación y recarga la página varias veces seguidas
- **WHEN** cada recarga solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema SHALL atender todas ellas, porque el límite deja margen para el uso normal

#### Scenario: Recorrido masivo de tokens
- **GIVEN** una dirección de origen que solicita validaciones de tokens distintos sin pausa
- **WHEN** supera el cupo de la ventana
- **THEN** el sistema SHALL responder `429 Too Many Requests`, de forma que recorrer el espacio de tokens deje de ser barato
