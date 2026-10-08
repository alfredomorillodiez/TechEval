## Purpose

Gestión de las cuentas de usuario por parte de los administradores: alta de administradores y evaluadores, cambio de rol, desactivación, reactivación y restablecimiento del acceso, con un enlace de un solo uso para que cada persona fije su propia contraseña.

## ADDED Requirements

### Requirement: Listado de usuarios
El sistema SHALL exponer a usuarios con rol `Admin` un listado de todos los usuarios. Cada entrada SHALL incluir el identificador, el nombre, el email, el rol, si la cuenta está activa, la fecha de alta y si el acceso está pendiente. El acceso está pendiente cuando un administrador o un evaluador todavía no ha fijado su contraseña. El listado SHALL admitir filtros por rol, por estado (activa o inactiva) y por texto sobre el nombre o el email. El listado SHALL NOT incluir el hash de la contraseña, el sello de seguridad ni ningún enlace para fijar la contraseña.

#### Scenario: Listado completo
- **GIVEN** un administrador, un evaluador y dos alumnos registrados
- **WHEN** un administrador solicita el listado de usuarios sin filtros
- **THEN** el sistema responde `200 OK` con los cuatro usuarios, cada uno con su rol y su estado

#### Scenario: Filtro por rol
- **WHEN** un administrador solicita el listado filtrado por el rol `Evaluador`
- **THEN** el sistema responde solo con los usuarios de rol `Evaluador`

#### Scenario: Evaluador con acceso pendiente
- **GIVEN** un evaluador recién creado que todavía no ha usado su enlace
- **WHEN** un administrador solicita el listado
- **THEN** la entrada de ese evaluador indica que su acceso está pendiente

#### Scenario: Acceso sin rol Admin
- **WHEN** un usuario con rol `Evaluador` o `Alumno` solicita el listado de usuarios
- **THEN** el sistema responde `403 Forbidden` y no revela ningún dato de usuarios

### Requirement: Alta de administradores y evaluadores
El sistema SHALL permitir a un administrador crear un usuario con nombre, email y rol. El rol SHALL ser `Admin` o `Evaluador`. Las cuentas de alumno SHALL NOT crearse por esta vía, porque nacen al abrir una invitación. La cuenta SHALL nacer activa, sin contraseña utilizable y con un enlace para fijar la contraseña, que el sistema envía al email del usuario. El administrador SHALL NOT poder indicar la contraseña del usuario nuevo.

Si el envío del correo falla, la cuenta SHALL quedar creada igualmente. La respuesta SHALL indicar que el correo no salió, para que el administrador pueda restablecer el acceso y reenviar el enlace.

#### Scenario: Alta correcta de un evaluador
- **WHEN** un administrador crea un usuario con nombre "Laura Gil", email "laura.gil@pronet-ise.com" y rol `Evaluador`
- **THEN** el sistema responde `201 Created` con el usuario creado, activo y con el acceso pendiente
- **AND** el sistema envía a "laura.gil@pronet-ise.com" un correo con el enlace para fijar la contraseña

#### Scenario: Email ya registrado
- **GIVEN** un usuario existente con el email "laura.gil@pronet-ise.com", sea cual sea su rol
- **WHEN** un administrador intenta crear otro usuario con ese email
- **THEN** el sistema responde `409 Conflict` y no crea ningún usuario

#### Scenario: Alta con rol Alumno rechazada
- **WHEN** un administrador intenta crear un usuario con rol `Alumno`
- **THEN** el sistema responde `400 Bad Request` y no crea ningún usuario

#### Scenario: Datos obligatorios
- **WHEN** un administrador intenta crear un usuario sin nombre, sin email o con un email sin formato válido
- **THEN** el sistema responde `400 Bad Request` y no crea ningún usuario

#### Scenario: Fallo del correo en el alta
- **GIVEN** un servidor de correo que rechaza el envío
- **WHEN** un administrador crea un evaluador
- **THEN** el sistema crea la cuenta y responde `201 Created` indicando que el correo no se envió

### Requirement: Cambio de rol
El sistema SHALL permitir a un administrador cambiar el rol de otro usuario según esta tabla:

| Rol actual | Roles de destino permitidos |
|---|---|
| `Admin` | `Evaluador` |
| `Evaluador` | `Admin` |
| `Alumno` | `Admin`, `Evaluador` |

El paso a `Alumno` SHALL NOT estar permitido: para retirar el acceso a un administrador o a un evaluador, el administrador SHALL desactivar la cuenta. Cuando un alumno pasa a otro rol y no tiene contraseña utilizable, el sistema SHALL enviarle el enlace para fijarla. Tras cualquier cambio de rol, los tokens emitidos antes del cambio SHALL dejar de valer.

#### Scenario: Evaluador ascendido a administrador
- **GIVEN** un evaluador con una sesión abierta
- **WHEN** un administrador cambia su rol a `Admin`
- **THEN** el sistema responde `200 OK` con el rol nuevo
- **AND** la siguiente petición con el token anterior del evaluador recibe `401 Unauthorized`

#### Scenario: Alumno convertido en evaluador
- **GIVEN** un alumno aprovisionado por invitación, sin contraseña utilizable
- **WHEN** un administrador cambia su rol a `Evaluador`
- **THEN** el sistema responde `200 OK`, el acceso queda pendiente y el sistema envía el enlace para fijar la contraseña
- **AND** los resultados de las pruebas que hizo como alumno siguen asociados a su cuenta

#### Scenario: Paso a Alumno rechazado
- **WHEN** un administrador intenta cambiar a `Alumno` el rol de un evaluador
- **THEN** el sistema responde `400 Bad Request` y el rol no cambia

#### Scenario: Cambio del propio rol rechazado
- **WHEN** un administrador intenta cambiar su propio rol
- **THEN** el sistema responde `409 Conflict` y el rol no cambia

### Requirement: Desactivación y reactivación
El sistema SHALL permitir a un administrador desactivar y reactivar la cuenta de otro usuario, de cualquier rol. Una cuenta desactivada SHALL NOT poder iniciar sesión, y sus tokens SHALL dejar de valer en el acto. Un alumno desactivado SHALL NOT poder abrir una invitación. Al reactivar una cuenta, los tokens emitidos antes de la desactivación SHALL seguir sin valer. La desactivación SHALL NOT borrar datos: los resultados, las correcciones y las pruebas creadas por el usuario se conservan.

#### Scenario: Desactivación de un evaluador con sesión abierta
- **GIVEN** un evaluador con una sesión abierta
- **WHEN** un administrador desactiva su cuenta
- **THEN** el sistema responde `200 OK`
- **AND** la siguiente petición del evaluador con su token recibe `401 Unauthorized`

#### Scenario: Reactivación no resucita tokens antiguos
- **GIVEN** un evaluador desactivado cuyo token, emitido antes de la desactivación, todavía no ha caducado
- **WHEN** un administrador reactiva la cuenta y el evaluador usa ese token antiguo
- **THEN** el sistema responde `401 Unauthorized`, y el evaluador tiene que iniciar sesión otra vez

#### Scenario: Alumno desactivado abre una invitación
- **GIVEN** un alumno desactivado con una invitación vigente
- **WHEN** abre el enlace de la invitación
- **THEN** el sistema responde que el enlace no es válido y no emite ningún token

#### Scenario: Desactivación propia rechazada
- **WHEN** un administrador intenta desactivar su propia cuenta
- **THEN** el sistema responde `409 Conflict` y la cuenta sigue activa

### Requirement: Siempre queda un administrador activo
El sistema SHALL garantizar que siempre existe al menos un usuario activo con rol `Admin`. El sistema SHALL rechazar con `409 Conflict` cualquier cambio de rol o desactivación que deje cero administradores activos. La garantía SHALL cumplirse también cuando dos administradores actúan a la vez, cada uno sobre el otro.

#### Scenario: Dos administradores se desactivan a la vez
- **GIVEN** exactamente dos administradores activos, A y B
- **WHEN** A desactiva a B y, al mismo tiempo, B desactiva a A
- **THEN** el sistema acepta como máximo una de las dos operaciones y responde `409 Conflict` a la otra
- **AND** al final queda al menos un administrador activo

### Requirement: Restablecimiento del acceso
El sistema SHALL permitir a un administrador restablecer el acceso de otro administrador o de un evaluador. El restablecimiento SHALL dejar la cuenta sin contraseña utilizable, SHALL invalidar todos los tokens y todos los enlaces anteriores del usuario, y SHALL enviar un enlace nuevo para fijar la contraseña. El restablecimiento del acceso de un alumno SHALL rechazarse, porque su acceso es el enlace de la invitación. Un administrador SHALL NOT poder restablecer su propio acceso.

#### Scenario: Restablecimiento del acceso de un evaluador
- **GIVEN** un evaluador con contraseña y con una sesión abierta
- **WHEN** un administrador restablece su acceso
- **THEN** el sistema responde `200 OK` y envía al evaluador un enlace nuevo
- **AND** el login con la contraseña anterior recibe `401 Unauthorized`
- **AND** la siguiente petición con el token anterior recibe `401 Unauthorized`

#### Scenario: Reenvío tras un fallo del correo
- **GIVEN** un evaluador creado cuyo correo de alta no salió
- **WHEN** un administrador restablece su acceso
- **THEN** el sistema envía un enlace nuevo, y el enlace del alta deja de valer

#### Scenario: Restablecimiento del acceso de un alumno rechazado
- **WHEN** un administrador intenta restablecer el acceso de un alumno
- **THEN** el sistema responde `400 Bad Request` y no envía ningún correo

#### Scenario: Restablecimiento propio rechazado
- **WHEN** un administrador intenta restablecer su propio acceso
- **THEN** el sistema responde `409 Conflict` y no modifica su cuenta

### Requirement: Enlace de un solo uso para fijar la contraseña
El enlace para fijar la contraseña SHALL tener la forma `{FrontendBaseUrl}/fijar-contrasena/{token}`. El token SHALL generarse con un generador criptográficamente seguro, con al menos 32 bytes de entropía. El sistema SHALL guardar solo una derivación del token, nunca el token en claro. El enlace SHALL caducar a las 48 horas de su emisión. El enlace SHALL valer una sola vez. Emitir un enlace nuevo para un usuario SHALL invalidar los enlaces anteriores de ese usuario que no se hayan usado.

El sistema SHALL exponer dos operaciones públicas: una para comprobar un enlace, que devuelve el nombre y el email del usuario, y otra para fijar la contraseña con el enlace. Ante un enlace inexistente, caducado, ya usado o de una cuenta desactivada, las dos operaciones SHALL dar la misma respuesta genérica, sin revelar cuál de las condiciones falló.

#### Scenario: Comprobación de un enlace vigente
- **GIVEN** un enlace emitido hace una hora y no usado
- **WHEN** alguien comprueba el enlace
- **THEN** el sistema responde con el nombre y el email del usuario

#### Scenario: Enlace caducado
- **GIVEN** un enlace emitido hace más de 48 horas
- **WHEN** alguien comprueba el enlace o intenta fijar la contraseña con él
- **THEN** el sistema da la respuesta genérica de enlace no válido

#### Scenario: Enlace ya usado
- **GIVEN** un enlace con el que ya se fijó una contraseña
- **WHEN** alguien intenta fijar otra contraseña con el mismo enlace
- **THEN** el sistema da la respuesta genérica de enlace no válido y la contraseña no cambia

#### Scenario: Enlace sustituido por otro más reciente
- **GIVEN** un evaluador con un enlace no usado
- **WHEN** un administrador restablece su acceso y el evaluador usa el enlace anterior
- **THEN** el sistema da la respuesta genérica de enlace no válido

#### Scenario: Enlace de una cuenta desactivada
- **GIVEN** un enlace vigente de un evaluador que un administrador desactivó después
- **WHEN** alguien intenta fijar la contraseña con ese enlace
- **THEN** el sistema da la respuesta genérica de enlace no válido

#### Scenario: El token no se guarda en claro
- **WHEN** el sistema emite un enlace
- **THEN** el valor guardado en la base de datos SHALL NOT coincidir con el token del enlace

### Requirement: Fijación de la contraseña
Con un enlace válido, el sistema SHALL aceptar una contraseña de entre 12 y 128 caracteres. El sistema SHALL NOT imponer reglas de composición (mayúsculas, números o símbolos). Al fijar la contraseña, el sistema SHALL guardar su hash con el esquema vigente, SHALL marcar el enlace como usado y SHALL invalidar los tokens anteriores del usuario. El sistema SHALL NOT iniciar la sesión de forma automática: el usuario inicia sesión después con su email y su contraseña.

#### Scenario: Contraseña fijada con éxito
- **GIVEN** un enlace válido de un evaluador
- **WHEN** el evaluador fija la contraseña "caballo-bateria-grapa"
- **THEN** el sistema responde `204 No Content`
- **AND** el login con su email y esa contraseña responde `200 OK` con el rol `Evaluador`

#### Scenario: Contraseña demasiado corta
- **GIVEN** un enlace válido
- **WHEN** alguien intenta fijar una contraseña de 11 caracteres
- **THEN** el sistema responde `400 Bad Request`, la contraseña no cambia y el enlace sigue valiendo

### Requirement: Límite de ritmo en el enlace para fijar la contraseña
El sistema SHALL limitar por dirección de origen el número de peticiones a las dos operaciones públicas del enlace. Superado el límite, SHALL responder `429 Too Many Requests` con una cabecera `Retry-After`, sin comprobar el enlace ni calcular ningún hash.

#### Scenario: Intentos por encima del límite
- **GIVEN** una dirección de origen que ya agotó su cupo en la ventana actual
- **WHEN** envía una petición más para fijar una contraseña
- **THEN** el sistema responde `429 Too Many Requests` con la cabecera `Retry-After`
