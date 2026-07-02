# TechEval Platform — Documentación técnica

## Índice
1. [Visión general](#1-visión-general)
2. [Stack tecnológico y versiones](#2-stack-tecnológico-y-versiones)
3. [Arquitectura](#3-arquitectura)
4. [Diseño de base de datos](#4-diseño-de-base-de-datos)
5. [Estructura del proyecto](#5-estructura-del-proyecto)
6. [API Reference](#6-api-reference)
7. [Flujo completo del sistema](#7-flujo-completo-del-sistema)
8. [Configuración y puesta en marcha](#8-configuración-y-puesta-en-marcha)
9. [Script SQL de creación de base de datos](#9-script-sql-de-creación-de-base-de-datos)
10. [Script SQL de preguntas del examen](#10-script-sql-de-preguntas-del-examen)
11. [Docker](#11-docker)
12. [Tests](#12-tests)
13. [Seguridad](#13-seguridad)
14. [Decisiones técnicas](#14-decisiones-técnicas)


---

## 1. Visión general

TechEval es una plataforma de evaluación técnica que permite:

- **Gestionar un banco de preguntas** con categorías, niveles de dificultad y tipos (test / respuesta abierta).
- **Generar exámenes** manualmente o de forma automática y aleatoria.
- **Enviar exámenes por email** con un enlace de un solo uso y tiempo de expiración configurable.
- **Realizar exámenes** con temporizador, auto-guardado y UI responsive.
- **Consultar resultados** con corrección automática (tipo test) y dashboard con estadísticas.

---

## 2. Stack tecnológico y versiones

| Componente | Tecnología | Versión |
|------------|-----------|---------|
| Runtime | .NET | **9.0** |
| Backend | ASP.NET Core | 9.0.0 |
| Frontend | Blazor WebAssembly | 9.0.0 |
| ORM | Entity Framework Core | 9.0.0 |
| Base de datos | SQL Server | 2022 |
| Autenticación | JWT Bearer | 9.0.0 |
| Documentación API | Swashbuckle (Swagger) | 7.2.0 |
| Logging | Serilog | 9.0.0 |
| Email | SMTP (`System.Net.Mail`) | — |
| Contenedores | Docker / Docker Compose | — |

---

## 3. Arquitectura

Se aplica **Clean Architecture** con separación estricta de responsabilidades en 4 capas:

```
┌─────────────────────────────────────────────┐
│              TechEval.Web (Blazor WASM)      │  ← Capa de presentación
│         Llama a la API via HttpClient        │
└─────────────────────────────────────────────┘
                      │ HTTP / REST
┌─────────────────────────────────────────────┐
│              TechEval.API                   │  ← Capa de entrada
│    Controllers · Middleware · Swagger · JWT │
└─────────────────────────────────────────────┘
                      │
┌─────────────────────────────────────────────┐
│           TechEval.Application              │  ← Lógica de negocio
│      Services · DTOs · Validaciones         │
└─────────────────────────────────────────────┘
                      │
┌─────────────────────────────────────────────┐
│           TechEval.Domain                   │  ← Núcleo del dominio
│   Entities · Interfaces · Enums             │
└─────────────────────────────────────────────┘
                      │
┌─────────────────────────────────────────────┐
│         TechEval.Infrastructure             │  ← Persistencia y servicios externos
│  EF Core · Repositorios · SMTP · JWT Token  │
└─────────────────────────────────────────────┘
                      │
                 SQL Server
```

**Principios aplicados:**
- **Dependency Inversion**: La Application solo conoce interfaces del Domain.
- **Repository Pattern**: Abstracción de EF Core detrás de interfaces.
- **Single Responsibility**: Cada servicio gestiona un único agregado.
- **Open/Closed**: Nuevos tipos de pregunta o proveedores de email se añaden sin modificar código existente.

---

## 4. Diseño de base de datos

### Diagrama de tablas

```
Users
├── Id (PK)
├── Email (UNIQUE)
├── Name
├── PasswordHash (SHA-256)
├── IsAdmin
└── IsActive

Categories
├── Id (PK)
├── Name (UNIQUE)
└── Description

Questions
├── Id (PK)
├── Text
├── Type             -- 1=MultipleChoice, 2=OpenEnded
├── Difficulty       -- 1=Basic, 2=Intermediate, 3=Advanced
├── CategoryId (FK → Categories)
├── Points
├── SampleAnswer     -- NULL para tipo test
└── IsActive

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
├── TotalPoints
├── ObtainedPoints
├── ScorePercentage (DECIMAL 5,2)
├── Passed
└── CompletedAt    [INDEX]
```

### Relaciones clave

| Relación | Tipo | Notas |
|----------|------|-------|
| Exam → ExamQuestions | 1:N | Un examen contiene varias preguntas |
| ExamToken → ExamSession | 1:1 | Cada token genera máx. 1 sesión |
| ExamSession → UserAnswers | 1:N | Respuestas parciales (auto-guardado) |
| ExamSession → ExamResult | 1:1 | Se crea al finalizar el examen |

---

## 5. Estructura del proyecto

```
TechEval/
├── TechEval.sln
├── docker-compose.yml
├── Dockerfile.api
├── Dockerfile.web
├── nginx.conf
├── documentacion.md
│
├── src/
│   ├── TechEval.Domain/
│   │   ├── Common/
│   │   │   └── AuditableEntity.cs          -- Base con CreatedAt/UpdatedAt
│   │   ├── Entities/                       -- Entidades de dominio puras
│   │   │   ├── User.cs
│   │   │   ├── Category.cs
│   │   │   ├── Question.cs
│   │   │   ├── Answer.cs
│   │   │   ├── Exam.cs
│   │   │   ├── ExamQuestion.cs
│   │   │   ├── ExamToken.cs
│   │   │   ├── ExamSession.cs
│   │   │   ├── UserAnswer.cs
│   │   │   └── ExamResult.cs
│   │   ├── Enums/
│   │   │   ├── DifficultyLevel.cs
│   │   │   ├── QuestionType.cs
│   │   │   └── SessionStatus.cs
│   │   └── Interfaces/
│   │       ├── Repositories/               -- Contratos de acceso a datos
│   │       │   ├── IRepository.cs          -- Genérico CRUD
│   │       │   ├── IQuestionRepository.cs
│   │       │   ├── IExamRepository.cs
│   │       │   ├── IExamTokenRepository.cs
│   │       │   └── IExamResultRepository.cs
│   │       └── Services/
│   │           ├── IEmailService.cs
│   │           └── ITokenService.cs
│   │
│   ├── TechEval.Application/
│   │   ├── DTOs/                           -- Objetos de transferencia (records)
│   │   │   ├── CategoryDto.cs
│   │   │   ├── QuestionDto.cs
│   │   │   ├── ExamDto.cs
│   │   │   ├── ExamSessionDto.cs
│   │   │   └── ResultDto.cs
│   │   └── Services/                       -- Lógica de negocio
│   │       ├── CategoryService.cs
│   │       ├── QuestionService.cs
│   │       ├── ExamService.cs
│   │       ├── ExamTokenService.cs         -- Gestión completa del ciclo de examen
│   │       └── ResultService.cs
│   │
│   ├── TechEval.Infrastructure/
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── DbSeeder.cs                 -- Datos iniciales (admin + categorías)
│   │   │   └── Configurations/
│   │   │       └── QuestionConfiguration.cs -- Todas las Fluent API configs
│   │   ├── Repositories/
│   │   │   ├── BaseRepository.cs           -- Implementación genérica
│   │   │   ├── QuestionRepository.cs
│   │   │   ├── ExamRepository.cs
│   │   │   ├── ExamTokenRepository.cs
│   │   │   └── ExamResultRepository.cs
│   │   ├── Email/
│   │   │   └── SmtpEmailService.cs         -- SMTP con HTML templates
│   │   ├── Security/
│   │   │   └── TokenService.cs             -- JWT + secure random tokens
│   │   └── DependencyInjection.cs          -- Registro de servicios
│   │
│   ├── TechEval.API/
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs           -- POST /api/auth/login
│   │   │   ├── CategoriesController.cs
│   │   │   ├── QuestionsController.cs
│   │   │   ├── ExamsController.cs          -- Incluye /send y /generate
│   │   │   ├── ExamSessionController.cs    -- Público: validate/start/answer/submit
│   │   │   └── ResultsController.cs        -- Dashboard y detalle
│   │   ├── Middleware/
│   │   │   └── ErrorHandlingMiddleware.cs  -- Manejo global de errores
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   └── TechEval.Web/                       -- Blazor WebAssembly
│       ├── Pages/
│       │   ├── Login.razor
│       │   ├── Admin/
│       │   │   ├── Dashboard.razor
│       │   │   ├── Questions/
│       │   │   │   ├── QuestionList.razor
│       │   │   │   └── QuestionForm.razor
│       │   │   ├── Exams/
│       │   │   │   ├── ExamList.razor      -- Con modal de envío integrado
│       │   │   │   └── GenerateExam.razor
│       │   │   └── Results/
│       │   │       └── ResultList.razor
│       │   └── Exam/
│       │       └── TakeExam.razor          -- UI completa del candidato
│       ├── Layout/
│       │   └── MainLayout.razor            -- Sidebar admin
│       ├── Services/
│       │   ├── ApiService.cs               -- Wrapper tipado del HttpClient
│       │   └── AuthStateService.cs         -- Auth con localStorage
│       └── Program.cs
│
└── tests/
    └── TechEval.Tests/
        └── Services/
            ├── QuestionServiceTests.cs
            └── ExamServiceTests.cs
```

---

## 6. API Reference

### Autenticación

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| POST | `/api/auth/login` | No | Login admin, devuelve JWT |

### Categorías

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/categories` | No | Listar categorías |
| POST | `/api/categories` | Admin | Crear categoría |
| PUT | `/api/categories/{id}` | Admin | Actualizar |
| DELETE | `/api/categories/{id}` | Admin | Desactivar (soft delete) |

### Preguntas

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/questions?categoryId=&difficulty=&type=` | Admin | Listar con filtros |
| GET | `/api/questions/{id}` | Admin | Detalle con respuestas |
| POST | `/api/questions` | Admin | Crear pregunta |
| PUT | `/api/questions/{id}` | Admin | Actualizar |
| DELETE | `/api/questions/{id}` | Admin | Desactivar |

### Exámenes

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/exams` | Admin | Listar con estadísticas |
| GET | `/api/exams/{id}` | Admin | Detalle con preguntas |
| POST | `/api/exams` | Admin | Crear manual |
| POST | `/api/exams/generate` | Admin | Generar automático |
| DELETE | `/api/exams/{id}` | Admin | Desactivar |
| POST | `/api/exams/send` | Admin | Enviar por email al candidato |

**Body de `/api/exams/generate`:**
```json
{
  "title": "Evaluación SQL Junio 2024",
  "description": "...",
  "timeLimitMinutes": 60,
  "passingScorePercentage": 70,
  "questionCount": 10,
  "categoryId": 1,       // opcional
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

### Sesión de examen (pública, sin auth)

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/exam/validate/{token}` | No | Verifica si el token es válido |
| POST | `/api/exam/start/{token}` | No | Inicia la sesión y marca el token como usado |
| POST | `/api/exam/answer/{sessionId}` | No | Auto-guarda una respuesta |
| POST | `/api/exam/submit` | No | Envía el examen completo |

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
    ↓
Admin crea examen (manual o automático)
    ↓
Admin envía examen: POST /api/exams/send
    ├── Se genera token seguro (64 bytes, URL-safe, único en BD)
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
    └── Devuelve: isValid, examTitle, candidateName
    ↓
Blazor muestra pantalla de bienvenida con info del examen
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
    ├── Crea ExamResult
    ├── Actualiza ExamSession.Status = Completed
    └── Envía email con resultado al candidato
    ↓
Admin consulta resultados en el dashboard
```

---

## 8. Configuración y puesta en marcha

### Prerrequisitos

- .NET 9 SDK
- SQL Server (local o Docker)
- Cuenta SMTP (SendGrid, Gmail, etc.) — o MailHog para desarrollo

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

#### 5. Arrancar la API

```bash
cd src/TechEval.API
dotnet run
```

- API: http://localhost:5000
- Swagger: http://localhost:5000/swagger

**Admin por defecto:** `admin@techeval.com` / `Admin@123!`
(configurado en `appsettings.json` → `AdminPassword`)

#### 6. Arrancar el frontend Blazor

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

## 9. Script SQL de creación de base de datos

El fichero [`scripts/create_database.sql`](scripts/create_database.sql) es una alternativa a las migraciones de EF Core.
Úsalo cuando:
- Necesites crear la BD en un servidor sin acceso al CLI de .NET (producción gestionada, DBA externo, etc.).
- Quieras revisar o auditar el esquema completo antes de desplegarlo.
- Trabajes con un CI/CD que ejecute scripts SQL directamente.

> **No uses ambas opciones a la vez.** Elige EF Migrations (sección 7, paso 3) o el script SQL, nunca los dos sobre la misma BD.

### Ejecución con SSMS

Abre SSMS, conecta al servidor y ejecuta el fichero:

```
Archivo → Abrir → scripts/create_database.sql → F5
```

### Ejecución con sqlcmd

```bash
sqlcmd -S localhost -E -i scripts/create_database.sql
```

Con autenticación SQL:

```bash
sqlcmd -S localhost -U sa -P "<contraseña>" -i scripts/create_database.sql
```

### Ejecución con Docker (SQL Server en contenedor)

```bash
docker exec -i techeval-sqlserver \
  /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -i /scripts/create_database.sql
```

### Contenido del script

El script realiza, en orden:

| Paso | Descripción |
|------|-------------|
| 1 | Crea la base de datos `TechEvalDb` si no existe |
| 2 | Elimina las tablas si ya existían (orden inverso de FK) |
| 3 | Crea las 10 tablas con restricciones, FK e índices |
| 4 | Inserta el usuario administrador por defecto |
| 5 | Inserta 5 categorías iniciales |
| 6 | Inserta 3 preguntas de ejemplo con sus respuestas |

### Tablas creadas y dependencias

```
Users ──────────────────────────────────────── (sin dependencias)
Categories ─────────────────────────────────── (sin dependencias)
Questions ──────────── FK → Categories
Answers ────────────── FK → Questions         (CASCADE delete)
Exams ──────────────── FK → Users
ExamQuestions ────────── FK → Exams (CASCADE), Questions
ExamTokens ─────────── FK → Exams
ExamSessions ───────── FK → ExamTokens        (CASCADE delete, UNIQUE)
UserAnswers ─────────── FK → ExamSessions (CASCADE), Questions, Answers
ExamResults ─────────── FK → ExamSessions (CASCADE, UNIQUE), Exams
```

### Contraseña de administrador

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

---

## 10. Script SQL de preguntas del examen

### Archivo: `scripts/seed_questions_examen.sql`

Inserta las **89 preguntas** del examen de competencias técnicas (Examen competencias v3.pdf) directamente en `TechEvalDb`.

#### Resumen de contenido

| Sección | Preguntas | Categorías usadas |
|---------|-----------|-------------------|
| Backend | 37 test + 2 abiertas | C#, Arquitectura, APIs REST |
| Frontend | 6 test | Frontend *(nueva)* |
| Bases de datos | 37 test + 1 abierta | SQL |
| iECS | 1 abierta + 3 test + 2 abiertas | iECS *(nueva)* |

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

#### Notas sobre las preguntas abiertas

Las preguntas de tipo `OpenEnded` (`Type = 2`) no tienen respuestas en la tabla `Answers`; se evalúan manualmente. El campo `SampleAnswer` de la tabla `Questions` contiene la respuesta modelo usada como guía de corrección.

---

## 11. Docker


### Desarrollo rápido con Docker Compose

```bash
# Arrancar SQL Server + API + MailHog
docker-compose --profile dev up -d

# Solo producción
docker-compose up -d
```

### Servicios Docker

| Servicio | Puerto | Descripción |
|----------|--------|-------------|
| `sqlserver` | 1433 | SQL Server 2022 Developer |
| `api` | 5000 | ASP.NET Core API |
| `web` | 5001 | Blazor WebAssembly (Nginx) |
| `mailhog` | 8025 | UI de email (solo perfil dev) |

### Configurar API key de SendGrid en Docker

```bash
SENDGRID_API_KEY=SG.xxx docker-compose up -d
```

---

## 12. Tests

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

### Añadir más tests

```csharp
// Ejemplo con InMemory DB para tests de integración
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("TestDb")
    .Options;
var context = new AppDbContext(options);
```

---

## 13. Seguridad

### Tokens de examen

- Generados con `RandomNumberGenerator.GetBytes(48)` → 64 chars Base64 URL-safe
- **Un solo uso**: al iniciar la sesión `IsUsed = true`
- **Expiración configurable** (72h por defecto)
- Almacenados en BD con índice único → imposible colisión

### Autenticación de administradores

- JWT con HS256, firmado con secreto de 44+ chars
- Expiración: 8 horas (configurable)
- Rol `Admin` requerido en todos los endpoints de gestión
- Contraseña hasheada con SHA-256 en BD

### Protección de la API de exámenes públicos

- No requiere JWT (candidatos no autenticados)
- Protección por token de un solo uso
- Validación del estado del token en cada operación
- Rate limiting recomendado en producción (añadir `AspNetCoreRateLimit`)

### CORS

- Configurado explícitamente para el origen del frontend
- No se usa `AllowAnyOrigin` en producción

### SQL Injection

- EF Core con parámetros tipados → inmune por diseño

### Recomendaciones adicionales para producción

```
✓ Cambiar Jwt:SecretKey por un valor de 32+ chars aleatorios
✓ Usar HTTPS (certificado SSL/TLS)
✓ Configurar rate limiting en /api/exam/*
✓ Añadir reCAPTCHA al formulario de examen si es necesario
✓ Rotar la contraseña de SQL Server
✓ Usar Azure Key Vault o similar para secretos
```

---

## 14. Decisiones técnicas

### ¿Por qué Clean Architecture en lugar de solo capas?

Clean Architecture invierte las dependencias: Infrastructure depende de Domain, no al revés. Esto permite cambiar EF Core por Dapper o SQL Server por PostgreSQL tocando solo Infrastructure, sin tocar Application ni Domain. En un proyecto de evaluación técnica que puede crecer, esta flexibilidad tiene valor real.

### ¿Por qué Blazor WebAssembly en lugar de Blazor Server?

Blazor WASM se ejecuta en el cliente → sin estado en servidor → escala trivialmente. El examen del candidato funciona aunque la conexión sea inestable (las respuestas se guardan localmente hasta el envío). Blazor Server requeriría SignalR y conexión persistente, lo que es un riesgo para candidatos con mala conexión.

### ¿Por qué SHA-256 para contraseñas y no BCrypt?

En un MVP está bien, pero **para producción se debe migrar a BCrypt o Argon2**. El seeder crea el hash; para cambiarlo basta actualizar el hash en BD. El AuthController ya verifica de forma constante para evitar timing attacks básicos.

### ¿Por qué soft delete y no hard delete?

Las preguntas y exámenes tienen historial de resultados. Si se eliminaran físicamente, los resultados huérfanos perderían contexto. El soft delete (IsActive = false) preserva el historial completo.

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
