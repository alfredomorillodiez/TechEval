## ADDED Requirements

### Requirement: Validación de la invitación
El sistema SHALL validar en el servidor los datos de una invitación y MUST responder `400 Bad Request` con un mensaje que diga qué falla, sin crear ningún token. El nombre SHALL no estar vacío. El correo SHALL tener formato válido. La expiración SHALL estar entre 1 y 720 horas. Un envío masivo SHALL llevar entre 1 y 500 candidatos, y SHALL validar cada uno antes de crear ningún token.

#### Scenario: Correo con formato inválido
- **WHEN** un administrador envía `POST /api/exams/send` con `CandidateEmail = "no-es-un-correo"`
- **THEN** el sistema responde `400` indicando que el correo no es válido y no crea ningún token

#### Scenario: Expiración fuera de rango
- **WHEN** un administrador envía una invitación con `ExpirationHours = 0`
- **THEN** el sistema responde `400` indicando que la expiración debe estar entre 1 y 720 horas

#### Scenario: Envío masivo vacío
- **WHEN** un administrador envía `POST /api/exams/send-bulk` sin candidatos
- **THEN** el sistema responde `400` indicando que hace falta al menos un candidato

### Requirement: Una prueba desactivada no admite invitaciones
El sistema SHALL rechazar con `409 Conflict` y un mensaje claro cualquier invitación, individual o masiva, a una prueba desactivada. El envío masivo SHALL rechazarse entero, antes de crear ningún token. Desactivar una prueba SHALL NOT invalidar los enlaces ya enviados.

#### Scenario: Invitación individual a una prueba desactivada
- **GIVEN** una prueba con `IsActive = false`
- **WHEN** un administrador envía `POST /api/exams/send` para esa prueba
- **THEN** el sistema responde `409` indicando que la prueba está desactivada
- **AND** no crea ningún token ni envía ningún correo

#### Scenario: Envío masivo a una prueba desactivada
- **GIVEN** una prueba con `IsActive = false`
- **WHEN** un administrador envía `POST /api/exams/send-bulk` con tres candidatos
- **THEN** el sistema responde `409` y no crea ningún token

#### Scenario: Un enlace ya enviado sigue valiendo
- **GIVEN** una invitación enviada antes de desactivar la prueba
- **WHEN** el candidato abre su enlace
- **THEN** el enlace se comporta como antes

### Requirement: Las fechas de los correos van en hora local
El correo de invitación y el de enlace para fijar la contraseña SHALL mostrar la fecha de expiración en la zona horaria que indique `Email:TimeZoneId` (por defecto `Europe/Madrid`), con el nombre de la zona y su desfase respecto a UTC. Si la zona configurada no existe en el equipo, SHALL mostrar la fecha en UTC con esa etiqueta, sin fallar. El valor almacenado SHALL seguir siendo UTC.

#### Scenario: Invitación en horario de verano
- **GIVEN** una expiración de `2026-10-09 10:30 UTC` y la zona `Europe/Madrid`
- **WHEN** se compone el correo de invitación
- **THEN** el correo dice «09/10/2026 12:30 (hora de Madrid, UTC+02:00)»

#### Scenario: Invitación en horario de invierno
- **GIVEN** una expiración de `2026-12-09 10:30 UTC` y la zona `Europe/Madrid`
- **WHEN** se compone el correo
- **THEN** el correo dice «09/12/2026 11:30 (hora de Madrid, UTC+01:00)»

#### Scenario: Zona desconocida
- **GIVEN** `Email:TimeZoneId` con un valor que el equipo no conoce
- **WHEN** se compone el correo
- **THEN** el correo muestra la fecha en UTC con la etiqueta «UTC»
- **AND** el envío no falla
