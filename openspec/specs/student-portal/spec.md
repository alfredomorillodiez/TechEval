# student-portal Specification

## Purpose
TBD - created by archiving change student-user-accounts. Update Purpose after archive.

## Requirements

### Requirement: Listado de pruebas pendientes del alumno autenticado
El sistema SHALL exponer un endpoint que devuelva, para el alumno autenticado (identificado por el `UserId` del JWT), el listado de sus invitaciones (`ExamToken`) aún no usadas y no expiradas, sin exponer nunca invitaciones de otro alumno.

#### Scenario: Alumno con pruebas pendientes
- **GIVEN** un alumno autenticado con dos `ExamToken` asociados a su `UserId`, ambos con `IsUsed = false` y no expirados
- **WHEN** solicita el listado de pruebas pendientes
- **THEN** el sistema responde `200 OK` con esas dos invitaciones, incluyendo el título del examen correspondiente

#### Scenario: Alumno sin pruebas pendientes
- **GIVEN** un alumno autenticado sin ningún `ExamToken` con `IsUsed = false` no expirado
- **WHEN** solicita el listado de pruebas pendientes
- **THEN** el sistema responde `200 OK` con una lista vacía

#### Scenario: Aislamiento entre alumnos
- **GIVEN** dos alumnos distintos, cada uno con invitaciones pendientes propias
- **WHEN** uno de ellos solicita el listado de pruebas pendientes
- **THEN** el sistema responde únicamente con las invitaciones cuyo `UserId` coincide con el alumno autenticado, sin incluir las del otro alumno

### Requirement: Listado de pruebas realizadas y notas del alumno autenticado
El sistema SHALL exponer un endpoint que devuelva, para el alumno autenticado, el listado de sus `ExamResult` (título del examen, estado de corrección, porcentaje obtenido, si aprobó, y fecha de finalización), ordenados del más reciente al más antiguo, sin exponer nunca resultados de otro alumno. Para los resultados con `Status = PendingReview`, el sistema SHALL exponer el estado pendiente y NO SHALL exponer porcentaje ni veredicto, evitando presentar una puntuación parcial como si fuera la nota definitiva.

#### Scenario: Alumno con historial de resultados
- **GIVEN** un alumno autenticado con tres `ExamResult` corregidos asociados a su `UserId`
- **WHEN** solicita su listado de pruebas realizadas
- **THEN** el sistema responde `200 OK` con los tres resultados, ordenados por fecha de finalización descendente, cada uno con el porcentaje obtenido y si aprobó

#### Scenario: Alumno con una prueba pendiente de corrección
- **GIVEN** un alumno autenticado con un `ExamResult` en estado `PendingReview`
- **WHEN** solicita su listado de pruebas realizadas
- **THEN** el sistema responde incluyendo esa prueba con su estado pendiente de corrección y sin porcentaje ni veredicto, y la interfaz del portal la muestra como «Pendiente de corrección» en lugar de una nota

#### Scenario: Aislamiento entre alumnos
- **GIVEN** dos alumnos distintos, cada uno con resultados propios
- **WHEN** uno de ellos solicita su listado de pruebas realizadas
- **THEN** el sistema responde únicamente con los resultados cuyo `UserId` coincide con el alumno autenticado, sin incluir los del otro alumno

### Requirement: Inicio de una prueba pendiente desde el portal
El sistema SHALL permitir al alumno autenticado iniciar, desde el portal, cualquiera de sus pruebas pendientes, reutilizando el mismo flujo de resolución de examen ya especificado (bienvenida, temporizador, autoguardado, auto-envío).

#### Scenario: Alumno inicia una prueba pendiente desde el portal
- **GIVEN** un alumno autenticado con una prueba pendiente en su portal
- **WHEN** selecciona "Comenzar" sobre esa prueba pendiente
- **THEN** la interfaz lo lleva a la pantalla de bienvenida de esa prueba, siguiendo el mismo flujo que al abrir el enlace de invitación por email
