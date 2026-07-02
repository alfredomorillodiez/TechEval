# TechEval

Plataforma de evaluación técnica para gestionar bancos de preguntas, generar exámenes y evaluar candidatos mediante un enlace de un solo uso enviado por email.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-WebAssembly-512BD4?logo=blazor)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)

---

## Características

- **Banco de preguntas** — tipo test y respuesta abierta, con categorías y niveles de dificultad
- **Generación de exámenes** — manual o automática con preguntas aleatorias por categoría y dificultad
- **Envío por email** — enlace único por candidato con expiración configurable; envío masivo a múltiples candidatos desde CSV o lista manual
- **Examen del candidato** — temporizador, auto-guardado de respuestas y envío automático al agotar el tiempo
- **Corrección automática** — para preguntas tipo test; preguntas abiertas pendientes de revisión manual
- **Dashboard de resultados** — historial, estadísticas y detalle por candidato

---

## Stack tecnológico

| Capa | Tecnología | Versión |
|------|-----------|---------|
| Runtime | .NET | 9.0 |
| Backend | ASP.NET Core API | 9.0.0 |
| Frontend | Blazor WebAssembly | 9.0.0 |
| ORM | Entity Framework Core | 9.0.0 |
| Base de datos | SQL Server | 2022 |
| Autenticación | JWT Bearer | 9.0.0 |
| Documentación API | Swagger (Swashbuckle) | 7.2.0 |
| Logging | Serilog | 9.0.0 |
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

**Credenciales por defecto:** `admin@techeval.com` / `Admin@123!`

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

## Arquitectura

Clean Architecture en 4 capas con separación estricta de dependencias:

```
TechEval.Web (Blazor WASM)
        │  HTTP / REST
TechEval.API  (Controllers · JWT · Swagger)
        │
TechEval.Application  (Services · DTOs · Validaciones)
        │
TechEval.Domain  (Entities · Interfaces · Enums)
        │
TechEval.Infrastructure  (EF Core · Repositorios · SMTP · JWT)
        │
    SQL Server
```

---

## Estructura del proyecto

```
TechEval/
├── src/
│   ├── TechEval.API/           # Controladores y middleware
│   ├── TechEval.Application/   # Lógica de negocio y DTOs
│   ├── TechEval.Domain/        # Entidades e interfaces
│   ├── TechEval.Infrastructure/# EF Core, email, JWT
│   └── TechEval.Web/           # Frontend Blazor WASM
├── tests/
│   └── TechEval.Tests/         # Tests unitarios
├── scripts/
│   ├── create_database.sql     # Esquema alternativo a migraciones
│   └── seed_questions_*.sql    # Banco de preguntas inicial
├── docker-compose.yml
└── documentacion.md            # Documentación técnica completa
```

---

## Tests

```bash
cd tests/TechEval.Tests
dotnet test
```

---

## Documentación

La documentación técnica completa (diseño de BD, API reference, flujos, decisiones técnicas) está en [`documentacion.md`](documentacion.md).
