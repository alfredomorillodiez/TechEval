## ADDED Requirements

### Requirement: El formulario de acceso dice por qué falla
El formulario de acceso SHALL mostrar un mensaje distinto según la causa del fallo. Un `401` SHALL mostrar «Credenciales incorrectas.». Un `429` SHALL mostrar el mensaje del servidor, que indica cuándo reintentar, y SHALL NOT mostrar «Credenciales incorrectas.». Un fallo de red SHALL decir que no se pudo conectar con el servidor. Cualquier otro error SHALL decir que el servidor falló e invitar a reintentar.

#### Scenario: Credenciales incorrectas
- **WHEN** el servidor responde `401` al intento de acceso
- **THEN** el formulario muestra «Credenciales incorrectas.»

#### Scenario: Límite de ritmo
- **WHEN** el servidor responde `429` al intento de acceso
- **THEN** el formulario muestra que hay demasiados intentos y cuándo volver a intentarlo
- **AND** SHALL NOT mostrar «Credenciales incorrectas.»

#### Scenario: API caída
- **GIVEN** la API parada
- **WHEN** la persona intenta acceder
- **THEN** el formulario dice que no se pudo conectar con el servidor, no que las credenciales sean incorrectas

#### Scenario: Error del servidor
- **WHEN** el servidor responde `500`
- **THEN** el formulario dice que el servidor falló y que se puede reintentar
