# Tasks: deployment-ops

## 1. Imágenes Docker

- [x] 1.1 Crear `Dockerfile.api` multi-stage: etapa `build` con `dotnet/sdk:9.0` que restaura y publica `TechEval.API.csproj`, etapa `runtime` con `dotnet/aspnet:9.0` que solo copia el resultado publicado
- [x] 1.2 Crear `Dockerfile.web` multi-stage: etapa `build` con `dotnet/sdk:9.0` que publica `TechEval.Web.csproj`, etapa `runtime` con `nginx:alpine` que copia únicamente `wwwroot`

## 2. Orquestación con Docker Compose

- [x] 2.1 Definir servicio `sqlserver` (imagen `mssql/server:2022-latest`, credenciales, volumen persistente `sqlserver_data`, `healthcheck` con `sqlcmd`)
- [x] 2.2 Definir servicio `api` con `depends_on: sqlserver: condition: service_healthy` y variables de entorno (connection string, `Jwt__SecretKey`, configuración de email, `AdminPassword`)
- [x] 2.3 Definir servicio `web` con `depends_on: api` y publicación del puerto `5001:80`
- [x] 2.4 Definir servicio `mailhog` bajo `profiles: [dev]` con puertos `1025` (SMTP) y `8025` (UI)

## 3. Configuración de Nginx

- [x] 3.1 Configurar fallback de rutas SPA (`try_files $uri $uri/ /index.html`) para el enrutamiento de Blazor WASM
- [x] 3.2 Configurar cache de un año para assets estáticos (`js`, `css`, `wasm`, imágenes) y compresión `gzip`

## 4. Script SQL de esquema completo

- [x] 4.1 Crear `scripts/create_database.sql`: creación de `TechEvalDb` si no existe y eliminación idempotente de las 10 tablas en orden inverso de dependencias
- [x] 4.2 Crear las 10 tablas (`Users`, `Categories`, `Questions`, `Answers`, `Exams`, `ExamQuestions`, `ExamTokens`, `ExamSessions`, `UserAnswers`, `ExamResults`) en el orden que respeta sus claves foráneas, con índices y constraints
- [x] 4.3 Insertar el usuario administrador por defecto y datos de ejemplo (categorías iniciales y preguntas de muestra)

## 5. Script de seed de preguntas del examen de competencias

- [x] 5.1 Crear `scripts/seed_questions_examen.sql` con comprobaciones de compatibilidad de esquema (`ALTER TABLE ... ADD DEFAULT` solo si la restricción no existe, para soportar esquemas generados por `EnsureCreatedAsync`)
- [x] 5.2 Insertar las categorías `Frontend` e `iECS` únicamente si no existen ya en `dbo.Categories`
- [x] 5.3 Insertar las 89 preguntas del examen (Backend 39, Frontend 6, Bases de datos 38, iECS 6) con sus respuestas y `SampleAnswer` para las preguntas abiertas

## 6. Documentación de arranque

- [x] 6.1 Documentar los comandos de `docker-compose` para arrancar en modo desarrollo (`--profile dev`) y en modo producción, incluyendo la variable `SENDGRID_API_KEY`
- [x] 6.2 Documentar la ejecución alternativa de los scripts SQL vía `sqlcmd`/SSMS y la advertencia de no combinar migraciones de EF Core con `create_database.sql` sobre la misma base de datos
