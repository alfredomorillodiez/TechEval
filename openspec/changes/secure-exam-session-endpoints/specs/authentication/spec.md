## MODIFIED Requirements

### Requirement: Autorización por rol Alumno en endpoints del portal
El sistema SHALL exigir un JWT válido con rol `Alumno` para acceder a los endpoints del portal del alumno (pruebas pendientes y realizadas) **y a los endpoints de resolución de la prueba que operan sobre una sesión ya iniciada** (guardado de respuestas y envío), y SHALL responder `401 Unauthorized` (sin token válido) o `403 Forbidden` (token válido sin rol `Alumno`) en caso contrario.

Quedan fuera de esta exigencia los endpoints que operan con el token del enlace de invitación —la validación del token y el inicio de la sesión—, donde la credencial es ese token y no una sesión autenticada.

#### Scenario: Petición sin token a un endpoint del portal
- **WHEN** se envía una petición a un endpoint del portal del alumno sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Token de administrador usado contra un endpoint del portal
- **GIVEN** un JWT válido emitido para un usuario con `IsAdmin = true`
- **WHEN** se envía una petición a un endpoint del portal del alumno con ese token
- **THEN** el sistema MUST responder `403 Forbidden`

#### Scenario: Petición sin token a un endpoint de resolución de la prueba
- **WHEN** se envía una petición de guardado de respuesta o de envío de la prueba sin header `Authorization`
- **THEN** el sistema MUST responder `401 Unauthorized`

#### Scenario: Los endpoints del enlace de invitación siguen siendo públicos
- **WHEN** se envía una petición de validación de token o de inicio de sesión sin header `Authorization`
- **THEN** el sistema MUST procesarla, porque la credencial de esas operaciones es el token del enlace
