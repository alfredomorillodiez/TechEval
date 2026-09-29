## ADDED Requirements

### Requirement: El cambio de rol de un evaluador limpia sus asignaciones y reservas
Cuando un usuario deja el rol `Evaluador`, el sistema SHALL borrar sus asignaciones a pruebas y SHALL liberar sus reservas vigentes, en la misma transacción que el cambio de rol. Sus correcciones ya hechas SHALL NOT modificarse. La desactivación de un evaluador SHALL conservar sus asignaciones, para que la reactivación le devuelva su trabajo; sus reservas caducan solas o las libera un administrador.

#### Scenario: Evaluador ascendido a administrador
- **GIVEN** un evaluador asignado a dos pruebas y con un resultado reservado
- **WHEN** un administrador cambia su rol a `Admin`
- **THEN** el usuario ya no figura entre los evaluadores de esas pruebas, el resultado queda sin reserva, y sus correcciones anteriores siguen registradas a su nombre

#### Scenario: Evaluador desactivado y reactivado
- **GIVEN** un evaluador asignado a una prueba
- **WHEN** un administrador lo desactiva y después lo reactiva
- **THEN** el evaluador sigue asignado a la prueba
