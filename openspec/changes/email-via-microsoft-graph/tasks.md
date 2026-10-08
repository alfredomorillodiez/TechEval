## 1. Envío por Microsoft Graph

- [x] 1.1 Añadir `Microsoft.Identity.Client` y `Microsoft.Extensions.Http` a `TechEval.Infrastructure`. Verificar que la solución compila.
- [x] 1.2 Convertir `SmtpEmailService` en `GraphEmailService`, conservando asuntos y plantillas. Verificar con `git status` que git lo ve como renombrado.
- [x] 1.3 Pedir el token con MSAL en un singleton, construido en el primer envío. Verificar que sin credenciales el servicio se resuelve y el envío falla sin llamar a Graph.
- [x] 1.4 Poner el token en cada petición y no escribirlo en el log. Verificar la cabecera `Authorization` en la prueba.
- [x] 1.5 Si Graph rechaza el envío, lanzar con el código y el cuerpo de la respuesta. Verificar con una respuesta 403.

## 2. Configuración

- [x] 2.1 Reducir `EmailSettings` a `FromEmail` y `Office365` (`TenantId`, `ClientId`, `ClientSecret`). Verificar que no queda ninguna clave SMTP en `appsettings.json`.
- [x] 2.2 Añadir las cuatro claves del correo a `StartupSecrets`. Verificar que fuera de desarrollo la falta de una para el arranque y la nombra.

## 3. Despliegue

- [x] 3.1 Quitar de `docker-compose.yml` el servicio `mailhog` y las variables de SendGrid, y añadir `O365_TENANT_ID`, `O365_CLIENT_ID` y `O365_CLIENT_SECRET`. Verificar que no queda ninguna referencia a SMTP.
- [x] 3.2 Cambiar las variables en `.env.example` y en `scripts/rotar-secretos.{sh,ps1}`. Verificar con una búsqueda que no queda `SENDGRID_API_KEY`.

## 4. Documentación

- [x] 4.1 Actualizar README, `documentacion.md` y `docs/rotacion-de-secretos.md`. Verificar con una búsqueda que SMTP y MailHog solo aparecen como historia.

## 5. Pruebas

- [x] 5.1 Añadir `GraphEmailServiceTests`: petición a Graph, contenido del mensaje, rechazo de Graph, resolución y envío sin credenciales. Verificar que pasan.
- [x] 5.2 Añadir a `StartupSecretsTests` la falta del secreto de cliente. Verificar que pasa.
- [x] 5.3 Ejecutar `dotnet test TechEval.sln` completo. Verificar que no falla ninguna prueba.

## 6. Cierre

- [x] 6.1 Pedir al departamento de sistemas la aplicación de Entra ID con `Mail.Send`, limitada al buzón de `FromEmail`. Recibida el 02·10·2026: `test.scrap-waste.sendmail.app`, inquilino de `pronet-ise.com`, buzón `test.scrap-waste@pronet-ise.com`. Su token no trae `roles`: el permiso lo da Exchange (RBAC para aplicaciones).
- [ ] 6.2 Con esas credenciales, enviar un correo de prueba a una dirección propia. Verificar que llega y que sistemas no recibe alertas. Enviado y recibido el 02·10·2026 a las 09:31 UTC; falta que sistemas confirme que no hubo alerta.
