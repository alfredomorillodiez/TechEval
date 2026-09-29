## ADDED Requirements

### Requirement: Asignación de evaluadores a una prueba
El sistema SHALL permitir a un administrador asignar evaluadores a una prueba y quitárselos. Una prueba SHALL poder tener varios evaluadores, y un evaluador varias pruebas. Solo SHALL poder asignarse un usuario con rol `Evaluador` y activo; en otro caso, el sistema SHALL responder `400 Bad Request`. Asignar un evaluador que ya está asignado SHALL NOT duplicar la asignación. Cada asignación SHALL guardar quién la hizo y cuándo.

El sistema SHALL exponer la lista de evaluadores de una prueba con su nombre y su email. Quitar una asignación SHALL NOT modificar las correcciones que el evaluador ya hizo en esa prueba.

#### Scenario: Asignación de un evaluador
- **GIVEN** una prueba sin evaluadores y un usuario activo con rol `Evaluador`
- **WHEN** un administrador lo asigna a la prueba
- **THEN** el sistema responde con la lista de evaluadores de la prueba, que lo incluye

#### Scenario: Asignación de un usuario que no es evaluador
- **WHEN** un administrador intenta asignar a una prueba un usuario con rol `Admin` o `Alumno`, o un evaluador desactivado
- **THEN** el sistema responde `400 Bad Request` y no crea la asignación

#### Scenario: Asignación repetida
- **GIVEN** un evaluador ya asignado a una prueba
- **WHEN** un administrador lo asigna otra vez
- **THEN** la prueba sigue con una sola asignación de ese evaluador

#### Scenario: Quitar una asignación conserva las correcciones
- **GIVEN** un evaluador que corrigió dos resultados de una prueba
- **WHEN** un administrador le quita la asignación
- **THEN** los dos resultados siguen corregidos y con el evaluador en `ReviewedByUserId`

#### Scenario: Acceso sin rol Admin
- **WHEN** un evaluador intenta asignar o quitar evaluadores
- **THEN** el sistema responde `403 Forbidden`
