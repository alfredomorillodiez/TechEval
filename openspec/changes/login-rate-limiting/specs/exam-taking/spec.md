## ADDED Requirements

### Requirement: Límite de ritmo en la validación del enlace de examen
El sistema SHALL limitar el número de peticiones a `GET /api/exam/validate/{token}` por dirección de origen dentro de una ventana de tiempo, y SHALL responder `429 Too Many Requests` con `ProblemDetails` y cabecera `Retry-After` al superarlo.

El límite SHALL ser más permisivo que el del inicio de sesión: el endpoint es anónimo por diseño —la credencial es el token del enlace— y un candidato legítimo lo llama varias veces al recargar la página o al volver a su prueba.

#### Scenario: Un candidato que recarga su enlace no queda bloqueado
- **GIVEN** un candidato que abre su invitación y recarga la página varias veces seguidas
- **WHEN** cada recarga solicita `GET /api/exam/validate/{token}`
- **THEN** el sistema SHALL atender todas ellas, porque el límite deja margen para el uso normal

#### Scenario: Recorrido masivo de tokens
- **GIVEN** una dirección de origen que solicita validaciones de tokens distintos sin pausa
- **WHEN** supera el cupo de la ventana
- **THEN** el sistema SHALL responder `429 Too Many Requests`, de forma que recorrer el espacio de tokens deje de ser barato
