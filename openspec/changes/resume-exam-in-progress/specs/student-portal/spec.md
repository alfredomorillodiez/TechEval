## MODIFIED Requirements

### Requirement: Listado de pruebas pendientes del alumno autenticado
El sistema SHALL exponer un endpoint que devuelva, para el alumno autenticado (identificado por el `UserId` del JWT), el listado de sus invitaciones (`ExamToken`) que todavía puede resolver, sin exponer nunca invitaciones de otro alumno. Una invitación se considera resoluble cuando no ha sido usada y no ha expirado, o bien cuando su sesión de examen sigue en estado `InProgress`. Cada entrada SHALL indicar si la prueba está sin empezar o a medias, para que la interfaz ofrezca la acción correcta.

#### Scenario: Alumno con pruebas pendientes
- **GIVEN** un alumno autenticado con dos `ExamToken` asociados a su `UserId`, ambos con `IsUsed = false` y no expirados
- **WHEN** solicita el listado de pruebas pendientes
- **THEN** el sistema responde `200 OK` con esas dos invitaciones, incluyendo el título del examen correspondiente
- **AND** ambas figuran como pruebas sin empezar

#### Scenario: Alumno con una prueba a medias
- **GIVEN** un alumno autenticado con un `ExamToken` usado cuya `ExamSession` está en estado `InProgress`
- **WHEN** solicita el listado de pruebas pendientes
- **THEN** el sistema responde `200 OK` incluyendo esa invitación
- **AND** la entrada figura como prueba empezada y sin terminar

#### Scenario: La prueba ya enviada no vuelve a la lista de pendientes
- **GIVEN** un alumno autenticado con un `ExamToken` cuya `ExamSession` está en estado `Completed`
- **WHEN** solicita el listado de pruebas pendientes
- **THEN** el sistema responde `200 OK` sin incluir esa invitación

#### Scenario: Alumno sin pruebas pendientes
- **GIVEN** un alumno autenticado sin ningún `ExamToken` resoluble
- **WHEN** solicita el listado de pruebas pendientes
- **THEN** el sistema responde `200 OK` con una lista vacía

#### Scenario: Aislamiento entre alumnos
- **GIVEN** dos alumnos distintos, cada uno con invitaciones pendientes propias
- **WHEN** uno de ellos solicita el listado de pruebas pendientes
- **THEN** el sistema responde únicamente con las invitaciones cuyo `UserId` coincide con el alumno autenticado, sin incluir las del otro alumno

### Requirement: Inicio de una prueba pendiente desde el portal
El sistema SHALL permitir al alumno autenticado iniciar o reanudar, desde el portal, cualquiera de sus pruebas pendientes, reutilizando el mismo flujo de resolución de examen ya especificado (bienvenida, temporizador, autoguardado, auto-envío). La acción ofrecida SHALL corresponder al estado de la prueba.

#### Scenario: Alumno inicia una prueba pendiente desde el portal
- **GIVEN** un alumno autenticado con una prueba sin empezar en su portal
- **WHEN** selecciona "Comenzar" sobre esa prueba pendiente
- **THEN** la interfaz lo lleva a la pantalla de bienvenida de esa prueba, siguiendo el mismo flujo que al abrir el enlace de invitación por email

#### Scenario: Alumno reanuda una prueba a medias desde el portal
- **GIVEN** un alumno autenticado con una prueba empezada y sin terminar en su portal
- **WHEN** selecciona la acción "Continuar" sobre esa prueba
- **THEN** la interfaz lo lleva directamente a la pantalla de preguntas, con el temporizador en el tiempo restante y sus respuestas previas ya marcadas, sin pasar por la bienvenida
