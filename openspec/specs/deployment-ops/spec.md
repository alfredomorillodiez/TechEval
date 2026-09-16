# deployment-ops Specification

## Purpose
TBD - created by archiving change deployment-ops. Update Purpose after archive.

## Requirements

### Requirement: Levantamiento del stack completo con Docker Compose
El sistema SHALL permitir levantar la plataforma completa (base de datos, API y frontend) con un único comando de Docker Compose, garantizando que cada servicio solo arranque cuando sus dependencias estén realmente disponibles.

La comprobación de salud de la base de datos SHALL invocar una herramienta que exista en la imagen que se usa. Una comprobación que invoque una ruta inexistente falla siempre, y con `condition: service_healthy` eso no retrasa el arranque de la API: lo impide para siempre.

#### Scenario: Arranque en modo producción
- **GIVEN** el fichero `docker-compose.yml` del repositorio
- **WHEN** se ejecuta `docker-compose up -d` sin especificar perfil
- **THEN** el sistema arranca los servicios `sqlserver`, `api` y `web`
- **AND** el servicio `mailhog` MUST NOT arrancar, al estar declarado bajo el perfil `dev`

#### Scenario: La API espera a que SQL Server esté saludable antes de arrancar
- **GIVEN** el servicio `sqlserver` con un `healthcheck` basado en `sqlcmd -Q 'SELECT 1'`
- **WHEN** se levanta el stack con `docker-compose up`
- **THEN** el servicio `api` SHALL permanecer sin arrancar hasta que la condición `service_healthy` de `sqlserver` se cumpla

#### Scenario: La comprobación de salud llega a pasar
- **GIVEN** la imagen de SQL Server que declara `docker-compose.yml`
- **WHEN** el contenedor termina de arrancar
- **THEN** la comprobación de salud SHALL pasar a `healthy`, de forma que `api` arranque
- **AND** la comprobación SHALL invocar la ruta de `sqlcmd` que existe en esa imagen

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

### Requirement: Script SQL aditivo añade las columnas de corrección manual sin pérdida de datos
El sistema SHALL ofrecer `scripts/add_review_columns.sql` para actualizar el esquema de una base de datos `TechEvalDb` ya existente con las columnas necesarias para la corrección manual (`ExamResults.Status`, `ExamResults.ReviewedAt`, `ExamResults.ReviewedByUserId`, `UserAnswers.AwardedPoints`, `UserAnswers.ReviewerComment`), siguiendo el patrón idempotente y no destructivo ya establecido en `scripts/add_user_link_columns.sql`. El script SHALL poder ejecutarse varias veces sin error y SHALL preservar todos los datos existentes.

#### Scenario: Ejecución sobre una base de datos con datos previos
- **GIVEN** una base de datos `TechEvalDb` con exámenes, preguntas y resultados ya registrados
- **WHEN** se ejecuta `scripts/add_review_columns.sql`
- **THEN** el sistema SHALL añadir las columnas nuevas comprobando antes su existencia con `COL_LENGTH`, sin eliminar ni modificar ninguna fila existente

#### Scenario: Reejecución idempotente del script
- **GIVEN** una base de datos en la que el script ya se ejecutó con éxito
- **WHEN** se vuelve a ejecutar `scripts/add_review_columns.sql`
- **THEN** el sistema SHALL detectar que las columnas, la clave foránea y los índices ya existen y SHALL terminar sin error ni duplicados

#### Scenario: Retrocompatibilidad de los resultados históricos
- **GIVEN** resultados creados antes de este cambio, con la corrección ya cerrada de hecho
- **WHEN** se ejecuta el script de actualización
- **THEN** el sistema SHALL asignarles `Status = Reviewed` como valor por defecto, de modo que no aparezcan en la cola de correcciones pendientes ni alteren las métricas del dashboard

#### Scenario: Clave foránea del corrector
- **WHEN** el script añade la columna `ExamResults.ReviewedByUserId`
- **THEN** el sistema SHALL crearla como `INT NULL` con una clave foránea hacia `dbo.Users(Id)` y un índice asociado, comprobando antes que no existan ya, en coherencia con el tratamiento de `ExamResults.UserId`

### Requirement: El script de creación completa del esquema incluye las columnas de corrección
El sistema SHALL mantener `scripts/create_database.sql` alineado con el modelo, incluyendo las columnas de corrección manual en las definiciones de `ExamResults` y `UserAnswers`, de forma que una base de datos creada desde cero con ese script y otra actualizada con `scripts/add_review_columns.sql` converjan en el mismo esquema.

#### Scenario: Creación desde cero incluye las columnas de corrección
- **WHEN** se ejecuta `scripts/create_database.sql` sobre un servidor sin la base de datos
- **THEN** las tablas `ExamResults` y `UserAnswers` SHALL crearse ya con las columnas de estado, trazabilidad de corrección y puntuación otorgada, sin necesidad de ejecutar después el script aditivo

### Requirement: Los secretos llegan por configuración, no por el repositorio
El repositorio NEVER SHALL contener el valor de un secreto de producción: contraseñas de base de datos, claves de firma, contraseñas de cuenta ni credenciales de servicios externos. Los ficheros versionados SHALL declarar el **nombre** de cada secreto y dejar su valor vacío o expresado como una variable de entorno sin valor por defecto.

El sistema MUST negarse a arrancar fuera del entorno de desarrollo si falta cualquier secreto obligatorio, o si alguno conserva el valor documentado para desarrollo. Un despliegue mal configurado SHALL parar en seco con un mensaje que nombre lo que falta, y NEVER SHALL arrancar con un valor por defecto conocido.

Los valores de desarrollo MAY estar versionados en el fichero de configuración de desarrollo, porque son públicos por definición y el arranque en producción los rechaza explícitamente.

#### Scenario: Arranque en producción sin la clave de firma
- **GIVEN** un despliegue con el entorno distinto de desarrollo y sin valor para la clave JWT
- **WHEN** la aplicación arranca
- **THEN** el sistema MUST detener el arranque con un error que nombre la clave que falta
- **AND** el sistema SHALL NOT generar ni asumir ninguna clave

#### Scenario: Arranque en producción con el valor de desarrollo
- **GIVEN** un despliegue con el entorno distinto de desarrollo y una clave JWT igual a la documentada para desarrollo
- **WHEN** la aplicación arranca
- **THEN** el sistema MUST detener el arranque, porque un valor público no sirve como secreto

#### Scenario: Arranque en producción sin contraseña de administrador
- **GIVEN** un despliegue con el entorno distinto de desarrollo y sin valor para la contraseña del administrador
- **WHEN** la aplicación arranca
- **THEN** el sistema MUST detener el arranque
- **AND** el sistema SHALL NOT sembrar el administrador con ninguna contraseña por defecto

#### Scenario: Arranque en desarrollo
- **GIVEN** el entorno de desarrollo y los valores documentados en su fichero de configuración
- **WHEN** la aplicación arranca
- **THEN** el sistema MUST arrancar con normalidad, sin exigir variables de entorno adicionales

#### Scenario: Arranque del stack con variables sin definir
- **GIVEN** una máquina sin las variables de entorno que declara `docker-compose.yml`
- **WHEN** se levanta el stack
- **THEN** Compose MUST fallar nombrando la variable que falta, en lugar de sustituirla por una cadena vacía

#### Scenario: El fichero de configuración local no viaja en el repositorio
- **WHEN** se inspecciona el contenido versionado del repositorio
- **THEN** el fichero de configuración local del desarrollador SHALL NOT estar entre los ficheros seguidos por git

### Requirement: Herramienta de rotación de secretos
El repositorio SHALL incluir un guion que genere valores nuevos para los secretos del despliegue y los escriba en el fichero de entorno local.

El guion SHALL generarlos en la máquina de quien lo ejecuta. NEVER SHALL enviarlos a ningún servicio, NEVER SHALL escribirlos en la salida estándar y NEVER SHALL dejarlos en un fichero versionado.

El guion SHALL NOT rotar por sí solo: cambiar la contraseña de la base de datos en el servidor, revocar la credencial del proveedor de correo y reasignar la del administrador son pasos que exigen acceso a esos sistemas. El repositorio SHALL documentarlos como lista de comprobación.

#### Scenario: Generación de valores nuevos
- **WHEN** una persona ejecuta el guion de rotación
- **THEN** el guion SHALL escribir un fichero de entorno con valores generados al azar
- **AND** SHALL NOT mostrar ninguno de esos valores por pantalla

#### Scenario: No se sobrescribe lo que ya existe sin avisar
- **GIVEN** un fichero de entorno ya presente
- **WHEN** se ejecuta el guion
- **THEN** el guion SHALL detenerse o guardar una copia del anterior, en lugar de perder valores que pueden estar en uso

#### Scenario: Lo que el guion no puede hacer queda escrito
- **WHEN** alguien consulta la documentación de la rotación
- **THEN** SHALL encontrar la lista de pasos manuales, con el aviso de que rotar la clave de firma cierra todas las sesiones abiertas, incluidos los exámenes en curso
