## ADDED Requirements

### Requirement: Las condiciones de la sesión se congelan al empezar
Al crear una `ExamSession`, el sistema SHALL guardar en ella el tiempo límite y la nota de corte que la prueba tiene en ese momento. Desde entonces, el plazo, el tiempo restante, la ventana de gracia del envío y el tope de las salidas de la página SHALL calcularse con el tiempo guardado, y el veredicto, tanto al enviar una prueba sin abiertas como al cerrar la corrección de una con abiertas, SHALL calcularse con la nota guardada. Editar la prueba SHALL NOT cambiar las sesiones ya empezadas. Una sesión sin valores guardados (anterior a este cambio) SHALL usar los de la prueba.

#### Scenario: Acortar el tiempo con candidatos en curso
- **GIVEN** un candidato que empezó hace 20 minutos una prueba de 60, y después un administrador cambia el tiempo límite de la prueba a 30
- **WHEN** el candidato pide su sesión o envía la prueba
- **THEN** el tiempo restante es de 40 minutos, no de 10, y el envío se acepta en plazo

#### Scenario: Alargar el tiempo con candidatos en curso
- **GIVEN** un candidato que empezó hace 50 minutos una prueba de 60, y después un administrador cambia el tiempo a 120
- **WHEN** el candidato pide su sesión
- **THEN** el tiempo restante es de 10 minutos

#### Scenario: Una sesión nueva usa el valor nuevo
- **GIVEN** la prueba editada a 30 minutos
- **WHEN** otro candidato empieza la prueba
- **THEN** su sesión tiene 30 minutos

#### Scenario: Cambiar la nota de corte entre dos envíos
- **GIVEN** dos candidatos que empezaron una prueba de test con nota de corte 70, y después un administrador sube la nota a 80
- **WHEN** ambos envían con un 75 %
- **THEN** los dos reciben veredicto «aprobado», porque ambos empezaron con 70

#### Scenario: Cambiar la nota de corte durante la corrección
- **GIVEN** un resultado pendiente de corrección cuya sesión empezó con nota de corte 70, y después un administrador sube la nota a 80
- **WHEN** el corrector cierra la corrección y el resultado queda en 75 %
- **THEN** el veredicto es «aprobado»

#### Scenario: Sesión anterior al cambio
- **GIVEN** una sesión sin tiempo ni nota guardados
- **WHEN** se calcula su plazo o su veredicto
- **THEN** el sistema usa el tiempo límite y la nota de corte actuales de la prueba

#### Scenario: Reanudar no cambia lo guardado
- **GIVEN** una sesión en curso con sus valores guardados
- **WHEN** el candidato la reanuda
- **THEN** el sistema SHALL NOT volver a copiar los valores de la prueba

### Requirement: Sesiones abandonadas
El sistema SHALL marcar como `Abandoned` toda sesión `InProgress` sin `ExamResult` cuyo plazo (inicio más el tiempo límite guardado) venció hace más de `Sessions:AbandonAfterHours` horas, 24 por defecto. Un trabajo en segundo plano SHALL revisarlo cada `Sessions:SweepMinutes` minutos, 15 por defecto, y con `AbandonAfterHours` a 0 SHALL desactivarse. Una sesión abandonada SHALL conservar sus respuestas guardadas y SHALL NOT generar resultado. Una sesión que tiene resultado, o que sigue dentro del plazo o de la gracia, SHALL NOT marcarse.

#### Scenario: Una sesión vencida hace más de la gracia
- **GIVEN** una sesión `InProgress` sin resultado cuyo plazo venció hace 25 horas
- **WHEN** el trabajo se ejecuta
- **THEN** la sesión pasa a `Abandoned`
- **AND** sus `UserAnswer` siguen guardadas y no se crea ningún `ExamResult`

#### Scenario: Una sesión que todavía puede enviar
- **GIVEN** una sesión `InProgress` cuyo plazo venció hace 2 horas
- **WHEN** el trabajo se ejecuta
- **THEN** la sesión sigue `InProgress`, y el candidato que vuelva puede enviarla

#### Scenario: Una sesión en curso dentro de su plazo
- **GIVEN** una sesión empezada hace 10 minutos de una prueba de 60
- **WHEN** el trabajo se ejecuta
- **THEN** la sesión no cambia

#### Scenario: El tiempo guardado manda sobre el de la prueba
- **GIVEN** una sesión empezada con 60 minutos, y después la prueba editada a 600
- **WHEN** el trabajo calcula su plazo
- **THEN** usa los 60 guardados

#### Scenario: Una sesión con resultado no se marca
- **GIVEN** una sesión `InProgress` con `ExamResult`, por un envío interrumpido, y plazo vencido hace una semana
- **WHEN** el trabajo se ejecuta
- **THEN** la sesión no pasa a `Abandoned`

#### Scenario: El trabajo desactivado
- **GIVEN** `Sessions:AbandonAfterHours` igual a 0
- **WHEN** arranca la API
- **THEN** el trabajo no marca ninguna sesión

#### Scenario: Un fallo no tumba la API
- **GIVEN** un fallo de base de datos durante una revisión
- **WHEN** el trabajo lo captura
- **THEN** lo registra en el log y repite la revisión en el siguiente intervalo

### Requirement: Una sesión abandonada no se reanuda ni se escribe
El sistema SHALL rechazar toda operación sobre una sesión `Abandoned`. La validación del enlace SHALL responder con un mensaje que diga que la prueba se cerró por inactividad y que hay que pedir una invitación nueva, distinto del de una prueba completada. El autoguardado y el envío SHALL responder `409 Conflict` con un mensaje que diga que la prueba se cerró por inactividad. La sesión SHALL NOT figurar entre las pruebas pendientes del alumno.

#### Scenario: Abrir el enlace de una sesión abandonada
- **GIVEN** una sesión `Abandoned`
- **WHEN** el candidato solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema responde `isValid = false` con el mensaje de que la prueba se cerró por inactividad y de que pida una invitación nueva
- **AND** el mensaje SHALL NOT decir que la prueba ya se completó

#### Scenario: Autoguardar en una sesión abandonada
- **GIVEN** una sesión `Abandoned`
- **WHEN** su dueño envía `POST /api/exam/answer/{sessionId}`
- **THEN** el sistema responde `409` con el mensaje de cierre por inactividad y no escribe nada

#### Scenario: Enviar una sesión abandonada
- **GIVEN** una sesión `Abandoned` sin resultado
- **WHEN** su dueño envía `POST /api/exam/submit`
- **THEN** el sistema responde `409` con el mensaje de cierre por inactividad
- **AND** no crea ningún `ExamResult`

#### Scenario: El portal del alumno
- **GIVEN** un alumno con una sesión abandonada
- **WHEN** consulta `GET /api/student/pending`
- **THEN** esa prueba no aparece

#### Scenario: Reinvitar tras un abandono
- **GIVEN** un alumno con una sesión abandonada
- **WHEN** un administrador le envía de nuevo la misma prueba
- **THEN** el sistema crea una invitación nueva con su propia sesión al empezar
