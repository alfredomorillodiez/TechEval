## ADDED Requirements

### Requirement: Las invitaciones son solo para alumnos
El sistema SHALL rechazar el envío de una invitación a un email que corresponde a un usuario con rol `Admin` o `Evaluador`. El rechazo SHALL producirse antes de generar el token y antes de enviar el correo, y el mensaje SHALL indicar que ese email pertenece a una cuenta que no es de alumno. En un envío masivo, el rechazo SHALL afectar solo a ese candidato, igual que cualquier otro fallo individual.

El motivo es la regla de un rol por usuario: abrir la invitación emite un token de alumno, y ese token no puede emitirse para una cuenta de otro rol.

#### Scenario: Invitación individual a un evaluador
- **GIVEN** un usuario con rol `Evaluador` y email "laura.gil@pronet-ise.com"
- **WHEN** un administrador envía una prueba a "laura.gil@pronet-ise.com"
- **THEN** el sistema responde `400 Bad Request`, no genera ningún `ExamToken` y no envía ningún correo

#### Scenario: Envío masivo con un evaluador en la lista
- **GIVEN** una lista de tres candidatos en la que el segundo email pertenece a un administrador
- **WHEN** un administrador envía la prueba a la lista
- **THEN** el sistema genera y envía las invitaciones del primer y del tercer candidato
- **AND** el resultado del segundo candidato indica el fallo con su motivo

#### Scenario: Invitación a un email que todavía no es usuario
- **GIVEN** un email que no corresponde a ningún usuario
- **WHEN** un administrador envía una prueba a ese email
- **THEN** el sistema genera la invitación con normalidad, y el usuario de rol `Alumno` nace al abrir el enlace
