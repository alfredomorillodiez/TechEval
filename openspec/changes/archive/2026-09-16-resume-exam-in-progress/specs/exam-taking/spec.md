## MODIFIED Requirements

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

## ADDED Requirements

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
