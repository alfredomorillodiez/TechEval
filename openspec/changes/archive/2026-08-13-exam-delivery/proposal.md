## Why
Un examen técnico solo aporta valor si llega al candidato correcto de forma segura y controlada. Sin un mecanismo dedicado de entrega, el equipo de reclutamiento tendría que compartir manualmente las preguntas (por correo suelto, chat, etc.), sin control de expiración, sin garantía de un único intento y sin trazabilidad de a quién se le envió cada examen. Se necesita un canal de entrega que genere un enlace de acceso único e infalsificable por candidato, con caducidad configurable, y que lo notifique automáticamente por email con una plantilla profesional.

## What Changes
- Generación de un **token de acceso criptográficamente seguro** (48 bytes aleatorios vía `RandomNumberGenerator`, codificado en Base64 URL-safe de 64 caracteres) para cada envío de examen a un candidato.
- **Unicidad garantizada** del token mediante índice único en base de datos (`ExamTokens.Token`), eliminando cualquier riesgo de colisión entre candidatos o exámenes.
- **Expiración configurable por horas** (`ExpirationHours`, 72h por defecto) calculada en el momento del envío y almacenada en `ExamToken.ExpiresAt`.
- Persistencia del vínculo **token → examen → candidato**: cada `ExamToken` queda asociado a un único `ExamId`, `CandidateName` y `CandidateEmail`, de forma que un token solo puede usarse para el examen y la persona para los que fue emitido.
- **Envío de email HTML** al candidato con el enlace único de acceso (`{baseUrl}/exam/{token}`), el título del examen y la fecha/hora de expiración, a través de un `IEmailService` desacoplado de la implementación concreta de envío.
- Endpoint `POST /api/exams/send` (protegido con rol `Admin`) que orquesta la generación del token, su persistencia y el envío del correo en una sola operación, devolviendo el token generado y un mensaje de confirmación.
- Soporte de **envío masivo** (`POST /api/exams/send-bulk`) que repite el mismo flujo de generación de token + envío de email para una lista de candidatos, informando de éxitos y fallos por destinatario sin abortar el lote completo ante un error individual.

## Capabilities
### New Capabilities
- `exam-delivery`: Entrega segura de exámenes a candidatos mediante tokens de acceso de un solo uso, criptográficamente seguros y con expiración configurable, notificados por email HTML con enlace único. Incluye el envío individual y el envío masivo a múltiples candidatos.

### Modified Capabilities
(ninguna)

## Impact
- **Código afectado**: `src/TechEval.Domain/Entities/ExamToken.cs`, `src/TechEval.Domain/Interfaces/Services/IEmailService.cs`, `src/TechEval.Domain/Interfaces/Services/ITokenService.cs`, `src/TechEval.Domain/Interfaces/Repositories/IExamTokenRepository.cs`, `src/TechEval.Application/Services/ExamTokenService.cs` (métodos `SendExamAsync`/`SendExamBulkAsync`), `src/TechEval.Infrastructure/Security/TokenService.cs`, `src/TechEval.Infrastructure/Email/SmtpEmailService.cs`, `src/TechEval.Infrastructure/Repositories/ExamTokenRepository.cs`, `src/TechEval.API/Controllers/ExamsController.cs` (endpoints `/send` y `/send-bulk`).
- **Dependencias añadidas**: `System.Net.Mail`/`System.Net` (SMTP nativo de .NET), `System.Security.Cryptography` (`RandomNumberGenerator`).
- **Sistemas externos**: servidor SMTP (SendGrid, Gmail, Mailtrap o MailHog en desarrollo) configurado mediante `EmailSettings`.
- **Configuración**: sección `Email` en `appsettings.json`/`appsettings.Development.json` (`Host`, `Port`, `UserName`, `Password`, `FromEmail`, `FromName`, `EnableSsl`) y clave opcional `FrontendBaseUrl` para construir el enlace del examen.
- **Base de datos**: tabla `ExamTokens` con índice único sobre `Token` y clave foránea a `Exams`.
