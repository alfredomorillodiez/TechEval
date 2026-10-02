## MODIFIED Requirements

### Requirement: Levantamiento del stack completo con Docker Compose
El sistema SHALL permitir levantar la plataforma completa (base de datos, API y frontend) con un único comando de Docker Compose, garantizando que cada servicio solo arranque cuando sus dependencias estén realmente disponibles.

La comprobación de salud de la base de datos SHALL invocar una herramienta que exista en la imagen que se usa. Una comprobación que invoque una ruta inexistente falla siempre, y con `condition: service_healthy` eso no retrasa el arranque de la API: lo impide para siempre.

#### Scenario: Arranque en modo producción
- **GIVEN** el fichero `docker-compose.yml` del repositorio
- **WHEN** se ejecuta `docker-compose up -d`
- **THEN** el sistema arranca los servicios `sqlserver`, `api` y `web`

#### Scenario: La API espera a que SQL Server esté saludable antes de arrancar
- **GIVEN** el servicio `sqlserver` con un `healthcheck` basado en `sqlcmd -Q 'SELECT 1'`
- **WHEN** se levanta el stack con `docker-compose up`
- **THEN** el servicio `api` SHALL permanecer sin arrancar hasta que la condición `service_healthy` de `sqlserver` se cumpla

#### Scenario: La comprobación de salud llega a pasar
- **GIVEN** la imagen de SQL Server que declara `docker-compose.yml`
- **WHEN** el contenedor termina de arrancar
- **THEN** la comprobación de salud SHALL pasar a `healthy`, de forma que `api` arranque
- **AND** la comprobación SHALL invocar la ruta de `sqlcmd` que existe en esa imagen

## REMOVED Requirements

### Requirement: Perfil de desarrollo incluye MailHog para captura local de correos
**Reason**: el correo ya no sale por SMTP sino por Microsoft Graph, y MailHog solo captura SMTP. Por SMTP, la cuenta autenticada y el `From` no coincidían, y el departamento de sistemas recibía alertas de suplantación.
**Migration**: no hay servidor de captura para Graph. En desarrollo, lo que se envía llega de verdad: hay que probar con direcciones propias. Sin la configuración de `Email:Office365`, la API de desarrollo arranca igual y cada envío falla con su error en el log.

## ADDED Requirements

### Requirement: El correo sale por Microsoft Graph en nombre del buzón remitente
El sistema SHALL enviar el correo con Microsoft Graph, con el mismo mecanismo que `EmailService365` de iECS: una aplicación de Entra ID obtiene un token con sus credenciales de cliente y publica el mensaje en nombre del buzón configurado en `Email:FromEmail`.

El remitente SHALL ser siempre el buzón en cuyo nombre se publica. El sistema NEVER SHALL enviar por SMTP ni usar la contraseña de un buzón.

El token SHALL ir en cada petición y NEVER SHALL quedar escrito en el log.

#### Scenario: Envío de un correo
- **GIVEN** `Email:FromEmail` y los tres valores de `Email:Office365` configurados
- **WHEN** el sistema envía un correo
- **THEN** SHALL publicar en `POST https://graph.microsoft.com/v1.0/users/{FromEmail}/sendMail` con un token de aplicación en la cabecera `Authorization`
- **AND** el cuerpo SHALL llegar como HTML sin modificar

#### Scenario: Graph rechaza el envío
- **GIVEN** un buzón inexistente o una aplicación sin el permiso `Mail.Send`
- **WHEN** el sistema envía un correo
- **THEN** el envío SHALL fallar con un error que incluya el código de estado y la explicación que devuelve Graph

#### Scenario: Arranque en producción sin la configuración del correo
- **GIVEN** un despliegue con el entorno distinto de desarrollo y sin uno de `Email:FromEmail`, `Email:Office365:TenantId`, `Email:Office365:ClientId` o `Email:Office365:ClientSecret`
- **WHEN** la aplicación arranca
- **THEN** el sistema MUST detener el arranque con un error que nombre la clave que falta

#### Scenario: Arranque en desarrollo sin la configuración del correo
- **GIVEN** el entorno de desarrollo sin los valores de `Email:Office365`
- **WHEN** la aplicación arranca y después intenta enviar un correo
- **THEN** la aplicación MUST arrancar con normalidad
- **AND** el envío SHALL fallar sin llegar a llamar a Graph, con su error en el log
