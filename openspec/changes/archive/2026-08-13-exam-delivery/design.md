## Context
Una vez que un examen existe (capacidad `exam-management`), hace falta un mecanismo para entregárselo de forma segura a un candidato concreto sin exponer las preguntas a terceros ni permitir reintentos indefinidos. La entrega ocurre siempre desde el panel de administración (`POST /api/exams/send` / `POST /api/exams/send-bulk`, protegidos con `[Authorize(Roles = "Admin")]`) y termina en la bandeja de entrada del candidato, que no tiene cuenta ni autenticación en la plataforma. El único "secreto" que protege el acceso al examen es el propio token incluido en la URL.

## Goals / Non-Goals

**Goals**
- Emitir un token de acceso que sea prácticamente imposible de adivinar o fuerza-bruta.
- Garantizar que cada token quede ligado de forma inequívoca a un examen y a un candidato.
- Permitir que el administrador configure cuánto tiempo permanece válido el enlace antes de que caduque.
- Notificar al candidato con un email profesional y autoexplicativo (marca, botón de acción, aviso de caducidad y de un solo uso).
- Mantener el envío de email desacoplado del proveedor SMTP concreto, para poder cambiarlo sin tocar la lógica de negocio.
- Soportar el envío a un candidato o a un lote de candidatos con el mismo nivel de garantías.

**Non-Goals**
- La validación, inicio, respuesta y envío de las respuestas del examen por parte del candidato (capacidad `exam-taking`).
- La renovación o reenvío de un token expirado o consumido (se trata como un nuevo envío, con un nuevo token).
- Plantillas de email configurables por el administrador (la plantilla HTML es fija en código).
- Rate limiting o CAPTCHA sobre los endpoints públicos de examen (recomendado en `documentacion.md` §13 como mejora futura, fuera del alcance de esta capacidad).

## Decisions

### Tamaño y generación del token
- Se generan **48 bytes** aleatorios con `System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)` — un generador criptográficamente seguro (CSPRNG), no `System.Random`.
- Los bytes se codifican en **Base64** y se transforman a **URL-safe** reemplazando `+` → `-`, `/` → `_` y eliminando el padding `=`. El resultado es una cadena de **64 caracteres** apta para viajar en una URL (`{baseUrl}/exam/{token}`) sin necesidad de escapado adicional.
- La generación vive en `ITokenService.GenerateSecureToken()` (implementada en `TokenService`, junto con la emisión de JWT de administradores), de modo que `ExamTokenService` no conoce el mecanismo criptográfico concreto, solo el contrato.
- **Unicidad**: no se comprueba unicidad de forma explícita en el código de aplicación; se delega en un **índice único** sobre la columna `Token` de la tabla `ExamTokens`. Con 48 bytes de entropía (2^384 combinaciones posibles) la probabilidad de colisión es despreciable, y el índice único actúa como red de seguridad ante cualquier fallo teórico del generador.
- Cada token queda vinculado en el mismo registro `ExamToken` a `ExamId`, `CandidateName`, `CandidateEmail`, `CreatedAt` y `ExpiresAt`, y expone `IsExpired`/`IsValid` como propiedades calculadas sobre `IsUsed` y `ExpiresAt`, evitando duplicar esa lógica de validez en cada consumidor.

### Expiración configurable
- El campo `SendExamDto.ExpirationHours` (por defecto `72`) determina cuántas horas, a partir de `DateTime.UtcNow`, permanece válido el token (`ExpiresAt = DateTime.UtcNow.AddHours(dto.ExpirationHours)`).
- Se usa UTC de forma consistente en `CreatedAt`/`ExpiresAt` para evitar ambigüedades de zona horaria entre el servidor y el candidato; el email muestra la fecha de expiración con el sufijo "UTC" explícito.

### ¿Por qué SMTP directo y no SendGrid SDK?
- `IEmailService` (en `TechEval.Domain.Interfaces.Services`) define el contrato (`SendExamInvitationAsync`, `SendExamResultAsync`) sin exponer ningún detalle de transporte. `ExamTokenService` solo depende de esa interfaz.
- `SmtpEmailService` (en `TechEval.Infrastructure.Email`) la implementa usando `System.Net.Mail.SmtpClient` sobre `EmailSettings` (host, puerto, credenciales, `EnableSsl`), lo que funciona con **cualquier servidor SMTP compatible**: SendGrid, Gmail, Mailtrap o MailHog en desarrollo local (ver `documentacion.md` §8).
- Si en el futuro se necesita el SDK propietario de SendGrid (por ejemplo, para usar plantillas dinámicas de SendGrid o webhooks de entrega), bastaría crear una nueva clase `SendGridEmailService : IEmailService` y registrarla en `DependencyInjection.AddInfrastructure` en lugar de `SmtpEmailService`, sin tocar `ExamTokenService` ni el controlador. Esta decisión prioriza la portabilidad entre proveedores sobre las funcionalidades avanzadas específicas de un SDK propietario.
- La plantilla del email es HTML inline (`string` con interpolación, generada en `BuildInvitationHtml`), sin motor de plantillas externo, para mantener la implementación autocontenida y sin dependencias adicionales.

### Envío masivo con aislamiento de errores
- `SendExamBulkAsync` itera la lista de candidatos y envuelve la generación de token + envío de email de **cada candidato** en su propio `try/catch`, acumulando un `BulkSendItemResultDto` (éxito/fallo + mensaje) por candidato en vez de abortar todo el lote ante el primer error (por ejemplo, un email con formato inválido que provoque una excepción en `SmtpClient`).
- Esto prioriza la resiliencia del envío masivo (maximizar candidatos notificados) sobre la atomicidad transaccional del lote completo.

## Risks / Trade-offs
- **Sin revocación manual de tokens**: si un token se filtra antes de expirar, no hay endpoint para invalidarlo manualmente; solo expira por tiempo o se consume con el primer uso. Mitigación parcial: ventanas de expiración cortas (72h por defecto) configurables por el administrador según sensibilidad del proceso.
- **Entrega de email no garantizada**: `SmtpClient.SendMailAsync` puede fallar (SMTP caído, credenciales inválidas, email rechazado) después de que el token ya se haya persistido en `SendExamAsync` (envío individual) — el token queda creado en base de datos aunque el correo no llegue, y no hay reintento automático. En el envío masivo este riesgo se mitiga registrando el fallo por candidato, pero en el envío individual actual la excepción se propaga sin deshacer la creación del token.
- **Dependencia de un CSPRNG correcto**: toda la seguridad del enlace descansa en `RandomNumberGenerator`; un fallo de la biblioteca criptográfica subyacente (extremadamente improbable en .NET) comprometería la imprevisibilidad del token. No se añade entropía adicional (por ejemplo, ligar el token a IP o user-agent) por mantener la simplicidad del enlace de un solo uso.
- **Plantilla de email fija en código**: cambios de branding o copy requieren un despliegue de `SmtpEmailService`, no son configurables en caliente.
