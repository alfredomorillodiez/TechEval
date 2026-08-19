## MODIFIED Requirements

### Requirement: Persistencia del resultado y notificación al candidato
El sistema SHALL crear y persistir una entidad `ExamResult` (vinculada a `ExamSessionId`, `ExamId` y al `UserId` del alumno propietario de la sesión, con `CandidateName`, `CandidateEmail`, `TotalPoints`, `ObtainedPoints`, `ScorePercentage`, `Passed` y `CompletedAt`) al finalizar la corrección, y SHALL enviar un correo con el resultado al candidato inmediatamente después de guardarlo.

#### Scenario: Creación del ExamResult tras enviar el examen
- **WHEN** `SubmitExamAsync` termina de procesar todas las respuestas de la sesión
- **THEN** el sistema persiste un nuevo `ExamResult` con los puntos totales, puntos obtenidos, porcentaje y estado de aprobación calculados, vinculado al `UserId` del alumno dueño de la sesión, y marca la `ExamSession` asociada como `Completed`

#### Scenario: Notificación de resultado por email
- **WHEN** el `ExamResult` ha sido persistido correctamente
- **THEN** el sistema envía al correo del candidato (`CandidateEmail`) un mensaje con el nombre del examen, el porcentaje obtenido y si aprobó o no
