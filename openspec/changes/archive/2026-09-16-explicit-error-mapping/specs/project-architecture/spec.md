## ADDED Requirements

### Requirement: Los errores se traducen a HTTP en un solo sitio
El sistema SHALL traducir excepciones a códigos de estado HTTP en un único punto del pipeline. Un controlador NEVER SHALL elegir el código de estado de una excepción de negocio por su cuenta, porque el mismo error acabaría mapeado de dos formas según por dónde saliera.

La traducción SHALL apoyarse en una jerarquía de excepciones de aplicación que exprese **intención** —validación, recurso no encontrado, conflicto de estado, operación prohibida— y NEVER SHALL apoyarse en tipos de excepción genéricos de la plataforma o de sus bibliotecas.

Toda excepción ajena a esa jerarquía SHALL producir `500 Internal Server Error` con un mensaje genérico. Su detalle SHALL quedar en el registro del servidor y NEVER SHALL viajar al cliente: el mensaje de una excepción de infraestructura puede contener nombres de entidad, fragmentos de consulta o rutas internas.

Las respuestas de error SHALL usar el formato `ProblemDetails`.

#### Scenario: Error de validación de negocio
- **GIVEN** una operación que incumple una regla de negocio, como pedir más preguntas de las que hay disponibles
- **WHEN** el cliente la solicita
- **THEN** el sistema responde `400 Bad Request` con el mensaje de la regla incumplida

#### Scenario: Recurso inexistente
- **GIVEN** una operación que referencia un examen, una sesión o un resultado que no existe
- **WHEN** el cliente la solicita
- **THEN** el sistema responde `404 Not Found`

#### Scenario: Conflicto con el estado actual
- **GIVEN** una operación que el estado del sistema no permite, como corregir un resultado ya corregido o eliminar una opción que un candidato eligió
- **WHEN** el cliente la solicita
- **THEN** el sistema responde `409 Conflict` con el motivo

#### Scenario: Operación prohibida
- **GIVEN** una operación sobre un recurso que no pertenece a quien la solicita
- **WHEN** el cliente la solicita
- **THEN** el sistema responde `403 Forbidden`

#### Scenario: Fallo de infraestructura no llega al cliente
- **GIVEN** un fallo ajeno al negocio, como una excepción de EF Core al perderse la conexión
- **WHEN** ocurre durante una petición
- **THEN** el sistema responde `500 Internal Server Error` con un mensaje genérico
- **AND** el mensaje de la excepción original SHALL NOT aparecer en la respuesta
- **AND** el detalle SHALL quedar registrado en el servidor

#### Scenario: Un tipo genérico ya no se confunde con un error de negocio
- **GIVEN** una `InvalidOperationException` lanzada por una biblioteca, no por el código de negocio
- **WHEN** llega al pipeline de errores
- **THEN** el sistema responde `500`, y NEVER SHALL responder `400` con su mensaje
