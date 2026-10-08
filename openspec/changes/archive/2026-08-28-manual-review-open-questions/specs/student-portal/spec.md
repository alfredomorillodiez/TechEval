## MODIFIED Requirements

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
