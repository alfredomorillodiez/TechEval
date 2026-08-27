# TechEval

Plataforma de evaluación técnica para gestionar bancos de preguntas, generar exámenes y evaluar candidatos mediante un enlace de un solo uso enviado por email, con portal propio para el alumno y generación de preguntas asistida por IA local.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-WebAssembly-512BD4?logo=blazor)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver)
![Ollama](https://img.shields.io/badge/IA-Ollama%20local-000000?logo=ollama)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)

---

## Características

### Banco de preguntas y exámenes

- **Banco de preguntas** — tipo test (4 opciones) y respuesta abierta, con categorías y niveles de dificultad
- **Generación de exámenes** — manual o automática con preguntas aleatorias por categoría y dificultad
- **Envío por email** — enlace único por candidato con expiración configurable; envío masivo a múltiples candidatos desde CSV o lista manual
- **Examen del candidato** — temporizador, auto-guardado de respuestas y envío automático al agotar el tiempo
- **Corrección automática** — para preguntas tipo test; preguntas abiertas pendientes de revisión manual
- **Dashboard de resultados** — historial, estadísticas y detalle por candidato, con filtros combinables

### Generación de preguntas con IA

- **Generación por lotes** — a partir de un tema en texto libre, para una categoría, dificultad y tipo dados (entre 1 y 20 preguntas por lote)
- **Procesamiento en segundo plano** — los lotes se encolan y se procesan de uno en uno; la API responde de inmediato sin esperar al modelo
- **Aislamiento de errores** — si el modelo devuelve una respuesta mal formada, solo falla esa pregunta; el resto del lote continúa
- **Revisión obligatoria** — toda pregunta generada nace en `PendingReview`; hay que aprobarla (o editarla y aprobarla) antes de que sea elegible para pruebas
- **Progreso en vivo** — la bandeja de revisión refresca el estado de los lotes en curso cada 5 s sin recargar la página
- **Categorías excluibles** — el flag `AllowsAiGeneration` deja categorías fuera del generador (p. ej. contenido propietario) sin afectar a la creación manual
- **Recuperación tras reinicio** — al arrancar, los lotes que quedaron a medias se marcan como fallidos en lugar de quedar colgados indefinidamente

### Cuentas y portal del alumno

- **Dos roles** — `Admin` (consola de administración) y `Alumno` (portal propio)
- **Aprovisionamiento automático** — al abrir por primera vez el enlace de invitación se crea la cuenta del alumno a partir del email del candidato
- **Auto-login desde la invitación** — validar el token devuelve un JWT con rol `Alumno`; el candidato no necesita credenciales para hacer la prueba
- **Portal del alumno** (`/portal`) — sus pruebas pendientes (con fecha de expiración y acceso directo a comenzarlas) y su historial de pruebas realizadas con nota y aprobado/suspenso
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
| Trabajos en segundo plano | `BackgroundService` + `System.Threading.Channels` | — |
| IA (generación de preguntas) | Ollama local (`qwen2.5-coder:14b` por defecto) | — |
| Contenedores | Docker / Docker Compose | — |

---

## Requisitos previos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- SQL Server 2019+ (o Docker)
- Cuenta SMTP (SendGrid, Gmail…) — o [MailHog](https://github.com/mailhog/MailHog) para desarrollo
- [Ollama](https://ollama.com) — solo si se va a usar la generación de preguntas con IA

---

## Inicio rápido con Docker

```bash
# Clonar el repositorio
git clone https://github.com/alfredomorillodiez/TechEval.git
cd TechEval

# Arrancar SQL Server + API + Web + Ollama + MailHog (entorno de desarrollo)
docker-compose --profile dev up -d
```

| Servicio | URL |
|----------|-----|
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| Web (Blazor) | http://localhost:5001 |
| Ollama | http://localhost:11434 |
| MailHog (emails) | http://localhost:8025 |

**Credenciales por defecto:** `admin@techeval.com` / `Admin@123!`

> Las cuentas de alumno se crean solas al abrir una invitación. La contraseña inicial es la parte local del email (`alejandro.robles@ejemplo.com` → usuario `alejandro.robles`, contraseña `alejandro.robles`), y se puede entrar en `/login` indistintamente con el email o con el usuario.

> **Generación de preguntas con IA**: el servicio `ollama` se levanta vacío (sin modelo descargado) para no alargar el `docker-compose up` inicial. La primera vez, descarga el modelo configurado en `Ollama__Model`:
> ```bash
> docker exec ollama ollama pull qwen2.5-coder:14b
> ```
> El modelo corre por CPU (no requiere GPU) y se conserva en el volumen `ollama_data` entre reinicios del contenedor. El timeout de cada llamada al modelo es de 5 minutos.
>
> Si cambias de modelo, recuerda que `Ollama__Model` en `docker-compose.yml` tiene prioridad sobre el valor de `appsettings.json`: hay que actualizarlo ahí y descargar el modelo correspondiente.

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

Como alternativa, `scripts/create_database.sql` crea el esquema completo desde cero. Sobre una base de datos **ya existente** creada con una versión anterior, aplica los scripts incrementales (son aditivos y no borran datos):

```bash
# Cuentas de alumno: Users.Username, ExamTokens.UserId, ExamResults.UserId
sqlcmd -S localhost -d TechEvalDb -i scripts/add_user_link_columns.sql

# Flag de generación por IA a nivel de categoría
sqlcmd -S localhost -d TechEvalDb -i scripts/add_category_ai_generation_flag.sql
```

### 3. Configurar email (desarrollo)

```bash
docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog
```

### 4. Levantar el modelo de IA (opcional)

```bash
ollama serve
ollama pull qwen2.5-coder:14b
```

En desarrollo, `appsettings.Development.json` apunta a `http://localhost:11434`.

### 5. Arrancar API y frontend

```bash
# Terminal 1
cd src/TechEval.API && dotnet run

# Terminal 2
cd src/TechEval.Web && dotnet run
```

---

## Configuración

Las claves viven en `appsettings.json` y se pueden sobrescribir por entorno (`appsettings.Development.json`, `appsettings.Local.json` — este último ignorado por git) o mediante variables de entorno usando doble guion bajo (`Ollama__Model`).

| Clave | Descripción | Por defecto |
|-------|-------------|-------------|
| `ConnectionStrings:DefaultConnection` | Cadena de conexión a SQL Server | `Server=localhost;Database=TechEvalDb;…` |
| `Jwt:SecretKey` | Clave de firma HMAC-SHA256 — **cambiar en producción** | `TechEval_SuperSecretKey_…` |
| `Jwt:ExpirationHours` | Vigencia del token de sesión | `8` |
| `Email:*` | Host, puerto, credenciales y remitente SMTP | vacío |
| `FrontendBaseUrl` | Base con la que se construyen los enlaces de invitación | `https://localhost:60805` |
| `AllowedOrigins` | Orígenes CORS permitidos en producción (separados por coma) | `http://localhost:5001` |
| `AdminPassword` | Contraseña del administrador creado en el primer arranque | `Admin@123!` |
| `Ollama:BaseUrl` | Endpoint del servidor Ollama | `http://ollama:11434` |
| `Ollama:Model` | Modelo usado para generar preguntas | `qwen2.5-coder:14b` |
| `Ollama:Temperature` | Creatividad del muestreo | `0.6` |
| `Ollama:RepeatPenalty` | Penalización de repetición, evita enunciados clonados | `1.3` |
| `Ollama:NumCtx` / `Ollama:NumPredict` | Ventana de contexto y tokens máximos de respuesta | `8192` / `2048` |

---

## Arquitectura

Clean Architecture en 4 capas con separación estricta de dependencias:

```
TechEval.Web (Blazor WASM)
        │  HTTP / REST
TechEval.API  (Controllers · JWT · Swagger · BackgroundServices)
        │
TechEval.Application  (Services · DTOs · Validaciones)
        │
TechEval.Domain  (Entities · Interfaces · Enums)
        │
TechEval.Infrastructure  (EF Core · Repositorios · SMTP · JWT · Ollama)
        │
    SQL Server
```

La generación con IA se apoya en una cola en memoria (`IBackgroundTaskQueue`, singleton sobre `Channel<int>`) que consume el `QuestionGenerationWorker` alojado en la API. El acceso al modelo está detrás de `IQuestionGenerationAiService`, implementado en Infrastructure por `OllamaQuestionGenerationService`, de modo que cambiar de proveedor de IA no toca ni Application ni Domain.

---

## Estructura del proyecto

```
TechEval/
├── src/
│   ├── TechEval.API/            # Controladores, middleware y worker de generación
│   ├── TechEval.Application/    # Lógica de negocio y DTOs
│   ├── TechEval.Domain/         # Entidades, enums e interfaces
│   ├── TechEval.Infrastructure/ # EF Core, email, JWT, cliente Ollama
│   └── TechEval.Web/            # Frontend Blazor WASM (admin + portal del alumno)
├── tests/
│   └── TechEval.Tests/          # Tests unitarios (xUnit)
├── scripts/
│   ├── create_database.sql                  # Esquema completo, alternativa a migraciones
│   ├── add_user_link_columns.sql            # Incremental: cuentas de alumno
│   ├── add_category_ai_generation_flag.sql  # Incremental: flag de IA por categoría
│   ├── reset_exam_history.sql               # Limpieza del histórico de intentos
│   └── seed_questions_*.sql                 # Banco de preguntas inicial
├── openspec/                    # Especificación viva del sistema e histórico de cambios
├── docker-compose.yml
├── Dockerfile.api · Dockerfile.web · nginx.conf
└── documentacion.md             # Documentación técnica completa
```

---

## Rutas del frontend

| Ruta | Rol | Descripción |
|------|-----|-------------|
| `/login` | — | Acceso de administradores y alumnos (email o usuario) |
| `/admin` | Admin | Dashboard con indicadores generales |
| `/admin/questions` | Admin | Banco de preguntas con filtros |
| `/admin/questions/new` · `/admin/questions/{id}` | Admin | Alta y edición de preguntas |
| `/admin/questions/generate` | Admin | Generación de preguntas con IA |
| `/admin/questions/review` | Admin | Bandeja de revisión de preguntas generadas |
| `/admin/pruebas` | Admin | Listado de pruebas y envío a candidatos |
| `/admin/pruebas/new` · `/admin/pruebas/generate` | Admin | Creación manual y generación automática |
| `/admin/results` · `/admin/results/{id}` | Admin | Resultados globales y detalle |
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
| `POST` | `/api/question-generation/jobs` | Admin |
| `GET` | `/api/question-generation/pending-items` · `/api/question-generation/jobs/progress` | Admin |
| `POST` | `/api/question-generation/items/{id}/approve` · `/reject` | Admin |
| `GET` | `/api/student/pending` · `/api/student/completed` | Alumno |
| `GET` | `/api/exam/validate/{token}` | Público — devuelve el JWT de alumno |
| `POST` | `/api/exam/start/{token}` · `/api/exam/answer/{sessionId}` · `/api/exam/submit` | Público — token del enlace |

---

## Flujo de generación con IA

```
Admin  →  POST /api/question-generation/jobs   (categoría, dificultad, tipo, tema, cantidad)
             │  Job = Queued  ·  N ítems = Pending        → respuesta inmediata (202)
             ▼
   QuestionGenerationWorker   (secuencial, un job cada vez)
             │  Job = Running
             │  por cada ítem: prompt → Ollama → parseo y validación
             │      ok  → Question (PendingReview)  ·  ítem = Succeeded
             │      ko  → ítem = Failed + mensaje de error
             ▼
   Job = Completed
             │
             ▼
Admin  →  /admin/questions/review     aprobar · editar · rechazar
             │
             ▼
   Question.QuestionReviewStatus = Approved   → elegible para pruebas
```

Estados implicados: `QuestionGenerationJobStatus` (`Queued` · `Running` · `Completed` · `Failed`), `QuestionGenerationJobItemStatus` (`Pending` · `Succeeded` · `Failed`) y `QuestionReviewStatus` (`PendingReview` · `Approved` · `Rejected`). Las preguntas creadas manualmente nacen ya como `Approved`.

---

## Especificaciones (OpenSpec)

El directorio [`openspec/`](openspec/) mantiene la especificación viva del sistema, dividida en 12 capacidades:

`admin-console` · `ai-question-generation` · `authentication` · `candidate-experience` · `deployment-ops` · `exam-delivery` · `exam-management` · `exam-results` · `exam-taking` · `project-architecture` · `question-bank` · `student-portal`

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
