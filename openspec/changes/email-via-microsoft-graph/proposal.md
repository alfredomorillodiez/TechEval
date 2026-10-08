## Why

El departamento de sistemas recibe alertas por los correos de prueba de TechEval. La causa: por SMTP, la cuenta que se autenticaba (`test@grupo-pronet.com`) y el `From` (`test.scrap-waste@pronet-ise.com`) eran buzones distintos y de dominios distintos. Exchange lo trata como suplantación. Además, con `EnableSsl = false` en el puerto 587, la contraseña del buzón viajaba sin cifrar.

Las demás aplicaciones de iECS no envían por SMTP. Usan `EmailService365` (`iECS.ERP.Application.Core.Mail`): una aplicación de Entra ID pide un token y publica el mensaje en Microsoft Graph, en nombre del buzón remitente. Con ese mecanismo el remitente es siempre el buzón que envía, y no hay contraseña de buzón.

## What Changes

- **El correo sale por Microsoft Graph.** `GraphEmailService` sustituye a `SmtpEmailService`. Pide el token con MSAL (credenciales de cliente) y publica en `POST /users/{FromEmail}/sendMail`. Las plantillas HTML y los asuntos no cambian.
- **Se elimina SMTP.** Desaparecen `SmtpEmailService` y las claves `Email:Host`, `Port`, `UserName`, `Password`, `FromName` y `EnableSsl`. Aparecen `Email:Office365:TenantId`, `ClientId` y `ClientSecret`.
- **Se elimina MailHog.** Solo capturaba SMTP. Sale de `docker-compose.yml` el servicio `mailhog` con su perfil `dev`.
- **Fuera de desarrollo, la API no arranca sin la configuración del correo.** `StartupSecrets` exige `Email:FromEmail` y los tres valores de `Email:Office365`. En desarrollo arranca igual, y cada envío falla con su error en el log.
- `docker-compose.yml`, `.env.example` y los guiones de rotación cambian `SENDGRID_API_KEY` por `O365_TENANT_ID`, `O365_CLIENT_ID` y `O365_CLIENT_SECRET`.

Respecto a `EmailService365`, `GraphEmailService` corrige tres cosas:

- Pone el token en cada petición, no en la cabecera de un `HttpClient` estático compartido entre hilos.
- No escribe el token en el log.
- No cambia los saltos de línea por `<br>`: el cuerpo ya es HTML.

Fuera de alcance:

- Crear la aplicación de Entra ID, conceder `Mail.Send` y limitarla a un buzón con una directiva de acceso de aplicación de Exchange. Lo hace el departamento de sistemas.
- Un servidor de captura local que sustituya a MailHog. Graph no tiene uno; en desarrollo, lo que se envía llega de verdad.

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities

- `deployment-ops`: se **retira** el requisito del perfil `dev` con MailHog. Se **modifica** el del levantamiento con Docker Compose, que lo nombraba. Se **añade** el requisito del envío por Microsoft Graph.

## Impact

- **Infraestructura**: `Email/GraphEmailService.cs` (antes `SmtpEmailService.cs`), `Email/EmailSettings.cs`, `DependencyInjection.cs`. Paquetes nuevos: `Microsoft.Identity.Client` y `Microsoft.Extensions.Http`.
- **API**: `StartupSecrets` exige la configuración del correo. `appsettings.json` cambia la sección `Email`.
- **Despliegue**: `docker-compose.yml` pierde `mailhog` y las variables de SendGrid. `.env.example` y `scripts/rotar-secretos.{sh,ps1}` cambian las variables.
- **Configuración local**: los `appsettings.Local.json` con claves SMTP siguen arrancando, pero ya no envían. Hay que añadir `Email:Office365`.
- **Documentación**: README, `documentacion.md` y `docs/rotacion-de-secretos.md`.
- **Pruebas**: `GraphEmailServiceTests` y una prueba más en `StartupSecretsTests`.
