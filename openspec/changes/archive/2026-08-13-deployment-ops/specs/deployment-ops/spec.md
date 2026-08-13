# Spec: deployment-ops

## ADDED Requirements

### Requirement: Levantamiento del stack completo con Docker Compose
El sistema SHALL permitir levantar la plataforma completa (base de datos, API y frontend) con un único comando de Docker Compose, garantizando que cada servicio solo arranque cuando sus dependencias estén realmente disponibles.

#### Scenario: Arranque en modo producción
- **GIVEN** el fichero `docker-compose.yml` del repositorio
- **WHEN** se ejecuta `docker-compose up -d` sin especificar perfil
- **THEN** el sistema arranca los servicios `sqlserver`, `api` y `web`
- **AND** el servicio `mailhog` MUST NOT arrancar, al estar declarado bajo el perfil `dev`

#### Scenario: La API espera a que SQL Server esté saludable antes de arrancar
- **GIVEN** el servicio `sqlserver` con un `healthcheck` basado en `sqlcmd -Q 'SELECT 1'`
- **WHEN** se levanta el stack con `docker-compose up`
- **THEN** el servicio `api` SHALL permanecer sin arrancar hasta que la condición `service_healthy` de `sqlserver` se cumpla

### Requirement: Perfil de desarrollo incluye MailHog para captura local de correos
El sistema SHALL ofrecer un perfil `dev` de Docker Compose que añade un servidor SMTP de pruebas, de forma que los correos que envía la API (invitaciones a examen, notificaciones) puedan revisarse sin depender de un proveedor externo como SendGrid.

#### Scenario: Arranque con el perfil dev
- **GIVEN** el fichero `docker-compose.yml` con el servicio `mailhog` bajo `profiles: [dev]`
- **WHEN** se ejecuta `docker-compose --profile dev up -d`
- **THEN** el sistema arranca adicionalmente el servicio `mailhog`, expuesto en el puerto `1025` (SMTP) y `8025` (interfaz web)

### Requirement: Imágenes Docker multi-stage para API y Web
El sistema SHALL construir las imágenes de la API y del frontend en dos etapas (compilación con el SDK completo, ejecución con una imagen de runtime mínima), para que las imágenes finales desplegadas no incluyan herramientas de compilación ni código fuente innecesario.

#### Scenario: Imagen de la API sin el SDK de .NET
- **GIVEN** `Dockerfile.api` con una etapa `build` basada en `mcr.microsoft.com/dotnet/sdk:9.0` que ejecuta `dotnet publish`
- **WHEN** se construye la imagen final
- **THEN** la etapa `runtime`, basada en `mcr.microsoft.com/dotnet/aspnet:9.0`, SHALL contener únicamente los artefactos publicados en `/app/publish`, sin el SDK usado para compilar

#### Scenario: Imagen del frontend basada en Nginx sin el SDK de .NET
- **GIVEN** `Dockerfile.web` con una etapa `build` que ejecuta `dotnet publish` sobre `TechEval.Web.csproj`
- **WHEN** se construye la imagen final
- **THEN** la etapa `runtime`, basada en `nginx:alpine`, SHALL copiar únicamente el contenido de `wwwroot` publicado, sin el SDK de .NET ni los proyectos de dominio/aplicación

### Requirement: Nginx sirve el Blazor WASM con enrutamiento de SPA
El sistema SHALL configurar Nginx para servir los archivos estáticos del frontend Blazor WebAssembly, devolviendo `index.html` para cualquier ruta que no corresponda a un archivo físico, de modo que el enrutamiento del lado del cliente funcione en recargas de página o accesos directos a rutas profundas.

#### Scenario: Acceso directo a una ruta profunda de la SPA
- **GIVEN** la configuración `location / { try_files $uri $uri/ /index.html; }` en `nginx.conf`
- **WHEN** un usuario solicita una URL que no corresponde a ningún archivo físico en `/usr/share/nginx/html` (por ejemplo, una ruta de una página de Blazor)
- **THEN** Nginx SHALL responder con `index.html`, permitiendo que el enrutador de Blazor resuelva la ruta en el cliente

#### Scenario: Cache de assets estáticos versionados
- **GIVEN** la regla `location ~* \.(js|css|png|jpg|gif|ico|wasm|dat)$` en `nginx.conf`
- **WHEN** el navegador solicita un archivo `.wasm`, `.js` o `.css` del build de Blazor
- **THEN** Nginx SHALL responder con cabeceras `Cache-Control: public, immutable` y expiración de un año

### Requirement: Restricción de origen CORS en despliegue de producción
El sistema SHALL restringir los orígenes permitidos por CORS a una lista configurada cuando la API se ejecuta con `ASPNETCORE_ENVIRONMENT=Production`, en lugar de aceptar solicitudes de cualquier origen.

#### Scenario: API en modo producción no usa AllowAnyOrigin
- **GIVEN** el servicio `api` de `docker-compose.yml` configurado con `ASPNETCORE_ENVIRONMENT=Production`
- **WHEN** la API construye la política CORS `BlazorPolicy` al arrancar
- **THEN** el sistema MUST usar el origen configurado en `AllowedOrigins` (por defecto `http://localhost:5001`, la URL del servicio `web`) en lugar de `AllowAnyOrigin()`

### Requirement: Script SQL alternativo a migraciones de EF Core crea el esquema completo respetando dependencias de clave foránea
El sistema SHALL ofrecer `scripts/create_database.sql` como alternativa a `dotnet ef database update`, capaz de crear desde cero la base de datos `TechEvalDb` y sus 10 tablas en un servidor sin acceso al CLI de .NET, respetando en todo momento el orden de dependencias de clave foránea.

#### Scenario: Eliminación idempotente de tablas existentes en orden inverso de dependencias
- **GIVEN** una base de datos `TechEvalDb` en la que ya existen las 10 tablas de una ejecución previa del script
- **WHEN** se vuelve a ejecutar `scripts/create_database.sql`
- **THEN** el sistema SHALL eliminar primero las tablas dependientes (`ExamResults`, `UserAnswers`, `ExamSessions`, `ExamTokens`, `ExamQuestions`, `Exams`, `Answers`) antes de eliminar las tablas de las que dependen (`Questions`, `Categories`, `Users`), evitando errores de restricción de clave foránea

#### Scenario: Creación de tablas en el orden que respeta sus claves foráneas
- **GIVEN** el script `scripts/create_database.sql` sobre una base de datos `TechEvalDb` vacía
- **WHEN** se ejecuta el script completo
- **THEN** el sistema SHALL crear `Users` y `Categories` antes que `Questions`, `Questions` antes que `Answers` y `ExamQuestions`, y `Exams` antes que `ExamQuestions`, `ExamTokens`, `ExamSessions`, `UserAnswers` y `ExamResults`, de forma que cada `CONSTRAINT ... FOREIGN KEY` referencie siempre una tabla ya creada

### Requirement: Script de seed de preguntas no duplica categorías existentes
El sistema SHALL ofrecer `scripts/seed_questions_examen.sql` para poblar `TechEvalDb` con las 89 preguntas del examen de competencias, sin duplicar categorías que ya existan y sin fallar si el esquema fue creado por EF Core en lugar de por `create_database.sql`.

#### Scenario: Reejecución del script sin duplicar las categorías Frontend e iECS
- **GIVEN** una base de datos en la que las categorías `Frontend` e `iECS` ya fueron insertadas por una ejecución previa del script
- **WHEN** se vuelve a ejecutar `scripts/seed_questions_examen.sql`
- **THEN** el sistema SHALL comprobar `IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = 'Frontend'/'iECS')` antes de insertar, de modo que no se creen categorías duplicadas

#### Scenario: Compatibilidad con un esquema generado por EF Core
- **GIVEN** una base de datos `TechEvalDb` cuyo esquema fue creado por `EnsureCreatedAsync` de EF Core (sin los `DEFAULT` que añade `create_database.sql`)
- **WHEN** se ejecuta `scripts/seed_questions_examen.sql`
- **THEN** el sistema SHALL añadir los `DEFAULT CONSTRAINT` faltantes en `Categories.CreatedAt`, `Categories.IsActive`, `Questions.CreatedAt` y `Questions.IsActive` únicamente si no existen ya, sin fallar ni duplicar restricciones
