## 1. Modelo de dominio
- [x] 1.1 Definir la entidad `ExamToken` (`Token`, `ExamId`, `CandidateName`, `CandidateEmail`, `CreatedAt`, `ExpiresAt`, `IsUsed`, `UsedAt`) con navegación a `Exam` y `ExamSession`.
- [x] 1.2 Añadir las propiedades calculadas `IsExpired` e `IsValid` sobre `ExamToken` para centralizar la lógica de vigencia del token.
- [x] 1.3 Definir el contrato `ITokenService.GenerateSecureToken()` en `TechEval.Domain.Interfaces.Services`.
- [x] 1.4 Definir el contrato `IEmailService` (`SendExamInvitationAsync`, `SendExamResultAsync`) en `TechEval.Domain.Interfaces.Services`.

## 2. Generación segura del token
- [x] 2.1 Implementar `TokenService.GenerateSecureToken()` usando `RandomNumberGenerator.GetBytes(48)`.
- [x] 2.2 Codificar el resultado en Base64 URL-safe (sustitución de `+`, `/` y eliminación de `=`).
- [x] 2.3 Registrar `ITokenService` en `DependencyInjection.AddInfrastructure` con ciclo de vida `Scoped`.

## 3. Persistencia del token
- [x] 3.1 Configurar la tabla `ExamTokens` con índice único sobre `Token` y clave foránea a `Exams`.
- [x] 3.2 Implementar `IExamTokenRepository`/`ExamTokenRepository` con `GetByTokenAsync` y `GetWithExamAndSessionAsync` (incluye examen, preguntas, respuestas y sesión asociada).
- [x] 3.3 Registrar `IExamTokenRepository` en el contenedor de dependencias.

## 4. Servicio de envío de examen
- [x] 4.1 Implementar `ExamTokenService.SendExamAsync`: valida que el examen exista, genera el token, calcula `ExpiresAt` a partir de `ExpirationHours` (default 72h) y persiste el `ExamToken`.
- [x] 4.2 Construir el enlace único del examen (`{baseUrl}/exam/{token}`) e invocar `IEmailService.SendExamInvitationAsync`.
- [x] 4.3 Implementar `ExamTokenService.SendExamBulkAsync` reutilizando la generación de token y envío de email por cada candidato, con `try/catch` por candidato y acumulación de resultados en `BulkSendResultDto`.

## 5. Envío de email HTML
- [x] 5.1 Implementar `EmailSettings` (host, puerto, credenciales, remitente, `EnableSsl`) enlazado a la sección `Email` de configuración.
- [x] 5.2 Implementar `SmtpEmailService` sobre `System.Net.Mail.SmtpClient` con manejo de errores y logging (`ILogger<SmtpEmailService>`).
- [x] 5.3 Crear la plantilla HTML de invitación (`BuildInvitationHtml`) con marca, botón de acción, enlace alternativo en texto y aviso de expiración/un solo uso.
- [x] 5.4 Registrar `IEmailService` → `SmtpEmailService` en `DependencyInjection.AddInfrastructure`.

## 6. Endpoints de entrega
- [x] 6.1 Implementar `POST /api/exams/send` en `ExamsController`, protegido con `[Authorize(Roles = "Admin")]`, resolviendo `baseUrl` desde `FrontendBaseUrl` o el host de la petición.
- [x] 6.2 Implementar `POST /api/exams/send-bulk` con validación de lista de candidatos no vacía y devolución del resumen de éxitos/fallos.
- [x] 6.3 Documentar ambos endpoints con comentarios XML para su aparición en Swagger.

## 7. Configuración
- [x] 7.1 Añadir la sección `Email` a `appsettings.json` (producción/SendGrid) y `appsettings.Development.json` (MailHog local).
- [x] 7.2 Añadir la clave opcional `FrontendBaseUrl` para construir el enlace del examen cuando la API y el frontend residen en dominios distintos.
