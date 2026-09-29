## ADDED Requirements

### Requirement: Autorización por rol Evaluador en endpoints de evaluación
El sistema SHALL exigir un JWT válido con rol `Evaluador` para acceder a los endpoints de evaluación (cola, detalle, reserva, envío, señales e historial del evaluador), y SHALL responder `401 Unauthorized` sin token válido o `403 Forbidden` con un token de otro rol. El administrador SHALL NOT usar estos endpoints: corrige por los suyos, que le muestran la identidad del candidato. La exigencia SHALL definirse en el mismo punto de la API que las demás políticas.

#### Scenario: Token de administrador contra un endpoint de evaluación
- **GIVEN** un JWT válido de un usuario con rol `Admin`
- **WHEN** se envía una petición a un endpoint de evaluación
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Token de alumno contra un endpoint de evaluación
- **GIVEN** un JWT válido de un usuario con rol `Alumno`
- **WHEN** se envía una petición a un endpoint de evaluación
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Petición sin token a un endpoint de evaluación
- **WHEN** se envía una petición a un endpoint de evaluación sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized`

## MODIFIED Requirements

### Requirement: Autorización por rol Admin en endpoints de gestión
El sistema SHALL exigir un JWT válido con rol `Admin` para acceder a los endpoints de gestión de categorías, preguntas, exámenes, resultados, corrección de preguntas abiertas y usuarios, y SHALL responder `401 Unauthorized` (sin token válido) o `403 Forbidden` (token válido de un rol distinto de `Admin`) en caso contrario. La exigencia SHALL definirse en un solo punto de la API, de forma que un cambio en la matriz de permisos no obligue a editar cada controlador.

#### Scenario: Petición sin token a un endpoint de gestión
- **GIVEN** los endpoints de gestión de categorías, preguntas, exámenes, resultados, corrección y usuarios
- **WHEN** se envía una petición a cualquiera de ellos sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Token válido sin claim de rol Admin
- **GIVEN** un JWT válido emitido para un usuario con rol `Alumno`
- **WHEN** se envía una petición a un endpoint de gestión con ese token en el header `Authorization`
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Token válido de un evaluador
- **GIVEN** un JWT válido emitido para un usuario con rol `Evaluador`
- **WHEN** se envía una petición a cualquier endpoint de gestión, incluidos los de corrección de preguntas abiertas del administrador
- **THEN** el sistema MUST responder `403 Forbidden`, porque esos endpoints muestran la identidad del candidato; el evaluador corrige por los endpoints de evaluación
