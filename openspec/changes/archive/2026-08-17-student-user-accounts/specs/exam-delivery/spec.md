## MODIFIED Requirements

### Requirement: Vínculo único entre token, examen y candidato
Cada token de examen SHALL corresponder exactamente a un examen (`ExamId`) y a un candidato identificado por nombre (`CandidateName`) y correo (`CandidateEmail`), fijados en el momento de la generación y no modificables posteriormente. El token SHALL asociarse además a un `User` (`UserId`), resuelto de forma perezosa —no en el momento de la generación, sino la primera vez que el enlace se abre— buscando o creando el `User` correspondiente a `CandidateEmail`.

#### Scenario: El token queda asociado a un examen y candidato concretos
- **GIVEN** una solicitud de envío con un `ExamId`, `CandidateName` y `CandidateEmail` determinados
- **WHEN** el sistema crea el `ExamToken`
- **THEN** el registro persistido SHALL almacenar ese `ExamId`, `CandidateName` y `CandidateEmail` de forma inmutable para ese token, con `UserId` sin resolver todavía

#### Scenario: Envío a examen inexistente es rechazado
- **GIVEN** una solicitud de envío que referencia un `ExamId` que no existe
- **WHEN** el sistema procesa la solicitud
- **THEN** SHALL rechazar la operación sin generar token ni enviar email

#### Scenario: El UserId se resuelve al abrir el enlace, no al generarlo
- **GIVEN** un `ExamToken` recién generado con `UserId` sin resolver
- **WHEN** el candidato abre el enlace y el sistema procesa `GET /api/exam/validate/{token}`
- **THEN** el sistema MUST asociar el `ExamToken` al `User` correspondiente a `CandidateEmail` (buscándolo o creándolo), dejando `UserId` fijado a partir de ese momento

## ADDED Requirements

### Requirement: Reinvitación del mismo alumno a la misma prueba
El sistema SHALL permitir generar más de un `ExamToken` para el mismo `ExamId` y el mismo candidato (mismo `CandidateEmail`), sin restricción de unicidad sobre la combinación examen-candidato, cada uno con su propio ciclo de vida (expiración, uso) independiente.

#### Scenario: Segunda invitación al mismo alumno para el mismo examen
- **GIVEN** un `ExamToken` ya existente y usado para `ExamId = 5` y `CandidateEmail = "alejandro.robles@pronet-ise.com"`
- **WHEN** un administrador envía una nueva invitación con el mismo `ExamId` y el mismo `CandidateEmail`
- **THEN** el sistema SHALL crear un segundo `ExamToken` válido e independiente del primero, sin rechazar la operación por duplicidad
