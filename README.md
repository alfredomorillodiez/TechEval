# TechEval

Plataforma de evaluación técnica para gestionar bancos de preguntas, generar exámenes y evaluar candidatos mediante un enlace de un solo uso enviado por email, con portal propio para el alumno y corrección manual de las preguntas abiertas.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-WebAssembly-512BD4?logo=blazor)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)

---

## Características

### Banco de preguntas y exámenes

- **Banco de preguntas** — tipo test (4 opciones) y respuesta abierta, con categorías y niveles de dificultad
- **Generación de exámenes** — manual o automática con preguntas aleatorias por categoría y dificultad
- **Envío por email** — enlace único por candidato con expiración configurable; envío masivo a múltiples candidatos desde CSV o lista manual
- **Examen del candidato** — temporizador, auto-guardado de respuestas y envío automático al agotar el tiempo
- **Reanudación de la prueba** — una recarga, un cierre de pestaña o una pérdida de red no expulsan al candidato: vuelve a sus preguntas con las respuestas que ya tenía y con el tiempo restante que calcula el servidor. La invitación caducada no corta una prueba ya empezada
- **Corrección automática** — para preguntas tipo test; preguntas abiertas pendientes de revisión manual
- **Dashboard de resultados** — historial, estadísticas y detalle por candidato, con filtros combinables

### Corrección manual de preguntas abiertas

- **Cola de pendientes** — un resultado con preguntas abiertas queda en estado `PendingReview` hasta que un administrador lo corrige; la cola se ordena del más antiguo al más reciente
- **Pantalla de corrección** — muestra cada respuesta del candidato junto a la respuesta de referencia, y admite puntuación y comentario por respuesta
- **Corrección completa** — el envío corrige todas las respuestas abiertas de una vez; no se admite la corrección parcial
- **Cierre del resultado** — al enviar la corrección, el sistema recalcula la nota, fija el veredicto, pasa el resultado a `Reviewed` y avisa al candidato
- **Trazabilidad** — cada resultado guarda el administrador que lo corrigió (`ReviewedByUserId`)
- **Sin doble corrección** — un resultado ya corregido devuelve `409 Conflict` ante un segundo intento

### Cuentas y portal del alumno

- **Dos roles** — `Admin` (consola de administración) y `Alumno` (portal propio)
- **Aprovisionamiento automático** — al abrir por primera vez el enlace de invitación se crea la cuenta del alumno a partir del email del candidato
- **Auto-login desde la invitación** — validar el token devuelve un JWT con rol `Alumno`; el candidato no necesita credenciales para hacer la prueba
- **Portal del alumno** (`/portal`) — sus pruebas pendientes (sin empezar, con fecha de expiración, o a medias con acceso directo a continuarlas) y su historial de pruebas realizadas con nota y aprobado/suspenso
- **Aislamiento por usuario** — cada alumno ve únicamente sus propias invitaciones y resultados

---

## Stack tecnológico

| Capa | Tecnología | Versión |
|------|-----------|---------|
| Runtime | .NET | 9.0 |
| Backend | ASP.NET Core API | 9.0.0 |
| Frontend | Blazor WebAssembly | 9.0.0 |
| ORM | Entity Framework Core | 9.0.0 |
| Base de datos | SQL Server | 2022 |
| Autenticación | JWT Bearer (roles `Admin` / `Alumno`) | 9.0.0 |
| Documentación API | Swagger (Swashbuckle) | 7.2.0 |
| Logging | Serilog (consola + fichero diario) | 9.0.0 |
| Contenedores | Docker / Docker Compose | — |

---

## Requisitos previos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- SQL Server 2019+ (o Docker)
- Cuenta SMTP (SendGrid, Gmail…) — o [MailHog](https://github.com/mailhog/MailHog) para desarrollo

---

## Inicio rápido con Docker

```bash
# Clonar el repositorio
git clone https://github.com/alfredomorillodiez/TechEval.git
cd TechEval

# Arrancar SQL Server + API + Web + MailHog (entorno de desarrollo)
docker-compose --profile dev up -d
```

| Servicio | URL |
|----------|-----|
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| Web (Blazor) | http://localhost:5001 |
| MailHog (emails) | http://localhost:8025 |

**Administrador:** `admin@techeval.com`. Su contraseña sale de `AdminPassword`, que **no tiene valor por defecto**. En desarrollo la trae `appsettings.Development.json` con el valor público `Admin@123!`. Fuera de desarrollo, la API se niega a arrancar si falta, y también si conserva ese valor de desarrollo.

> Las cuentas de alumno se crean solas al abrir una invitación, y **nacen sin contraseña utilizable**: el candidato entra por el enlace de la invitación, que ya lo autentica. Hasta el 16·09·2026 la contraseña era la parte local de su email, así que cualquiera que conociese el email entraba en su portal.

> **Antes de levantar el stack**, copia `.env.example` a `.env` y rellena sus valores. Ni `docker-compose.yml` ni `appsettings.json` llevan secretos: si falta alguno, el arranque para y dice cuál.
>
> ```bash
> cp .env.example .env    # y edita los valores
> ```
>
> **Aviso de seguridad**: la contraseña de `sa`, la clave JWT, la contraseña del administrador y las credenciales SMTP **estuvieron versionadas** hasta el 16·09·2026. Siguen en el historial de git, así que hay que darlas por comprometidas: no basta con moverlas, hay que **rotarlas**. El procedimiento está en [docs/rotacion-de-secretos.md](docs/rotacion-de-secretos.md), con el guion que genera los valores nuevos.

---

## Instalación manual

### 1. Configurar la base de datos

Editar `src/TechEval.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=TechEvalDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### 2. Aplicar migraciones

```bash
cd src/TechEval.Infrastructure
dotnet ef database update --startup-project ../TechEval.API
```

Como alternativa, `scripts/create_database.sql` crea el esquema completo desde cero. Sobre una base de datos **ya existente** creada con una versión anterior, aplica los scripts incrementales por orden. Todos son idempotentes:

```bash
# Cuentas de alumno: Users.Username, ExamTokens.UserId, ExamResults.UserId
sqlcmd -S localhost -d TechEvalDb -i scripts/add_user_link_columns.sql

# Corrección manual: ExamResults.Status/ReviewedByUserId, UserAnswers.AwardedPoints/ReviewerComment
sqlcmd -S localhost -d TechEvalDb -i scripts/add_review_columns.sql

# Retirada del pipeline de IA (solo en bases de datos anteriores a su eliminación)
sqlcmd -S localhost -d TechEvalDb -i scripts/remove_ai_generation.sql
```

### 3. Configurar email (desarrollo)

```bash
docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog
```

### 4. Arrancar API y frontend

```bash
# Terminal 1
cd src/TechEval.API && dotnet run

# Terminal 2
cd src/TechEval.Web && dotnet run
```

---

## Configuración

Las claves viven en `appsettings.json` y se pueden sobrescribir por entorno (`appsettings.Development.json`, `appsettings.Local.json` — este último ignorado por git) o mediante variables de entorno usando doble guion bajo (`Jwt__SecretKey`).

| Clave | Descripción | Por defecto |
|-------|-------------|-------------|
| `ConnectionStrings:DefaultConnection` | Cadena de conexión a SQL Server | `Server=localhost;Database=TechEvalDb;…` |
| `Jwt:SecretKey` | Clave de firma HMAC-SHA256 — **obligatoria, sin valor por defecto** | vacío |
| `Jwt:ExpirationHours` | Vigencia del token de sesión | `8` |
| `Email:*` | Host, puerto, credenciales y remitente SMTP | vacío |
| `FrontendBaseUrl` | Base con la que se construyen los enlaces de invitación | `https://localhost:60805` |
| `AllowedOrigins` | Orígenes CORS permitidos en producción (separados por coma) | `http://localhost:5001` |
| `AdminPassword` | Contraseña del administrador creado en el primer arranque — **obligatoria** | vacío |
| `Jwt:Issuer` / `Jwt:Audience` | Emisor y destinatario del token | `TechEvalAPI` / `TechEvalClient` |

### Límite de ritmo

`POST /api/auth/login` admite 10 peticiones por minuto y dirección de origen. `GET /api/exam/validate/{token}` admite 60. Al superarlo, la API responde `429` con una cabecera `Retry-After`.

El motivo es el coste: verificar una contraseña cuesta cientos de milisegundos de CPU desde que el hash es PBKDF2, así que un volumen moderado de intentos deja al servidor sin hilos aunque ninguno acierte.

> **Si despliegas detrás de un proxy inverso**, declara sus direcciones en `ForwardedHeadersOptions`. Sin hacerlo, todas las peticiones llegan con la dirección del proxy: el cupo se comparte entre todos los candidatos y el primero que llegue agota el de los demás.
>
> No confíes en `X-Forwarded-For` de cualquier origen. Sin restringir qué proxies pueden fijarla, cualquiera se inventa su dirección y se salta el límite. Por eso la lista de proxies de confianza va vacía por defecto y la cabecera se ignora.

---

## Arquitectura

Clean Architecture en 4 capas con separación estricta de dependencias:

```
TechEval.Web (Blazor WASM)
        │  HTTP / REST
TechEval.API  (Controllers · JWT · Swagger · Middleware)
        │
TechEval.Application  (Services · DTOs · Validaciones)
        │
TechEval.Domain  (Entities · Interfaces · Enums)
        │
TechEval.Infrastructure  (EF Core · Repositorios · SMTP · JWT)
        │
    SQL Server
```

Domain no depende de ninguna otra capa. Application define los puertos —servicios de negocio, repositorios y envío de email— e Infrastructure los implementa. Así, un cambio de ORM o de proveedor de correo no toca ni Application ni Domain.

---

## Estructura del proyecto

```
TechEval/
├── src/
│   ├── TechEval.API/            # Controladores y middleware
│   ├── TechEval.Application/    # Lógica de negocio y DTOs
│   ├── TechEval.Domain/         # Entidades, enums e interfaces
│   ├── TechEval.Infrastructure/ # EF Core, email y JWT
│   └── TechEval.Web/            # Frontend Blazor WASM (admin + portal del alumno)
├── tests/
│   └── TechEval.Tests/          # Tests unitarios (xUnit)
├── scripts/
│   ├── create_database.sql        # Esquema completo, alternativa a migraciones
│   ├── add_user_link_columns.sql  # Incremental: cuentas de alumno
│   ├── add_review_columns.sql     # Incremental: corrección manual de abiertas
│   ├── remove_ai_generation.sql   # Incremental: retirada del pipeline de IA
│   ├── reset_exam_history.sql     # Limpieza del histórico de intentos
│   └── seed_questions_*.sql       # Banco de preguntas inicial
├── openspec/                    # Especificación viva del sistema e histórico de cambios
├── docker-compose.yml
├── Dockerfile.api · Dockerfile.web · nginx.conf
└── documentacion.md             # Documentación técnica completa
```

---

## Rutas del frontend

| Ruta | Rol | Descripción |
|------|-----|-------------|
| `/` · `/login` | — | Acceso de administradores y alumnos (email o usuario) |
| `/admin` · `/admin/dashboard` | Admin | Dashboard con indicadores generales |
| `/admin/questions` | Admin | Banco de preguntas con filtros |
| `/admin/questions/new` · `/admin/questions/{id}` | Admin | Alta y edición de preguntas |
| `/admin/pruebas` · `/admin/pruebas/{id}` | Admin | Listado de pruebas y detalle |
| `/admin/pruebas/new` · `/admin/pruebas/generate` | Admin | Creación manual y generación automática |
| `/admin/results` · `/admin/results/{id}` | Admin | Resultados globales y detalle |
| `/admin/results/prueba/{examId}` | Admin | Resultados de una prueba concreta |
| `/admin/results/pending` | Admin | Cola de resultados pendientes de corrección |
| `/admin/results/{id}/review` | Admin | Corrección de las preguntas abiertas de un resultado |
| `/portal` | Alumno | Pruebas pendientes y realizadas del alumno |
| `/exam/{token}` · `/prueba/{token}` | Público | Apertura de la invitación y resolución del examen |

---

## API

Swagger publica la referencia completa en `/swagger` (solo en desarrollo). Resumen de endpoints:

| Método | Endpoint | Autorización |
|--------|----------|--------------|
| `POST` | `/api/auth/login` | Público |
| `GET` `POST` `PUT` `DELETE` | `/api/categories` | Admin |
| `GET` `POST` `PUT` `DELETE` | `/api/questions` | Admin |
| `GET` `POST` `PUT` `DELETE` | `/api/exams` | Admin |
| `POST` | `/api/exams/generate` | Admin |
| `POST` | `/api/exams/send` · `/api/exams/send-bulk` | Admin |
| `GET` | `/api/results` · `/api/results/{id}` · `/api/results/exam/{examId}` · `/api/results/dashboard` | Admin |
| `GET` | `/api/review/pending` · `/api/review/{resultId}` | Admin |
| `POST` | `/api/review/{resultId}` | Admin |
| `GET` | `/api/student/pending` · `/api/student/completed` | Alumno |
| `GET` | `/api/exam/validate/{token}` | Público — devuelve el JWT de alumno |
| `POST` | `/api/exam/start/{token}` · `/api/exam/answer/{sessionId}` · `/api/exam/submit` | Público — token del enlace |

---

## Flujo de corrección manual

```
Candidato  →  POST /api/exam/submit
             │  corrección automática de las preguntas tipo test
             │
             ├─ sin preguntas abiertas  →  ExamResult = Reviewed · nota y veredicto firmes
             │
             └─ con preguntas abiertas  →  ExamResult = PendingReview · Passed = NULL
                          │
                          ▼
Admin  →  /admin/results/pending        cola del más antiguo al más reciente
                          │
                          ▼
Admin  →  /admin/results/{id}/review    puntuación y comentario por respuesta
                          │  POST /api/review/{resultId}   (todas las abiertas a la vez)
                          ▼
   ExamResult = Reviewed · nota recalculada · veredicto fijado · aviso al candidato
```

Estado implicado: `ExamResultStatus` (`PendingReview` · `Reviewed`). Cada respuesta guarda los puntos otorgados en `UserAnswers.AwardedPoints` y el comentario del corrector en `UserAnswers.ReviewerComment`.

---

## Especificaciones (OpenSpec)

El directorio [`openspec/`](openspec/) mantiene la especificación viva del sistema, dividida en 13 capacidades:

`admin-console` · `ai-question-generation` · `authentication` · `candidate-experience` · `deployment-ops` · `exam-delivery` · `exam-management` · `exam-results` · `exam-taking` · `open-question-review` · `project-architecture` · `question-bank` · `student-portal`

> `ai-question-generation` describe una capacidad ya retirada del código. Su spec sigue en `openspec/specs/` a la espera de archivarse.

Cada cambio funcional se propone en `openspec/changes/` (propuesta, diseño, deltas de spec y tareas) y, una vez implementado, se archiva en `openspec/changes/archive/` fusionando sus deltas en las specs principales.

---

## Tests

```bash
cd tests/TechEval.Tests
dotnet test
```

---

## Documentación

La documentación técnica completa (diseño de BD, API reference, flujos, decisiones técnicas) está en [`documentacion.md`](documentacion.md).
