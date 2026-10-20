## MODIFIED Requirements

### Requirement: Nginx sirve el Blazor WASM con enrutamiento de SPA
El sistema SHALL configurar Nginx para servir los archivos estáticos del frontend Blazor WebAssembly, devolviendo `index.html` para cualquier ruta que no corresponda a un archivo físico, de modo que el enrutamiento del lado del cliente funcione en recargas de página o accesos directos a rutas profundas.

Nginx SHALL marcar como inmutable, con `Cache-Control: public, max-age=31536000, immutable`, solo un archivo cuyo nombre lleve una huella de contenido (`dotnet.native.6sals8nuk0.js`, `icudt_CJK.tjcz0u77k5.dat`): el contenido de ese nombre no cambia nunca. Todo lo demás —`index.html`, `appsettings.json`, `_framework/blazor.boot.json`, `_framework/blazor.webassembly.js`, `_framework/dotnet.js`, `css/` y `js/`— SHALL servirse con `Cache-Control: no-cache`, para que el navegador pregunte si cambió en cada carga y reciba un `304` si no. Un archivo sin huella marcado como inmutable deja a los usuarios con la versión vieja durante un año tras una actualización.

#### Scenario: Acceso directo a una ruta profunda de la SPA
- **GIVEN** la configuración `location / { try_files $uri $uri/ /index.html; }` en `nginx.conf`
- **WHEN** un usuario solicita una URL que no corresponde a ningún archivo físico en `/usr/share/nginx/html` (por ejemplo, una ruta de una página de Blazor)
- **THEN** Nginx SHALL responder con `index.html`, permitiendo que el enrutador de Blazor resuelva la ruta en el cliente

#### Scenario: Cache de assets estáticos versionados
- **GIVEN** un archivo publicado con una huella de diez caracteres en el nombre, como `dotnet.native.6sals8nuk0.js` o `System.Private.CoreLib.abc1234567.wasm`
- **WHEN** el navegador lo solicita
- **THEN** Nginx SHALL responder con `Cache-Control: public, max-age=31536000, immutable`

#### Scenario: Un archivo sin huella se revalida
- **GIVEN** un archivo publicado sin huella, como `_framework/blazor.webassembly.js`, `_framework/dotnet.js`, `_framework/blazor.boot.json`, `css/app.css`, `js/exam-timer.js` o `index.html`
- **WHEN** el navegador lo solicita
- **THEN** Nginx SHALL responder con `Cache-Control: no-cache`
- **AND** SHALL NOT responder con `immutable`

#### Scenario: Una ruta profunda de la SPA se revalida
- **GIVEN** una URL que no corresponde a ningún archivo y que Nginx resuelve con `index.html`
- **WHEN** el navegador la solicita
- **THEN** Nginx SHALL responder con `Cache-Control: no-cache`

## ADDED Requirements

### Requirement: La URL de la API de la web se fija al arrancar el contenedor
La imagen de la web SHALL generar `appsettings.json` al arrancar el contenedor, con el valor de la variable de entorno `API_BASE_URL` como `ApiBaseUrl`, antes de que Nginx empiece a servir. Sin la variable, SHALL usar `http://localhost:5000/`. Una barra final que falte SHALL añadirse. Un valor con comillas o barras invertidas SHALL impedir el arranque con un mensaje que lo diga, y no escribir un JSON roto. La imagen SHALL NOT llevar fijada la URL de desarrollo, y una misma imagen SHALL servir para cualquier despliegue.

#### Scenario: Sin variable
- **GIVEN** un contenedor de la web arrancado sin `API_BASE_URL`
- **WHEN** el navegador pide `/appsettings.json`
- **THEN** la respuesta es `{ "ApiBaseUrl": "http://localhost:5000/" }`

#### Scenario: Con una URL propia
- **GIVEN** un contenedor arrancado con `API_BASE_URL=https://api.empresa.com`
- **WHEN** el navegador pide `/appsettings.json`
- **THEN** la respuesta es `{ "ApiBaseUrl": "https://api.empresa.com/" }`, con la barra final añadida

#### Scenario: Un valor que rompería el JSON
- **GIVEN** un contenedor arrancado con `API_BASE_URL` que contiene una comilla
- **WHEN** arranca
- **THEN** el contenedor termina con un mensaje que nombra la variable y no sirve ningún `appsettings.json` roto

#### Scenario: La URL no se queda en una copia comprimida
- **GIVEN** una imagen publicada con `appsettings.json.br` y `appsettings.json.gz` de la URL de desarrollo
- **WHEN** el contenedor genera el `appsettings.json` nuevo
- **THEN** las copias comprimidas SHALL borrarse, y ningún cliente recibe la URL vieja

### Requirement: Los enlaces de los correos y el origen CORS salen de una sola variable
`docker-compose.yml` SHALL pasar a la API el valor de la variable `FRONTEND_BASE_URL` como `FrontendBaseUrl` y como `AllowedOrigins`, con `http://localhost:5001` por defecto. Así los enlaces de las invitaciones y de «fijar contraseña» llevan a la web que sirve este stack, y la API acepta solo el origen de esa web. El valor SHALL ser el origen sin barra final.

#### Scenario: Por defecto, en este equipo
- **GIVEN** un `.env` sin `FRONTEND_BASE_URL`
- **WHEN** se renderiza `docker-compose.yml`
- **THEN** el servicio `api` lleva `FrontendBaseUrl=http://localhost:5001` y `AllowedOrigins=http://localhost:5001`

#### Scenario: Fuera de este equipo
- **GIVEN** un `.env` con `FRONTEND_BASE_URL=https://techeval.empresa.com`
- **WHEN** la API envía una invitación
- **THEN** el enlace del correo empieza por `https://techeval.empresa.com/`
- **AND** la política CORS de la API acepta ese origen

### Requirement: La edición de SQL Server se elige de forma explícita
`docker-compose.yml` SHALL tomar la edición de SQL Server de la variable obligatoria `MSSQL_PID` y SHALL NOT fijarla. `.env.example` SHALL ofrecer `Developer` como valor para desarrollo y SHALL avisar de que esa edición no tiene licencia para producción, y de que producción necesita `Express`, `Standard` o `Enterprise`.

#### Scenario: Falta la variable
- **GIVEN** un `.env` sin `MSSQL_PID`
- **WHEN** se ejecuta `docker compose config`
- **THEN** Compose termina con un error que nombra `MSSQL_PID`

#### Scenario: Edición de producción
- **GIVEN** un `.env` con `MSSQL_PID=Express`
- **WHEN** se renderiza `docker-compose.yml`
- **THEN** el servicio `sqlserver` lleva `MSSQL_PID=Express`
