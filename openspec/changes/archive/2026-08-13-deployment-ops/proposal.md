## Why
Para que TechEval pueda desplegarse de forma reproducible en cualquier entorno (portátil de un desarrollador, servidor de staging, o un DBA externo sin el CLI de .NET), se necesita empaquetar la API, el frontend Blazor WASM y sus dependencias (SQL Server, servidor SMTP de pruebas) en contenedores versionados, junto con una vía alternativa a `dotnet ef database update` para crear el esquema de base de datos y poblarlo con las preguntas reales del examen de competencias, sin depender de que exista un entorno .NET instalado en el servidor de destino.

## What Changes
- `docker-compose.yml` orquesta cuatro servicios: `sqlserver` (SQL Server 2022 Developer con healthcheck y volumen persistente), `api` (espera a que `sqlserver` esté healthy), `web` (espera a `api`) y `mailhog` (bajo el perfil `dev`, para capturar correos en desarrollo local sin usar SendGrid).
- `Dockerfile.api`: build multi-stage con `dotnet/sdk:9.0` para restaurar y publicar la API, y runtime final sobre `dotnet/aspnet:9.0` que solo contiene los binarios publicados (sin el SDK).
- `Dockerfile.web`: build multi-stage con `dotnet/sdk:9.0` para publicar el proyecto Blazor WASM, y runtime final sobre `nginx:alpine` que sirve únicamente los estáticos de `wwwroot`.
- `nginx.conf`: fallback de rutas de SPA (`try_files ... /index.html`) para que el enrutamiento de Blazor funcione en cualquier URL profunda, cache de un año para assets estáticos (`js`, `css`, `wasm`, imágenes) y compresión `gzip`.
- `scripts/create_database.sql`: alternativa a las migraciones de EF Core para crear el esquema completo (10 tablas) en un servidor sin CLI de .NET, respetando el orden de dependencias por clave foránea tanto al eliminar tablas existentes (orden inverso) como al crearlas, e insertando el usuario administrador por defecto junto con categorías y preguntas de ejemplo.
- `scripts/seed_questions_examen.sql`: puebla la base de datos con las 89 preguntas reales del examen de competencias (Backend, Frontend, Bases de datos, iECS), creando únicamente las categorías nuevas que aún no existan y siendo compatible tanto con un esquema creado por este mismo script SQL como con uno generado por EF Core (`EnsureCreatedAsync`).

## Capabilities
### New Capabilities
- `deployment-ops`: Infraestructura de despliegue de TechEval — orquestación con Docker Compose de SQL Server, API, frontend Blazor WASM y MailHog (perfil de desarrollo); imágenes Docker multi-stage para API y Web; configuración de Nginx para servir la SPA; y scripts SQL como alternativa a las migraciones de EF Core para crear el esquema completo y poblarlo con las preguntas reales del examen de competencias.

### Modified Capabilities
(ninguna)

## Impact
- **Código afectado**: `docker-compose.yml`, `Dockerfile.api`, `Dockerfile.web`, `nginx.conf`, `scripts/create_database.sql`, `scripts/seed_questions_examen.sql`.
- **Dependencias funcionales**: empaqueta y despliega el sistema completo — depende de todas las demás capacidades ya construidas (`authentication`, `question-bank`, `exam-management`, `exam-delivery`, `exam-taking`, `exam-results`, `admin-console`, `candidate-experience`) sin añadir lógica de negocio propia.
- **Infraestructura**: imagen `mcr.microsoft.com/mssql/server:2022-latest` para la base de datos, `mcr.microsoft.com/dotnet/aspnet:9.0` para la API, `nginx:alpine` para el frontend, `mailhog/mailhog` como servidor SMTP de pruebas en desarrollo.
- **Consumidores posteriores**: equipo de operaciones/DevOps y administradores de sistemas que despliegan TechEval en entornos donde no se dispone del SDK de .NET o de acceso directo a `dotnet ef`.
