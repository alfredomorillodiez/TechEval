## ADDED Requirements

### Requirement: Los secretos llegan por configuración, no por el repositorio
El repositorio NEVER SHALL contener el valor de un secreto de producción: contraseñas de base de datos, claves de firma, contraseñas de cuenta ni credenciales de servicios externos. Los ficheros versionados SHALL declarar el **nombre** de cada secreto y dejar su valor vacío o expresado como una variable de entorno sin valor por defecto.

El sistema MUST negarse a arrancar fuera del entorno de desarrollo si falta cualquier secreto obligatorio, o si alguno conserva el valor documentado para desarrollo. Un despliegue mal configurado SHALL parar en seco con un mensaje que nombre lo que falta, y NEVER SHALL arrancar con un valor por defecto conocido.

Los valores de desarrollo MAY estar versionados en el fichero de configuración de desarrollo, porque son públicos por definición y el arranque en producción los rechaza explícitamente.

#### Scenario: Arranque en producción sin la clave de firma
- **GIVEN** un despliegue con el entorno distinto de desarrollo y sin valor para la clave JWT
- **WHEN** la aplicación arranca
- **THEN** el sistema MUST detener el arranque con un error que nombre la clave que falta
- **AND** el sistema SHALL NOT generar ni asumir ninguna clave

#### Scenario: Arranque en producción con el valor de desarrollo
- **GIVEN** un despliegue con el entorno distinto de desarrollo y una clave JWT igual a la documentada para desarrollo
- **WHEN** la aplicación arranca
- **THEN** el sistema MUST detener el arranque, porque un valor público no sirve como secreto

#### Scenario: Arranque en producción sin contraseña de administrador
- **GIVEN** un despliegue con el entorno distinto de desarrollo y sin valor para la contraseña del administrador
- **WHEN** la aplicación arranca
- **THEN** el sistema MUST detener el arranque
- **AND** el sistema SHALL NOT sembrar el administrador con ninguna contraseña por defecto

#### Scenario: Arranque en desarrollo
- **GIVEN** el entorno de desarrollo y los valores documentados en su fichero de configuración
- **WHEN** la aplicación arranca
- **THEN** el sistema MUST arrancar con normalidad, sin exigir variables de entorno adicionales

#### Scenario: Arranque del stack con variables sin definir
- **GIVEN** una máquina sin las variables de entorno que declara `docker-compose.yml`
- **WHEN** se levanta el stack
- **THEN** Compose MUST fallar nombrando la variable que falta, en lugar de sustituirla por una cadena vacía

#### Scenario: El fichero de configuración local no viaja en el repositorio
- **WHEN** se inspecciona el contenido versionado del repositorio
- **THEN** el fichero de configuración local del desarrollador SHALL NOT estar entre los ficheros seguidos por git
