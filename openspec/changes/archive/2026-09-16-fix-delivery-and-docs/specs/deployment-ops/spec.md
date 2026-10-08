## MODIFIED Requirements

### Requirement: Levantamiento del stack completo con Docker Compose
El sistema SHALL permitir levantar la plataforma completa (base de datos, API y frontend) con un único comando de Docker Compose, garantizando que cada servicio solo arranque cuando sus dependencias estén realmente disponibles.

La comprobación de salud de la base de datos SHALL invocar una herramienta que exista en la imagen que se usa. Una comprobación que invoque una ruta inexistente falla siempre, y con `condition: service_healthy` eso no retrasa el arranque de la API: lo impide para siempre.

#### Scenario: Arranque en modo producción
- **GIVEN** el fichero `docker-compose.yml` del repositorio
- **WHEN** se ejecuta `docker-compose up -d` sin especificar perfil
- **THEN** el sistema arranca los servicios `sqlserver`, `api` y `web`
- **AND** el servicio `mailhog` MUST NOT arrancar, al estar declarado bajo el perfil `dev`

#### Scenario: La API espera a que SQL Server esté saludable antes de arrancar
- **GIVEN** el servicio `sqlserver` con un `healthcheck` basado en `sqlcmd -Q 'SELECT 1'`
- **WHEN** se levanta el stack con `docker-compose up`
- **THEN** el servicio `api` SHALL permanecer sin arrancar hasta que la condición `service_healthy` de `sqlserver` se cumpla

#### Scenario: La comprobación de salud llega a pasar
- **GIVEN** la imagen de SQL Server que declara `docker-compose.yml`
- **WHEN** el contenedor termina de arrancar
- **THEN** la comprobación de salud SHALL pasar a `healthy`, de forma que `api` arranque
- **AND** la comprobación SHALL invocar la ruta de `sqlcmd` que existe en esa imagen

## ADDED Requirements

### Requirement: Herramienta de rotación de secretos
El repositorio SHALL incluir un guion que genere valores nuevos para los secretos del despliegue y los escriba en el fichero de entorno local.

El guion SHALL generarlos en la máquina de quien lo ejecuta. NEVER SHALL enviarlos a ningún servicio, NEVER SHALL escribirlos en la salida estándar y NEVER SHALL dejarlos en un fichero versionado.

El guion SHALL NOT rotar por sí solo: cambiar la contraseña de la base de datos en el servidor, revocar la credencial del proveedor de correo y reasignar la del administrador son pasos que exigen acceso a esos sistemas. El repositorio SHALL documentarlos como lista de comprobación.

#### Scenario: Generación de valores nuevos
- **WHEN** una persona ejecuta el guion de rotación
- **THEN** el guion SHALL escribir un fichero de entorno con valores generados al azar
- **AND** SHALL NOT mostrar ninguno de esos valores por pantalla

#### Scenario: No se sobrescribe lo que ya existe sin avisar
- **GIVEN** un fichero de entorno ya presente
- **WHEN** se ejecuta el guion
- **THEN** el guion SHALL detenerse o guardar una copia del anterior, en lugar de perder valores que pueden estar en uso

#### Scenario: Lo que el guion no puede hacer queda escrito
- **WHEN** alguien consulta la documentación de la rotación
- **THEN** SHALL encontrar la lista de pasos manuales, con el aviso de que rotar la clave de firma cierra todas las sesiones abiertas, incluidos los exámenes en curso
