## MODIFIED Requirements

### Requirement: Validación pública de token de examen
El sistema SHALL exponer un endpoint público (sin autenticación) que permita comprobar si un token de examen es válido antes de mostrar la interfaz de examen al candidato. La validez del token SHALL depender del estado de su sesión de examen, no del hecho de haberla creado: un token cuya sesión sigue en curso SHALL validar como correcto, y solo una sesión ya terminada SHALL cerrar el acceso.

Una respuesta válida SHALL incluir el número de preguntas de la prueba y su tiempo límite en minutos, que es lo que el candidato ve antes de empezar. Validar SHALL NOT crear ninguna `ExamSession`, SHALL NOT marcar el token como usado y SHALL NOT poner en marcha el tiempo de la prueba: eso ocurre solo al iniciar la sesión.

#### Scenario: Token válido
- **GIVEN** un `ExamToken` existente que no ha expirado y que no tiene ninguna `ExamSession` asociada
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = true`, el título del examen, el nombre del candidato asociado al token, el número de preguntas y el tiempo límite en minutos
- **AND** el sistema no devuelve ningún identificador de sesión

#### Scenario: Validar no inicia la prueba
- **GIVEN** un `ExamToken` válido sin `ExamSession` asociada, para una prueba de 60 minutos
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}` dos veces, con horas de diferencia
- **THEN** el sistema SHALL NOT crear ninguna `ExamSession` y el token sigue con `IsUsed = false`
- **AND** cuando el candidato solicita después `POST /api/exam/start/{token}`, la sesión nueva tiene un tiempo restante de 60 minutos

#### Scenario: Token con sesión en curso
- **GIVEN** un `ExamToken` con una `ExamSession` asociada en estado `InProgress`
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = true`, el título del examen, el nombre del candidato y el identificador de la sesión en curso
- **AND** el sistema NO responde con el mensaje "Esta prueba ya ha sido completada."

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
- **THEN** el sistema responde con `isValid = false` y el mensaje "Esta prueba ya ha sido completada."
- **AND** el rechazo se apoya en el estado de la sesión, no en `IsUsed` por sí solo

#### Scenario: Sesión en curso que ya tiene resultado
- **GIVEN** una `ExamSession` en estado `InProgress` que sin embargo ya tiene un `ExamResult` asociado, porque el envío se interrumpió entre la escritura del resultado y la del estado
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde con `isValid = false` y el mensaje "Esta prueba ya ha sido completada."
- **AND** una sesión con resultado NUNCA se considera reanudable, sea cual sea su estado

## ADDED Requirements

### Requirement: Longitud máxima de la respuesta abierta
El sistema SHALL aceptar respuestas abiertas de hasta 4000 caracteres, que es el tamaño con el que se guardan. Una respuesta abierta más larga SHALL rechazarse con `400 Bad Request` y un mensaje que indique el máximo, tanto en el auto-guardado como en el envío final dentro de plazo. El rechazo SHALL producirse antes de escribir nada: ninguna respuesta, ningún resultado y ningún cambio en el estado de la sesión.

El envío final fuera de plazo SHALL NOT rechazarse por la longitud de lo que traiga, porque descarta ese contenido y puntúa lo que ya estaba guardado.

#### Scenario: Respuesta abierta en el límite
- **GIVEN** una `ExamSession` en progreso
- **WHEN** el candidato envía `POST /api/exam/answer/{sessionId}` con una respuesta abierta de exactamente 4000 caracteres
- **THEN** el sistema responde `200 OK` y guarda la respuesta completa, sin recortarla

#### Scenario: Auto-guardado de una respuesta demasiado larga
- **GIVEN** una `ExamSession` en progreso con una respuesta abierta ya guardada para una pregunta
- **WHEN** el candidato envía `POST /api/exam/answer/{sessionId}` para esa pregunta con una respuesta de 4001 caracteres
- **THEN** el sistema responde `400 Bad Request` con un mensaje que indica el máximo de 4000 caracteres
- **AND** la respuesta guardada no cambia

#### Scenario: Envío dentro de plazo con una respuesta demasiado larga
- **GIVEN** una `ExamSession` en progreso, dentro de su tiempo límite
- **WHEN** el candidato envía `POST /api/exam/submit` y una de las respuestas abiertas tiene 4001 caracteres
- **THEN** el sistema responde `400 Bad Request` con un mensaje que indica el máximo de 4000 caracteres
- **AND** el sistema SHALL NOT crear ningún `ExamResult` ni modificar ninguna `UserAnswer`
- **AND** la sesión sigue en estado `InProgress`, así que el candidato puede acortar la respuesta y volver a enviar

#### Scenario: Envío fuera de plazo con una respuesta demasiado larga
- **GIVEN** una `ExamSession` cuyo tiempo límite ya venció, con respuestas guardadas en plazo
- **WHEN** se solicita `POST /api/exam/submit` y el cuerpo trae una respuesta abierta de 4001 caracteres
- **THEN** el sistema acepta el envío, descarta el contenido del cuerpo y puntúa las respuestas guardadas en plazo
