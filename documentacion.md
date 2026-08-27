# TechEval Platform — Documentación técnica

## Índice
1. [Visión general](#1-visión-general)
2. [Stack tecnológico y versiones](#2-stack-tecnológico-y-versiones)
3. [Arquitectura](#3-arquitectura)
4. [Diseño de base de datos](#4-diseño-de-base-de-datos)
5. [Estructura del proyecto](#5-estructura-del-proyecto)
6. [API Reference](#6-api-reference)
7. [Flujo completo del sistema](#7-flujo-completo-del-sistema)
8. [Generación de preguntas con IA](#8-generación-de-preguntas-con-ia)
9. [Cuentas de usuario y portal del alumno](#9-cuentas-de-usuario-y-portal-del-alumno)
10. [Configuración y puesta en marcha](#10-configuración-y-puesta-en-marcha)
11. [Scripts SQL](#11-scripts-sql)
12. [Script SQL de preguntas del examen](#12-script-sql-de-preguntas-del-examen)
13. [Docker](#13-docker)
14. [Tests](#14-tests)
15. [Seguridad](#15-seguridad)
16. [Especificaciones (OpenSpec)](#16-especificaciones-openspec)
17. [Decisiones técnicas](#17-decisiones-técnicas)


---

## 1. Visión general

TechEval es una plataforma de evaluación técnica que permite:

- **Gestionar un banco de preguntas** con categorías, niveles de dificultad y tipos (test / respuesta abierta).
- **Generar preguntas con IA** a partir de un tema en texto libre, usando un modelo local (Ollama), en segundo plano y con revisión humana obligatoria antes de que la pregunta sea utilizable.
- **Generar exámenes** manualmente o de forma automática y aleatoria.
- **Enviar exámenes por email** con un enlace de un solo uso y tiempo de expiración configurable, de forma individual o masiva.
- **Realizar exámenes** con temporizador, auto-guardado y UI responsive.
- **Consultar resultados** con corrección automática (tipo test) y dashboard con estadísticas.
- **Ofrecer un portal al alumno** donde consulta sus pruebas pendientes y su historial de notas, con cuenta creada automáticamente al abrir su primera invitación.

### Roles del sistema

| Rol | Claim JWT | Alcance |
|-----|-----------|---------|
| `Admin` | `Role = "Admin"` | Consola completa: categorías, preguntas, generación con IA, pruebas, envíos y resultados de todos los candidatos |
| `Alumno` | `Role = "Alumno"` | Portal propio: sus pruebas pendientes y sus resultados. No accede a nada de otro alumno |

---

## 2. Stack tecnológico y versiones

| Componente | Tecnología | Versión |
|------------|-----------|---------|
| Runtime | .NET | **9.0** |
| Backend | ASP.NET Core | 9.0.0 |
| Frontend | Blazor WebAssembly | 9.0.0 |
| ORM | Entity Framework Core | 9.0.0 |
| Base de datos | SQL Server | 2022 |
| Autenticación | JWT Bearer (roles `Admin` / `Alumno`) | 9.0.0 |
| Documentación API | Swashbuckle (Swagger) | 7.2.0 |
| Logging | Serilog (consola + fichero diario) | 9.0.0 |
| Email | SMTP (`System.Net.Mail`) | — |
| Trabajos en segundo plano | `BackgroundService` + `System.Threading.Channels` | — |
| IA (generación de preguntas) | Ollama local — `qwen2.5-coder:14b` por defecto | — |
| Contenedores | Docker / Docker Compose | — |

---

## 3. Arquitectura

Se aplica **Clean Architecture** con separación estricta de responsabilidades en 4 capas:

```
┌─────────────────────────────────────────────────────┐
│              TechEval.Web (Blazor WASM)             │  ← Capa de presentación
│   Consola de administración + portal del alumno     │
└─────────────────────────────────────────────────────┘
                      │ HTTP / REST
┌─────────────────────────────────────────────────────┐
│                  TechEval.API                       │  ← Capa de entrada
│  Controllers · Middleware · Swagger · JWT           │
│  BackgroundServices (QuestionGenerationWorker)      │
└─────────────────────────────────────────────────────┘
                      │
┌─────────────────────────────────────────────────────┐
│               TechEval.Application                  │  ← Lógica de negocio
│         Services · DTOs · Validaciones              │
└─────────────────────────────────────────────────────┘
                      │
┌─────────────────────────────────────────────────────┐
│                 TechEval.Domain                     │  ← Núcleo del dominio
│           Entities · Interfaces · Enums             │
└─────────────────────────────────────────────────────┘
                      │
┌─────────────────────────────────────────────────────┐
│              TechEval.Infrastructure                │  ← Persistencia y servicios externos
│  EF Core · Repositorios · SMTP · JWT · Ollama       │
└─────────────────────────────────────────────────────┘
                      │
                 SQL Server
```

**Principios aplicados:**
- **Dependency Inversion**: La Application solo conoce interfaces del Domain.
- **Repository Pattern**: Abstracción de EF Core detrás de interfaces.
- **Single Responsibility**: Cada servicio gestiona un único agregado.
- **Open/Closed**: Nuevos tipos de pregunta, proveedores de email o proveedores de IA se añaden sin modificar código existente.

### Procesamiento asíncrono de la generación con IA

La generación con IA no bloquea la petición HTTP. Se apoya en tres piezas:

| Pieza | Ubicación | Responsabilidad |
|-------|-----------|-----------------|
| `IBackgroundTaskQueue` / `BackgroundTaskQueue` | Application (contrato) / Infrastructure (impl.) | Cola en memoria (singleton sobre `Channel<int>`) con los ids de job pendientes |
| `QuestionGenerationWorker` | API (`BackgroundServices/`) | `BackgroundService` que consume la cola, procesa un job cada vez y crea un scope de DI por job |
| `IQuestionGenerationAiService` / `OllamaQuestionGenerationService` | Domain (contrato) / Infrastructure (impl.) | Construcción del prompt, llamada HTTP a Ollama, parseo y validación de la respuesta |

Cambiar de proveedor de IA (Ollama → API remota, otro runtime local, etc.) implica una nueva implementación de `IQuestionGenerationAiService` registrada en DI, sin tocar Application ni Domain.

---

## 4. Diseño de base de datos

### Diagrama de tablas

```
Users
├── Id (PK)
├── Email (UNIQUE)
├── Username (UNIQUE filtrado WHERE Username IS NOT NULL)
├── Name
├── PasswordHash (SHA-256)
├── IsAdmin          -- true = Admin, false = Alumno
└── IsActive

Categories
├── Id (PK)
├── Name (UNIQUE)
├── Description
├── IsActive
└── AllowsAiGeneration   -- DEFAULT 1; en 0 la categoría queda fuera del generador por IA

Questions
├── Id (PK)
├── Text
├── Type                  -- 1=MultipleChoice, 2=OpenEnded
├── Difficulty            -- 1=Basic, 2=Intermediate, 3=Advanced
├── CategoryId (FK → Categories)
├── Points
├── SampleAnswer          -- NULL para tipo test
├── IsActive
└── QuestionReviewStatus  -- 1=Approved (DEFAULT), 2=PendingReview, 3=Rejected  [INDEX]

Answers
├── Id (PK)
├── QuestionId (FK → Questions, CASCADE)
├── Text
├── IsCorrect
└── Order

Exams
├── Id (PK)
├── Title
├── Description
├── TimeLimitMinutes
├── PassingScorePercentage
├── IsActive
├── CreatedByUserId (FK → Users)
└── CreatedAt

ExamQuestions
├── Id (PK)
├── ExamId (FK → Exams, CASCADE)
├── QuestionId (FK → Questions, RESTRICT)
└── Order
     [UNIQUE (ExamId, QuestionId)]

ExamTokens
├── Id (PK)
├── Token (UNIQUE, 64 chars URL-safe)
├── ExamId (FK → Exams)
├── CandidateName
├── CandidateEmail
├── UserId (FK → Users, SET NULL)   -- alumno propietario de la invitación  [INDEX]
├── CreatedAt
├── ExpiresAt
├── IsUsed
└── UsedAt

ExamSessions
├── Id (PK)
├── ExamTokenId (FK → ExamTokens, 1:1)
├── StartedAt
├── CompletedAt
└── Status       -- 1=InProgress, 2=Completed, 3=Expired, 4=Abandoned

UserAnswers
├── Id (PK)
├── ExamSessionId (FK → ExamSessions, CASCADE)
├── QuestionId (FK → Questions)
├── SelectedAnswerId (FK → Answers, NULL para abiertas)
├── OpenAnswer (NULL para tipo test)
├── IsCorrect (NULL para abiertas hasta corrección manual)
└── AnsweredAt

ExamResults
├── Id (PK)
├── ExamSessionId (FK → ExamSessions, 1:1)
├── ExamId (FK → Exams)
├── CandidateName
├── CandidateEmail  [INDEX]
├── UserId (FK → Users, SET NULL)   -- alumno propietario del resultado  [INDEX]
├── TotalPoints
├── ObtainedPoints
├── ScorePercentage (DECIMAL 5,2)
├── Passed
└── CompletedAt    [INDEX]

QuestionGenerationJobs
├── Id (PK)
├── CategoryId (FK → Categories, RESTRICT)
├── Difficulty
├── Type
├── Topic (500)
├── RequestedCount
├── Status          -- 1=Queued, 2=Running, 3=Completed, 4=Failed  [INDEX]
├── CreatedByUserId (FK → Users, RESTRICT)
├── CreatedAt
└── CompletedAt

QuestionGenerationJobItems
├── Id (PK)
├── JobId (FK → QuestionGenerationJobs, CASCADE)  [INDEX]
├── Status          -- 1=Pending, 2=Succeeded, 3=Failed  [INDEX]
├── QuestionId (FK → Questions, SET NULL)  -- la pregunta generada, si tuvo éxito
└── ErrorMessage (2000)                    -- motivo del fallo, si lo hubo
```

### Relaciones clave

| Relación | Tipo | Notas |
|----------|------|-------|
| Exam → ExamQuestions | 1:N | Un examen contiene varias preguntas |
| ExamToken → ExamSession | 1:1 | Cada token genera máx. 1 sesión |
| ExamSession → UserAnswers | 1:N | Respuestas parciales (auto-guardado) |
| ExamSession → ExamResult | 1:1 | Se crea al finalizar el examen |
| User → ExamTokens | 1:N | Invitaciones del alumno; `SET NULL` si se borra el usuario |
| User → ExamResults | 1:N | Historial de notas del alumno; `SET NULL` si se borra el usuario |
| QuestionGenerationJob → Items | 1:N | Un ítem por pregunta solicitada; `CASCADE` |
| QuestionGenerationJobItem → Question | 0..1 | La pregunta creada por ese ítem; `SET NULL` |

> **Selección de preguntas para pruebas**: `QuestionRepository` filtra siempre por `IsActive = true` **y** `QuestionReviewStatus = Approved`. Una pregunta generada por IA no entra en ninguna prueba mientras esté en `PendingReview`, y nunca si se rechaza.

---

## 5. Estructura del proyecto

```
TechEval/
├── TechEval.sln
├── docker-compose.yml
├── Dockerfile.api
├── Dockerfile.web
├── nginx.conf
├── README.md
├── documentacion.md
├── openspec/                                   -- Especificación viva del sistema
│   ├── config.yaml
│   ├── specs/                                  -- 12 capacidades especificadas
│   └── changes/archive/                        -- Histórico de cambios aplicados
│
├── src/
│   ├── TechEval.Domain/
│   │   ├── Common/
│   │   │   └── AuditableEntity.cs              -- Base con CreatedAt/UpdatedAt
│   │   ├── Entities/                           -- Entidades de dominio puras
│   │   │   ├── User.cs
│   │   │   ├── Category.cs
│   │   │   ├── Question.cs
│   │   │   ├── Answer.cs
│   │   │   ├── Exam.cs
│   │   │   ├── ExamQuestion.cs
│   │   │   ├── ExamToken.cs
│   │   │   ├── ExamSession.cs
│   │   │   ├── UserAnswer.cs
│   │   │   ├── ExamResult.cs
│   │   │   ├── QuestionGenerationJob.cs
│   │   │   └── QuestionGenerationJobItem.cs
│   │   ├── Enums/
│   │   │   ├── DifficultyLevel.cs
│   │   │   ├── QuestionType.cs
│   │   │   ├── SessionStatus.cs
│   │   │   ├── QuestionReviewStatus.cs
│   │   │   ├── QuestionGenerationJobStatus.cs
│   │   │   └── QuestionGenerationJobItemStatus.cs
│   │   └── Interfaces/
│   │       ├── Repositories/                   -- Contratos de acceso a datos
│   │       │   ├── IRepository.cs              -- Genérico CRUD
│   │       │   ├── IQuestionRepository.cs
│   │       │   ├── IExamRepository.cs
│   │       │   ├── IExamTokenRepository.cs
│   │       │   ├── IExamResultRepository.cs
│   │       │   └── IQuestionGenerationJobRepository.cs
│   │       └── Services/
│   │           ├── IEmailService.cs
│   │           ├── ITokenService.cs
│   │           └── IQuestionGenerationAiService.cs
│   │
│   ├── TechEval.Application/
│   │   ├── DTOs/                               -- Objetos de transferencia (records)
│   │   │   ├── CategoryDto.cs
│   │   │   ├── QuestionDto.cs
│   │   │   ├── ExamDto.cs
│   │   │   ├── ExamSessionDto.cs
│   │   │   ├── ResultDto.cs
│   │   │   ├── QuestionGenerationDto.cs
│   │   │   └── StudentPortalDto.cs
│   │   └── Services/                           -- Lógica de negocio
│   │       ├── CategoryService.cs
│   │       ├── QuestionService.cs
│   │       ├── ExamService.cs
│   │       ├── ExamTokenService.cs             -- Ciclo de examen + cuentas de alumno
│   │       ├── ResultService.cs
│   │       ├── QuestionGenerationService.cs    -- Jobs, revisión, aprobación/rechazo
│   │       ├── StudentPortalService.cs         -- Pendientes y realizadas del alumno
│   │       ├── IBackgroundTaskQueue.cs         -- Contrato de la cola de jobs
│   │       └── PasswordHasher.cs               -- Hash SHA-256 compartido
│   │
│   ├── TechEval.Infrastructure/
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── DbSeeder.cs                     -- Datos iniciales (admin + categorías)
│   │   │   └── Configurations/
│   │   │       └── QuestionConfiguration.cs    -- Todas las Fluent API configs
│   │   ├── Repositories/
│   │   │   ├── BaseRepository.cs               -- Implementación genérica
│   │   │   ├── QuestionRepository.cs
│   │   │   ├── ExamRepository.cs
│   │   │   ├── ExamTokenRepository.cs
│   │   │   ├── ExamResultRepository.cs
│   │   │   └── QuestionGenerationJobRepository.cs
│   │   ├── Ai/
│   │   │   ├── OllamaQuestionGenerationService.cs  -- Prompt, llamada y validación
│   │   │   └── OllamaSettings.cs                   -- Modelo y parámetros de muestreo
│   │   ├── BackgroundJobs/
│   │   │   └── BackgroundTaskQueue.cs          -- Channel<int> en memoria
│   │   ├── Email/
│   │   │   └── SmtpEmailService.cs             -- SMTP con HTML templates
│   │   ├── Security/
│   │   │   └── TokenService.cs                 -- JWT + secure random tokens
│   │   └── DependencyInjection.cs              -- Registro de servicios
│   │
│   ├── TechEval.API/
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs               -- POST /api/auth/login (admin y alumno)
│   │   │   ├── CategoriesController.cs
│   │   │   ├── QuestionsController.cs
│   │   │   ├── ExamsController.cs              -- Incluye /generate, /send y /send-bulk
│   │   │   ├── ExamSessionController.cs        -- Público: validate/start/answer/submit
│   │   │   ├── ResultsController.cs            -- Dashboard y detalle
│   │   │   ├── QuestionGenerationController.cs -- Jobs de IA y bandeja de revisión
│   │   │   └── StudentPortalController.cs      -- Portal del alumno
│   │   ├── BackgroundServices/
│   │   │   └── QuestionGenerationWorker.cs     -- Procesa los jobs de generación
│   │   ├── Middleware/
│   │   │   └── ErrorHandlingMiddleware.cs      -- Manejo global de errores
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   │
│   └── TechEval.Web/                           -- Blazor WebAssembly
│       ├── Pages/
│       │   ├── Login.razor                     -- Acceso de admins y alumnos
│       │   ├── Admin/
│       │   │   ├── Dashboard.razor
│       │   │   ├── Questions/
│       │   │   │   ├── QuestionList.razor
│       │   │   │   ├── QuestionForm.razor
│       │   │   │   ├── GenerateQuestions.razor -- Solicitud de generación con IA
│       │   │   │   └── QuestionReview.razor    -- Bandeja de revisión con progreso
│       │   │   ├── Exams/
│       │   │   │   ├── ExamList.razor          -- Con modal de envío integrado
│       │   │   │   ├── ExamNew.razor
│       │   │   │   ├── ExamDetail.razor
│       │   │   │   └── GenerateExam.razor
│       │   │   └── Results/
│       │   │       ├── ResultList.razor
│       │   │       ├── ResultDetail.razor
│       │   │       └── ResultsByExam.razor
│       │   ├── Student/
│       │   │   └── Portal.razor                -- Pendientes y realizadas del alumno
│       │   └── Exam/
│       │       ├── ExamLinkRedirect.razor      -- /exam/{token} → /prueba/{token}
│       │       └── TakeExam.razor              -- UI completa del candidato
│       ├── Layout/
│       │   └── MainLayout.razor                -- Sidebar admin / barra del alumno
│       ├── Services/
│       │   ├── ApiService.cs                   -- Wrapper tipado del HttpClient
│       │   └── AuthStateService.cs             -- Auth con localStorage
│       └── Program.cs
│
├── scripts/
│   ├── create_database.sql                     -- Esquema completo (12 tablas)
│   ├── add_user_link_columns.sql               -- Incremental: cuentas de alumno
│   ├── add_category_ai_generation_flag.sql     -- Incremental: flag AllowsAiGeneration
│   ├── reset_exam_history.sql                  -- Limpieza del histórico de intentos
│   ├── seed_questions_examen.sql
│   └── seed_questions_extra.sql
│
└── tests/
    └── TechEval.Tests/
        └── Services/
            ├── QuestionServiceTests.cs
            └── ExamServiceTests.cs
```

---

## 6. API Reference

Swagger publica la referencia interactiva en `/swagger` (solo en entorno de desarrollo). Todos los endpoints marcados como `Admin` o `Alumno` exigen `Authorization: Bearer <jwt>` con el rol correspondiente: sin token devuelven `401`, con token de otro rol devuelven `403`.

### Autenticación

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| POST | `/api/auth/login` | No | Login de admin o alumno; acepta email **o** username |

**Body de `/api/auth/login`:**
```json
{ "email": "admin@techeval.com", "password": "Admin@123!" }
```

**Respuesta (`AuthResultDto`):**
```json
{ "token": "eyJhbGciOi…", "name": "Administrador", "email": "admin@techeval.com", "isAdmin": true }
```

### Categorías

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/categories` | Admin | Listar categorías (incluye `questionCount` y `allowsAiGeneration`) |
| GET | `/api/categories/{id}` | Admin | Detalle |
| POST | `/api/categories` | Admin | Crear categoría |
| PUT | `/api/categories/{id}` | Admin | Actualizar |
| DELETE | `/api/categories/{id}` | Admin | Desactivar (soft delete) |

### Preguntas

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/questions?categoryId=&difficulty=&type=` | Admin | Listar con filtros |
| GET | `/api/questions/{id}` | Admin | Detalle con respuestas |
| POST | `/api/questions` | Admin | Crear pregunta (nace `Approved`) |
| PUT | `/api/questions/{id}` | Admin | Actualizar (no altera el estado de revisión) |
| DELETE | `/api/questions/{id}` | Admin | Desactivar |

### Exámenes

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/exams` | Admin | Listar con estadísticas |
| GET | `/api/exams/{id}` | Admin | Detalle con preguntas |
| POST | `/api/exams` | Admin | Crear manual |
| POST | `/api/exams/generate` | Admin | Generar automático |
| PUT | `/api/exams/{id}` | Admin | Actualizar |
| DELETE | `/api/exams/{id}` | Admin | Desactivar |
| POST | `/api/exams/send` | Admin | Enviar por email a un candidato |
| POST | `/api/exams/send-bulk` | Admin | Enviar por email a varios candidatos |

**Body de `/api/exams/generate`:**
```json
{
  "title": "Evaluación SQL Junio 2024",
  "description": "...",
  "timeLimitMinutes": 60,
  "passingScorePercentage": 70,
  "questionCount": 10,
  "categoryIds": [1, 3],        // opcional
  "difficulty": "Intermediate"  // opcional
}
```

**Body de `/api/exams/send`:**
```json
{
  "examId": 3,
  "candidateName": "Juan García",
  "candidateEmail": "juan@empresa.com",
  "expirationHours": 72
}
```

**Body de `/api/exams/send-bulk`:**
```json
{
  "examId": 3,
  "candidates": [
    { "name": "Juan García", "email": "juan@empresa.com" },
    { "name": "Ana López",  "email": "ana@empresa.com" }
  ],
  "expirationHours": 72
}
```

**Respuesta (`BulkSendResultDto`):** recuento de `sent` / `failed` y el detalle por candidato con el error concreto de cada fallo.

### Generación de preguntas con IA

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| POST | `/api/question-generation/jobs` | Admin | Encola un lote de generación → `202 Accepted` |
| GET | `/api/question-generation/pending-items` | Admin | Bandeja de revisión (éxitos pendientes y fallos) |
| GET | `/api/question-generation/jobs/progress` | Admin | Progreso agregado de los lotes con ítems pendientes de atención |
| POST | `/api/question-generation/items/{id}/approve` | Admin | Aprueba la pregunta generada → `Approved` |
| POST | `/api/question-generation/items/{id}/reject` | Admin | Rechaza la pregunta generada → `Rejected` |

**Body de `/api/question-generation/jobs`:**
```json
{
  "categoryId": 1,
  "difficulty": "Intermediate",
  "type": "MultipleChoice",
  "topic": "Índices y planes de ejecución en SQL Server",
  "count": 5
}
```

Validaciones: `topic` no vacío, `categoryId` de una categoría existente **con `AllowsAiGeneration = true`**, y `count` entre 1 y 20.

### Sesión de examen (pública, con el token del enlace)

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/exam/validate/{token}` | No | Valida el token, aprovisiona/reutiliza la cuenta del alumno y devuelve un JWT de auto-login |
| POST | `/api/exam/start/{token}` | No | Inicia la sesión y marca el token como usado |
| POST | `/api/exam/answer/{sessionId}` | No | Auto-guarda una respuesta |
| POST | `/api/exam/submit` | No | Envía el examen completo |

**Respuesta de `/api/exam/validate/{token}` (`ExamTokenValidationDto`):**
```json
{
  "isValid": true,
  "error": null,
  "sessionId": null,
  "examTitle": "Evaluación SQL Junio 2024",
  "candidateName": "Juan García",
  "authToken": "eyJhbGciOi…"   // JWT con rol Alumno
}
```

### Portal del alumno

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/student/pending` | Alumno | Sus invitaciones no usadas y no expiradas |
| GET | `/api/student/completed` | Alumno | Su historial de resultados (nota, aprobado, fecha) |

El `UserId` se toma del claim `NameIdentifier` del JWT, nunca de un parámetro de la petición: un alumno no puede consultar los datos de otro.

### Resultados

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/results` | Admin | Historial completo |
| GET | `/api/results/exam/{examId}` | Admin | Por examen |
| GET | `/api/results/{id}` | Admin | Detalle con revisión de respuestas |
| GET | `/api/results/dashboard` | Admin | Estadísticas del dashboard |

---

## 7. Flujo completo del sistema

### Flujo del administrador

```
Admin crea preguntas → Categorías + Dificultad + Tipo + Respuestas
    │                  (o las genera con IA, ver sección 8)
    ↓
Admin crea examen (manual o automático)
    │  La selección automática solo usa preguntas IsActive + Approved
    ↓
Admin envía examen: POST /api/exams/send  ·  POST /api/exams/send-bulk
    ├── Se genera token seguro (48 bytes → 64 chars URL-safe, único en BD)
    ├── Se guarda ExamToken con fecha de expiración
    └── Se envía email HTML al candidato con enlace único
                          ↓
              Candidato recibe email
```

### Flujo del candidato

```
Candidato recibe email con enlace: https://app.com/exam/{token}
    ↓
GET /api/exam/validate/{token}
    ├── Valida: token existe, no expirado, no usado
    ├── Crea (o reutiliza) el User del alumno a partir de CandidateEmail
    ├── Vincula ExamToken.UserId a ese usuario
    └── Devuelve: isValid, examTitle, candidateName y authToken (JWT rol Alumno)
    ↓
Blazor guarda la sesión y muestra la pantalla de bienvenida con info del examen
    ↓
Candidato pulsa "Comenzar"
    ↓
POST /api/exam/start/{token}
    ├── Marca IsUsed = true, UsedAt = now (no puede repetirse)
    └── Crea ExamSession con status=InProgress
    ↓
Candidato responde preguntas con temporizador visible
    ├── Auto-guardado: POST /api/exam/answer/{sessionId} (cada respuesta)
    └── Si el tiempo se agota → auto-envío
    ↓
Candidato pulsa "Finalizar" → POST /api/exam/submit
    ├── Corrige automáticamente preguntas tipo test
    ├── Calcula puntuación y % de éxito
    ├── Crea ExamResult (con UserId del alumno)
    ├── Actualiza ExamSession.Status = Completed
    └── Envía email con resultado al candidato
    ↓
Admin consulta resultados en el dashboard
Alumno consulta su nota en /portal
```

### Flujo del alumno recurrente

```
Alumno entra en /login con su email (o username) y contraseña
    ↓
POST /api/auth/login → JWT con rol Alumno → redirección a /portal
    ↓
Portal del alumno
    ├── GET /api/student/pending    → pruebas pendientes con su fecha de expiración
    │       └── "Comenzar" → /prueba/{token} (mismo flujo que desde el email)
    └── GET /api/student/completed  → historial de notas (%, aprobado/suspenso, fecha)
```

---

## 8. Generación de preguntas con IA

### Visión del flujo

```
Admin  →  POST /api/question-generation/jobs   (categoría, dificultad, tipo, tema, cantidad)
             │  Job = Queued  ·  N ítems = Pending        → respuesta inmediata (202)
             │  El job se encola en IBackgroundTaskQueue
             ▼
   QuestionGenerationWorker   (secuencial, un job cada vez)
             │  Job = Running
             │  por cada ítem: prompt → Ollama → parseo y validación
             │      ok  → Question (PendingReview)  ·  ítem = Succeeded
             │      ko  → ítem = Failed + mensaje de error
             ▼
   Job = Completed   (aunque algún ítem haya fallado)
             │
             ▼
Admin  →  /admin/questions/review     aprobar · editar · rechazar
             │
             ▼
   Question.QuestionReviewStatus = Approved   → elegible para pruebas
```

### Estados

| Enum | Valores | Significado |
|------|---------|-------------|
| `QuestionGenerationJobStatus` | `Queued` · `Running` · `Completed` · `Failed` | Ciclo de vida del lote |
| `QuestionGenerationJobItemStatus` | `Pending` · `Succeeded` · `Failed` | Ciclo de vida de cada pregunta del lote |
| `QuestionReviewStatus` | `Approved` · `PendingReview` · `Rejected` | Estado de revisión de la pregunta resultante |

Las preguntas creadas manualmente nacen ya como `Approved` (valor por defecto en la Fluent API y en el esquema SQL), de modo que el flujo manual no se ve afectado por la revisión.

### Garantías del procesamiento

| Garantía | Implementación |
|----------|----------------|
| **La API no se bloquea** | El endpoint solo persiste el job y lo encola; responde `202 Accepted` |
| **Un job cada vez** | El worker consume la cola secuencialmente, evitando saturar un modelo local CPU-bound |
| **Un fallo no arrastra al lote** | Cada ítem se valida por separado; el ítem falla con su mensaje y el resto continúa |
| **Nada llega a producción sin revisar** | Las preguntas nacen en `PendingReview` y el repositorio de selección filtra por `Approved` |
| **Sin jobs colgados** | Al arrancar, `RecoverInterruptedJobsAsync` marca como `Failed` los jobs que quedaron en `Running` y sus ítems `Pending` |
| **Categorías protegidas** | `AllowsAiGeneration = false` excluye la categoría del selector y hace que la API rechace el job aunque se llame directamente |

### Validación de la respuesta del modelo

Un ítem se marca `Failed` (sin crear ninguna `Question`) cuando:

- La respuesta no se puede interpretar como JSON válido.
- El número de respuestas no es el exigido por el tipo de pregunta (4 opciones para tipo test).
- No hay exactamente una respuesta marcada como correcta.

El parser tolera que el modelo emita un bloque de razonamiento antes del JSON: se extrae el contenido estructurado posterior a ese bloque, y también funciona con modelos que no razonan explícitamente.

### Bandeja de revisión

`/admin/questions/review` muestra en una única pantalla:

- Los ítems **exitosos** pendientes de revisar, con el contenido completo de la pregunta generada.
- Los ítems **fallidos**, con su mensaje de error.
- El **tema y el id del job** de origen junto a cada ítem, para distinguir lotes.
- Un **indicador de progreso** de los lotes en curso (listas / fallidas / pendientes sobre el total solicitado) que se refresca cada 5 s mediante un `PeriodicTimer`, sin recargar la página.

Desde ahí se puede **aprobar**, **rechazar** o **editar antes de aprobar** (la edición usa el mismo formulario que una pregunta manual y no altera el estado de revisión).

### Parámetros del modelo

Configurables bajo la sección `Ollama` de `appsettings.json`:

| Parámetro | Por defecto | Efecto |
|-----------|-------------|--------|
| `BaseUrl` | `http://ollama:11434` (Docker) · `http://localhost:11434` (dev) | Endpoint del servidor Ollama |
| `Model` | `qwen2.5-coder:14b` | Modelo usado para generar |
| `Temperature` | `0.6` | Creatividad del muestreo |
| `RepeatPenalty` | `1.3` | Penaliza la repetición: evita enunciados clonados dentro de un mismo lote |
| `NumCtx` | `8192` | Ventana de contexto |
| `NumPredict` | `2048` | Tokens máximos de la respuesta |

El `HttpClient` del servicio usa un timeout de **5 minutos** por llamada, porque el modelo corre sobre CPU.

---

## 9. Cuentas de usuario y portal del alumno

### Aprovisionamiento automático

No hay alta manual de alumnos. La cuenta se crea sola la primera vez que se abre una invitación:

```
GET /api/exam/validate/{token}
    ├── ¿Existe un User con ese CandidateEmail?
    │      Sí → se reutiliza (no se modifica ni el Name ni el PasswordHash)
    │      No → se crea:
    │             Email        = CandidateEmail
    │             Username     = parte local del email (antes de la @)
    │             Name         = CandidateName de la invitación
    │             PasswordHash = SHA-256(username)
    │             IsAdmin      = false
    │             IsActive     = true
    ├── ExamToken.UserId ← Id del usuario
    └── Respuesta con authToken: JWT firmado con rol Alumno (auto-login)
```

Ejemplo: `alejandro.robles@pronet-ise.com` → usuario `alejandro.robles`, contraseña inicial `alejandro.robles`.

### Sesión y navegación

| Situación | Comportamiento |
|-----------|----------------|
| Login de admin | `POST /api/auth/login` → JWT rol `Admin` → redirección a `/admin`, sidebar completo |
| Login de alumno | `POST /api/auth/login` (email o username) → JWT rol `Alumno` → redirección a `/portal`, barra superior simple |
| Apertura del enlace de invitación | `authToken` devuelto por `validate` → sesión iniciada sin pedir credenciales |
| Sesión previa en `localStorage` | `AuthStateService.InitializeAsync` la restaura y redirige según el rol |

### Contenido del portal (`/portal`)

- **Pruebas pendientes** — invitaciones con `IsUsed = false` y no expiradas, con su fecha de expiración y un botón "Comenzar" que lleva a `/prueba/{token}`, el mismo flujo que desde el email.
- **Pruebas realizadas** — `ExamResult` del alumno ordenados de más reciente a más antiguo, con título, porcentaje obtenido y aprobado/suspenso.

Ambos listados se resuelven exclusivamente por el `UserId` del JWT.

### Migración de datos previos

Los `ExamToken` y `ExamResult` creados antes de este modelo tienen `UserId = NULL` y, por tanto, no aparecen en ningún portal. Para partir de un estado limpio en un entorno de pruebas existe [`scripts/reset_exam_history.sql`](scripts/reset_exam_history.sql), que borra el histórico de intentos (tokens, sesiones, respuestas y resultados) **sin tocar** usuarios, categorías, preguntas ni exámenes.

---

## 10. Configuración y puesta en marcha

### Prerrequisitos

- .NET 9 SDK
- SQL Server (local o Docker)
- Cuenta SMTP (SendGrid, Gmail, etc.) — o MailHog para desarrollo
- Ollama (opcional, solo para la generación de preguntas con IA)

### Referencia de configuración

Las claves se leen de `appsettings.json`, se sobrescriben por entorno (`appsettings.Development.json`) y, en local, por `appsettings.Local.json` (opcional y fuera de git). En Docker se sobrescriben con variables de entorno usando doble guion bajo: `Ollama__Model`, `Jwt__SecretKey`, etc.

| Clave | Descripción | Por defecto |
|-------|-------------|-------------|
| `ConnectionStrings:DefaultConnection` | Cadena de conexión a SQL Server | `Server=localhost;Database=TechEvalDb;…` |
| `Jwt:SecretKey` | Clave de firma HMAC-SHA256 | `TechEval_SuperSecretKey_…` |
| `Jwt:Issuer` / `Jwt:Audience` | Emisor y audiencia validados en cada petición | `TechEvalAPI` / `TechEvalClient` |
| `Jwt:ExpirationHours` | Vigencia del token | `8` |
| `Email:Host` · `Port` · `UserName` · `Password` · `FromEmail` · `FromName` · `EnableSsl` | Configuración SMTP | vacío |
| `FrontendBaseUrl` | Base con la que se construyen los enlaces de invitación | `https://localhost:60805` |
| `AllowedOrigins` | Orígenes CORS permitidos en producción (separados por coma) | `http://localhost:5001` |
| `AdminPassword` | Contraseña del admin creado en el primer arranque | `Admin@123!` |
| `Ollama:BaseUrl` · `Model` · `Temperature` · `RepeatPenalty` · `NumCtx` · `NumPredict` | Configuración del modelo de IA (ver sección 8) | ver sección 8 |
| `Serilog:MinimumLevel` | Nivel de log por defecto y overrides | `Information` |

En desarrollo, CORS permite cualquier origen; en producción se restringe a los valores de `AllowedOrigins`.

### Pasos de instalación

#### 1. Clonar / copiar el proyecto

```bash
cd TechEval
```

#### 2. Configurar la base de datos

Editar `src/TechEval.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=TechEvalDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

#### 3. Crear y aplicar migraciones

```bash
cd src/TechEval.Infrastructure

# Crear migración inicial
dotnet ef migrations add InitialCreate --startup-project ../TechEval.API

# Aplicar a la BD (también se hace automáticamente al arrancar la API)
dotnet ef database update --startup-project ../TechEval.API
```

Sobre una base de datos ya creada con una versión anterior, aplica los scripts incrementales de la [sección 11](#11-scripts-sql).

#### 4. Configurar email

**Para desarrollo (MailHog):**
```bash
docker run -d -p 1025:1025 -p 8025:8025 mailhog/mailhog
```

En `appsettings.Development.json`:
```json
{
  "Email": {
    "Host": "localhost",
    "Port": 1025,
    "EnableSsl": false
  }
}
```
UI de MailHog: http://localhost:8025

**Para producción (SendGrid):**
```json
{
  "Email": {
    "Host": "smtp.sendgrid.net",
    "Port": 587,
    "UserName": "apikey",
    "Password": "SG.xxxx",
    "FromEmail": "noreply@tudominio.com",
    "EnableSsl": true
  }
}
```

#### 5. Levantar el modelo de IA (opcional)

```bash
ollama serve
ollama pull qwen2.5-coder:14b
```

`appsettings.Development.json` apunta a `http://localhost:11434`. Si no hay servidor Ollama disponible, el resto de la plataforma funciona con normalidad: solo fallan los jobs de generación, cuyos ítems quedan en `Failed` con el mensaje "No se pudo contactar al modelo de IA".

#### 6. Arrancar la API

```bash
cd src/TechEval.API
dotnet run
```

- API: http://localhost:5000
- Swagger: http://localhost:5000/swagger

**Admin por defecto:** `admin@techeval.com` / `Admin@123!`
(configurado en `appsettings.json` → `AdminPassword`)

#### 7. Arrancar el frontend Blazor

```bash
cd src/TechEval.Web
dotnet run
```

- Web: http://localhost:5001
- Asegúrate de que `wwwroot/appsettings.json` apunta a la API:

```json
{ "ApiBaseUrl": "http://localhost:5000/" }
```

---

## 11. Scripts SQL

### 11.1 Creación completa: `create_database.sql`

El fichero [`scripts/create_database.sql`](scripts/create_database.sql) es una alternativa a las migraciones de EF Core.
Úsalo cuando:
- Necesites crear la BD en un servidor sin acceso al CLI de .NET (producción gestionada, DBA externo, etc.).
- Quieras revisar o auditar el esquema completo antes de desplegarlo.
- Trabajes con un CI/CD que ejecute scripts SQL directamente.

> **No uses ambas opciones a la vez.** Elige EF Migrations (sección 10, paso 3) o el script SQL, nunca los dos sobre la misma BD.

#### Ejecución con SSMS

Abre SSMS, conecta al servidor y ejecuta el fichero:

```
Archivo → Abrir → scripts/create_database.sql → F5
```

#### Ejecución con sqlcmd

```bash
sqlcmd -S localhost -E -i scripts/create_database.sql
```

Con autenticación SQL:

```bash
sqlcmd -S localhost -U sa -P "<contraseña>" -i scripts/create_database.sql
```

#### Ejecución con Docker (SQL Server en contenedor)

```bash
docker exec -i techeval-sqlserver \
  /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -i /scripts/create_database.sql
```

#### Contenido del script

El script realiza, en orden:

| Paso | Descripción |
|------|-------------|
| 1 | Crea la base de datos `TechEvalDb` si no existe |
| 2 | Elimina las tablas si ya existían (orden inverso de FK) |
| 3 | Crea las 12 tablas con restricciones, FK e índices |
| 4 | Inserta el usuario administrador por defecto |
| 5 | Inserta 5 categorías iniciales |
| 6 | Inserta 3 preguntas de ejemplo con sus respuestas |

#### Tablas creadas y dependencias

```
Users ──────────────────────────────────────── (sin dependencias)
Categories ─────────────────────────────────── (sin dependencias)
Questions ──────────── FK → Categories
Answers ────────────── FK → Questions                 (CASCADE delete)
Exams ──────────────── FK → Users
ExamQuestions ──────── FK → Exams (CASCADE), Questions
ExamTokens ─────────── FK → Exams, Users (SET NULL)
ExamSessions ───────── FK → ExamTokens                (CASCADE delete, UNIQUE)
UserAnswers ────────── FK → ExamSessions (CASCADE), Questions, Answers
ExamResults ────────── FK → ExamSessions (CASCADE, UNIQUE), Exams, Users (SET NULL)
QuestionGenerationJobs ────── FK → Categories, Users
QuestionGenerationJobItems ── FK → QuestionGenerationJobs (CASCADE), Questions (SET NULL)
```

#### Contraseña de administrador

El script inserta el usuario admin con contraseña `Admin@123!` hasheada en SHA-256.  
Para usar una contraseña diferente, genera el hash con PowerShell antes de ejecutar el script:

```powershell
$p = "TuNuevaContraseña"
[BitConverter]::ToString(
    [System.Security.Cryptography.SHA256]::Create().ComputeHash(
        [Text.Encoding]::UTF8.GetBytes($p)
    )
).Replace("-","").ToLower()
```

Sustituye el valor del `INSERT INTO dbo.Users` en el script con el hash obtenido,  
o actualiza la BD tras crearla:

```sql
USE TechEvalDb;
UPDATE dbo.Users
SET PasswordHash = '<hash_nuevo>'
WHERE Email = 'admin@techeval.com';
```

### 11.2 Scripts incrementales

Para bases de datos **ya existentes** creadas con una versión anterior. Son aditivos e idempotentes (comprueban antes de crear) y no borran datos:

| Script | Qué hace |
|--------|----------|
| [`add_user_link_columns.sql`](scripts/add_user_link_columns.sql) | Añade `Users.Username` con índice único filtrado, `ExamTokens.UserId` y `ExamResults.UserId` con sus FK (`ON DELETE SET NULL`) e índices |
| [`add_category_ai_generation_flag.sql`](scripts/add_category_ai_generation_flag.sql) | Añade `Categories.AllowsAiGeneration` con default `1` y marca la categoría `iECS` como no apta para generación por IA |

```bash
sqlcmd -S localhost -d TechEvalDb -i scripts/add_user_link_columns.sql
sqlcmd -S localhost -d TechEvalDb -i scripts/add_category_ai_generation_flag.sql
```

### 11.3 Limpieza de histórico

[`reset_exam_history.sql`](scripts/reset_exam_history.sql) borra `ExamTokens` y, por cascada, `ExamSessions`, `UserAnswers` y `ExamResults`. Pensado para descartar los intentos creados bajo el modelo anterior (sin `User` asociado). **No toca** `Users`, `Categories`, `Questions`, `Answers` ni `Exams`.

```bash
sqlcmd -S localhost -d TechEvalDb -i scripts/reset_exam_history.sql
```

---

## 12. Script SQL de preguntas del examen

### Archivo: `scripts/seed_questions_examen.sql`

Inserta las **89 preguntas** del examen de competencias técnicas (Examen competencias v3.pdf) directamente en `TechEvalDb`.

#### Resumen de contenido

| Sección | Preguntas | Categorías usadas |
|---------|-----------|-------------------|
| Backend | 37 test + 2 abiertas | C#, Arquitectura, APIs REST |
| Frontend | 6 test | Frontend *(nueva)* |
| Bases de datos | 37 test + 1 abierta | SQL |
| iECS | 1 abierta + 3 test + 2 abiertas | iECS *(nueva)* |

Existe además `scripts/seed_questions_extra.sql` con un banco de preguntas adicional.

#### Cómo ejecutar

```sql
-- En SQL Server Management Studio o Azure Data Studio:
USE TechEvalDb;
GO
-- ejecutar el contenido de scripts/seed_questions_examen.sql
```

O desde la línea de comandos:

```bash
sqlcmd -S localhost -E -i scripts/seed_questions_examen.sql
```

#### Categorías nuevas que crea el script

El script añade automáticamente (si no existen):

- **Frontend** — JavaScript, frameworks SPA (React/Angular/Vue), UX/UI, Blazor WASM
- **iECS** — Plataforma iECS de Grupo Pronet: listados, ventanas, tareas programadas

> `iECS` queda marcada con `AllowsAiGeneration = 0` por el script incremental: al tratarse de contenido propietario, no se envía a un modelo generativo. Sigue disponible con normalidad para la creación manual de preguntas.

#### Notas sobre las preguntas abiertas

Las preguntas de tipo `OpenEnded` (`Type = 2`) no tienen respuestas en la tabla `Answers`; se evalúan manualmente. El campo `SampleAnswer` de la tabla `Questions` contiene la respuesta modelo usada como guía de corrección.

---

## 13. Docker

### Desarrollo rápido con Docker Compose

```bash
# Arrancar SQL Server + API + Web + Ollama + MailHog
docker-compose --profile dev up -d

# Solo producción (sin MailHog)
docker-compose up -d
```

### Servicios Docker

| Servicio | Puerto | Descripción |
|----------|--------|-------------|
| `sqlserver` | 1433 | SQL Server 2022 Developer |
| `api` | 5000 | ASP.NET Core API |
| `web` | 5001 | Blazor WebAssembly (Nginx) |
| `ollama` | 11434 | Modelo de IA local para la generación de preguntas |
| `mailhog` | 8025 | UI de email (solo perfil dev) |

Volúmenes persistentes: `sqlserver_data` (datos de SQL Server) y `ollama_data` (modelos descargados).

### Descargar el modelo de IA

El contenedor `ollama` arranca vacío para no alargar el primer `docker-compose up`. Hay que descargar el modelo una sola vez:

```bash
docker exec ollama ollama pull qwen2.5-coder:14b
```

Corre sobre CPU (no requiere GPU) y queda guardado en `ollama_data`. El modelo se fija con `Ollama__Model` en `docker-compose.yml`, que tiene prioridad sobre `appsettings.json`: si lo cambias, descarga el nuevo modelo.

### Configurar API key de SendGrid en Docker

```bash
SENDGRID_API_KEY=SG.xxx docker-compose up -d
```

---

## 14. Tests

### Ejecutar los tests

```bash
cd tests/TechEval.Tests
dotnet test
```

### Cobertura actual

| Test | Descripción |
|------|-------------|
| `QuestionService.CreateAsync_ValidMultipleChoice_ReturnsDto` | Creación correcta de pregunta tipo test |
| `QuestionService.CreateAsync_MultipleChoiceWithoutCorrectAnswer_ThrowsException` | Validación de respuesta correcta obligatoria |
| `QuestionService.CreateAsync_MultipleChoiceWithWrongCount_ThrowsException` | Validación de exactamente 4 opciones |
| `QuestionService.GetByIdAsync_ExistingQuestion_ReturnsDto` | Recuperación y mapeo de DTO |
| `QuestionService.GetByIdAsync_NonExisting_ReturnsNull` | No 404 expuesto |
| `QuestionService.DeleteAsync_ExistingQuestion_SetsInactive` | Soft delete correcto |
| `ExamService.GenerateAsync_NotEnoughQuestions_ThrowsException` | Validación de preguntas disponibles |
| `ExamService.CreateAsync_AllQuestionsExist_CreatesExam` | Creación manual de examen |

### Áreas sin cobertura automatizada

Pendiente de cubrir: `QuestionGenerationService` (validación del job y transiciones de estado), `StudentPortalService` (aislamiento entre alumnos) y el parseo de respuestas del modelo en `OllamaQuestionGenerationService`. Estas tres son buenas candidatas para tests con `InMemory` y un doble de `IQuestionGenerationAiService`.

### Añadir más tests

```csharp
// Ejemplo con InMemory DB para tests de integración
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("TestDb")
    .Options;
var context = new AppDbContext(options);
```

---

## 15. Seguridad

### Tokens de examen

- Generados con `RandomNumberGenerator.GetBytes(48)` → 64 chars Base64 URL-safe
- **Un solo uso**: al iniciar la sesión `IsUsed = true`
- **Expiración configurable** (72h por defecto)
- Almacenados en BD con índice único → imposible colisión

### Autenticación y roles

- JWT con HS256, firmado con secreto de 44+ chars
- Expiración: 8 horas (configurable con `Jwt:ExpirationHours`); `ClockSkew = 0`
- Claims emitidos: `NameIdentifier` (id de usuario), `Email`, `Role` (`Admin` o `Alumno`) e `isAdmin`
- Rol `Admin` requerido en categorías, preguntas, exámenes, resultados y generación con IA
- Rol `Alumno` requerido en el portal del alumno; un token de admin recibe `403` en esos endpoints
- El login filtra por `IsActive`: una cuenta desactivada recibe `401` aunque las credenciales sean correctas
- El mensaje de error de login es genérico ("Credenciales incorrectas.") tanto si el email no existe como si la contraseña falla

### Cuentas de alumno aprovisionadas automáticamente

La contraseña inicial de un alumno es **la parte local de su email**, un valor predecible a partir de la propia dirección. Es aceptable para un portal de consulta de notas, pero conviene tenerlo presente:

```
✓ Forzar cambio de contraseña en el primer acceso al portal
✓ O emitir una contraseña aleatoria y enviarla en el email de invitación
```

El aislamiento entre alumnos sí está garantizado: los endpoints del portal resuelven el `UserId` desde el claim del JWT y nunca desde un parámetro de la petición.

### Protección de la API de exámenes públicos

- No requiere JWT previo (el candidato llega desde el email)
- Protección por token de un solo uso, validado en cada operación
- `validate` devuelve un JWT de alumno, de modo que el resto de la sesión queda asociada a un usuario real
- Rate limiting recomendado en producción (añadir `AspNetCoreRateLimit`)

### Generación con IA

- El modelo corre **en local** (Ollama): ni los enunciados ni el contexto propietario salen de la red de la organización
- Las categorías con contenido sensible se excluyen con `AllowsAiGeneration = false`
- Ninguna pregunta generada llega a un candidato sin aprobación humana explícita
- Solo un `Admin` puede encolar jobs, y el número de preguntas por lote está acotado a 20

### CORS

- Configurado explícitamente para el origen del frontend en producción (`AllowedOrigins`)
- No se usa `AllowAnyOrigin` fuera de desarrollo

### SQL Injection

- EF Core con parámetros tipados → inmune por diseño

### Recomendaciones adicionales para producción

```
✓ Cambiar Jwt:SecretKey por un valor de 32+ chars aleatorios
✓ Migrar el hash de contraseñas de SHA-256 a BCrypt o Argon2
✓ Usar HTTPS (certificado SSL/TLS)
✓ Configurar rate limiting en /api/exam/*
✓ Añadir reCAPTCHA al formulario de examen si es necesario
✓ Rotar la contraseña de SQL Server
✓ Usar Azure Key Vault o similar para secretos
```

---

## 16. Especificaciones (OpenSpec)

El directorio [`openspec/`](openspec/) mantiene la especificación viva del sistema con un flujo *spec-driven*: cada cambio funcional se propone, se implementa y se archiva fusionando sus deltas en las specs principales.

### Capacidades especificadas (`openspec/specs/`)

| Capacidad | Cubre |
|-----------|-------|
| `project-architecture` | Estructura de la solución y reglas de dependencia entre capas |
| `authentication` | Login, roles, expiración de JWT, hash de contraseñas, aprovisionamiento de cuentas |
| `question-bank` | Banco de preguntas, categorías, dificultades y tipos |
| `ai-question-generation` | Jobs de generación, revisión, aprobación y calidad del modelo |
| `exam-management` | Creación manual y generación automática de pruebas |
| `exam-delivery` | Invitaciones, tokens de un solo uso y envío por email |
| `exam-taking` | Resolución de la prueba, temporizador y auto-guardado |
| `exam-results` | Corrección, cálculo de nota y consulta de resultados |
| `candidate-experience` | Experiencia del candidato de principio a fin |
| `student-portal` | Pruebas pendientes y realizadas del alumno autenticado |
| `admin-console` | Pantallas y comportamiento de la consola de administración |
| `deployment-ops` | Docker, configuración y puesta en marcha |

### Histórico de cambios (`openspec/changes/archive/`)

| Fecha | Cambio |
|-------|--------|
| 2026-08-13 | 10 capacidades iniciales (`scaffolding-clean-architecture`, `authentication`, `question-bank`, `exam-management`, `exam-delivery`, `exam-taking`, `exam-results`, `candidate-experience`, `admin-console`, `deployment-ops`) |
| 2026-08-14 | `ai-question-generation` — generación de preguntas por IA con revisión obligatoria |
| 2026-08-14 | `rename-exam-to-prueba-terminology` — unificación de terminología en la interfaz |
| 2026-08-17 | `question-review-progress-and-timestamps` — progreso en vivo de la bandeja de revisión |
| 2026-08-17 | `student-user-accounts` — cuentas de alumno, auto-login y portal |
| 2026-08-18 | `ai-question-quality` — parámetros de muestreo y calidad de las preguntas generadas |
| 2026-08-24 | `ai-generation-model-and-scope` — cambio de modelo y flag `AllowsAiGeneration` por categoría |

Cada carpeta archivada conserva su `proposal.md`, `design.md` (cuando aplica), los deltas de spec y el `tasks.md` con el desglose de implementación.

---

## 17. Decisiones técnicas

### ¿Por qué Clean Architecture en lugar de solo capas?

Clean Architecture invierte las dependencias: Infrastructure depende de Domain, no al revés. Esto permite cambiar EF Core por Dapper, SQL Server por PostgreSQL u Ollama por otro proveedor de IA tocando solo Infrastructure, sin tocar Application ni Domain. En un proyecto de evaluación técnica que puede crecer, esta flexibilidad tiene valor real.

### ¿Por qué Blazor WebAssembly en lugar de Blazor Server?

Blazor WASM se ejecuta en el cliente → sin estado en servidor → escala trivialmente. El examen del candidato funciona aunque la conexión sea inestable (las respuestas se guardan localmente hasta el envío). Blazor Server requeriría SignalR y conexión persistente, lo que es un riesgo para candidatos con mala conexión.

### ¿Por qué un modelo de IA local y no una API en la nube?

Los enunciados incluyen contexto técnico y, en algunas categorías, conocimiento propietario. Con Ollama el contenido nunca sale de la red de la organización, no hay coste por token y no depende de la disponibilidad de un tercero. El precio es el rendimiento: al correr sobre CPU cada pregunta tarda, y por eso la generación es asíncrona con un timeout de 5 minutos por llamada.

### ¿Por qué una cola en memoria y no Hangfire o un broker?

El volumen es bajo (lotes de hasta 20 preguntas, un admin a la vez) y el modelo local es el cuello de botella real: paralelizar no aportaría nada. Un `Channel<int>` con un `BackgroundService` cubre el caso sin añadir infraestructura ni dependencias. La contrapartida —perder la cola al reiniciar— se resuelve con `RecoverInterruptedJobsAsync`, que marca los jobs interrumpidos como fallidos en lugar de dejarlos colgados. Si el volumen creciera, `IBackgroundTaskQueue` es el único punto que habría que reimplementar.

### ¿Por qué revisión humana obligatoria de las preguntas generadas?

Un modelo puede producir enunciados ambiguos, con más de una respuesta válida o con errores factuales sutiles. Enviar eso a un candidato tiene un coste reputacional alto y falsea la evaluación. Por eso las preguntas generadas nacen en `PendingReview` y el repositorio de selección filtra por `Approved`: el estado por defecto del sistema es "no usar", y hace falta un acto explícito de un admin para cambiarlo.

### ¿Por qué crear la cuenta del alumno al abrir la invitación y no antes?

Evita un alta manual y un email adicional: el candidato hace su prueba exactamente igual que antes, y como efecto colateral queda con una cuenta que le permite volver a consultar su nota. Vincular `ExamToken` y `ExamResult` a un `UserId` también da al historial una identidad estable, en lugar de depender de la coincidencia de cadenas de email.

### ¿Por qué SHA-256 para contraseñas y no BCrypt?

En un MVP está bien, pero **para producción se debe migrar a BCrypt o Argon2**. El seeder crea el hash; para cambiarlo basta actualizar el hash en BD. Con la llegada de las cuentas de alumno esta deuda pesa más, porque hay muchos más usuarios y su contraseña inicial es predecible.

### ¿Por qué soft delete y no hard delete?

Las preguntas y exámenes tienen historial de resultados. Si se eliminaran físicamente, los resultados huérfanos perderían contexto. El soft delete (IsActive = false) preserva el historial completo. Por el mismo motivo, una pregunta rechazada se marca `Rejected` en lugar de borrarse: se conserva la trazabilidad de qué generó el modelo y qué se descartó.

### ¿Por qué records para DTOs?

Los `record` de C# son inmutables por defecto, tienen igualdad por valor y sintaxis de deconstrucción. Son ideales para DTOs que viajan entre capas y no deben mutar. En caso de DTOs con muchas propiedades opcionales, se puede cambiar a clases normales.

### ¿Por qué no AutoMapper?

AutoMapper añade magia implícita difícil de depurar. Los mapeos manuales en los servicios son explícitos, fáciles de testear y no introducen dependencia adicional. Para proyectos muy grandes con decenas de entidades, sí tiene sentido considerarlo.

### ¿Por qué SMTP directo y no SendGrid SDK?

El `IEmailService` desacopla la implementación. La `SmtpEmailService` funciona con cualquier servidor SMTP (SendGrid, Gmail, Mailtrap, MailHog). Para cambiar a SendGrid SDK basta crear `SendGridEmailService : IEmailService` y registrarlo en DI.

---

## Changelog

| Versión | Fecha | Cambios |
|---------|-------|---------|
| 1.0.0 | 2024-06 | Versión inicial completa |
| 1.1.0 | 2026-08-14 | Generación de preguntas con IA (Ollama), procesamiento en segundo plano y bandeja de revisión |
| 1.2.0 | 2026-08-17 | Progreso en vivo de la revisión; cuentas de alumno, auto-login desde la invitación y portal del alumno |
| 1.3.0 | 2026-08-18 | Parámetros de calidad del modelo (temperatura, penalización de repetición, contexto) |
| 1.4.0 | 2026-08-27 | Modelo de generación `qwen2.5-coder:14b` y flag `AllowsAiGeneration` por categoría |
