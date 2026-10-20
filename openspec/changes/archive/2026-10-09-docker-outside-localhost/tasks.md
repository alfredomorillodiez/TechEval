## 1. Imagen de la web

- [x] 1.1 `docker/web-appsettings.sh`, `Dockerfile.web` (copiar el guion a `/docker-entrypoint.d/`, quitar CRLF, hacerlo ejecutable, `ENV API_BASE_URL`) y `.gitattributes` con `*.sh text eol=lf` (design.md D1, D5). Verificar el guion con seis casos en un directorio temporal: sin variable, sin barra final, con barra final, con ruta, con comilla (sale con 1 y no escribe) y con barra invertida (ídem); en todos los éxitos, ninguna copia `.br` ni `.gz` queda; `sh -n` pasa.
- [x] 1.2 `nginx.conf` con las dos reglas (design.md D3). Verificar la expresión regular contra los 62 nombres de fichero de un `dotnet publish` real: 53 casan, los 9 sin huella no, ninguno con huella queda fuera.
- [x] 1.3 `.dockerignore` (design.md D5). Verificar que no excluye nada que usen los dos `Dockerfile` (`src/`, `nginx.conf`, `docker/`).

## 2. Compose y documentación

- [x] 2.1 `docker-compose.yml` (`MSSQL_PID` obligatoria, `FrontendBaseUrl` y `AllowedOrigins` desde `FRONTEND_BASE_URL`, `API_BASE_URL` en la web) y `.env.example` (design.md D2, D4). Verificar con `docker compose config`: falla nombrando `MSSQL_PID` si falta; con los valores por defecto da `http://localhost:5001` y `http://localhost:5000/`; con `FRONTEND_BASE_URL` y `API_BASE_URL` propias salen las propias en los tres sitios.
- [x] 2.2 `README.md` y `documentacion.md`: variables nuevas, cómo desplegar fuera de `localhost`, y la edición de SQL Server. `openspec validate docker-outside-localhost --strict` pasa.

## 3. Con Docker

- [x] 3.1 Con el demonio de Docker en marcha (arrancado el 09·10·2026): `docker build -f Dockerfile.web` construye; `nginx -t` pasa; con el contenedor en marcha, `/index.html`, `/appsettings.json`, `/css/app.css`, `/js/exam-timer.js`, `/_framework/blazor.boot.json`, `blazor.webassembly.js`, `dotnet.js` y una ruta profunda (`/admin/results`) responden `no-cache`; un `.wasm` y un `.dat` con huella responden `public, max-age=31536000, immutable`; un nombre con huella que no existe da 404 y no `index.html`; `blazor.boot.json` con `If-None-Match` da 304; `/appsettings.json` trae la URL pedida con la barra final, y sin la variable `http://localhost:5000/`; una comilla en `API_BASE_URL` detiene el contenedor con código 1; no quedan copias `.br` ni `.gz` del `appsettings.json`. Sin verificar: `docker compose up` del stack entero (exige la imagen de SQL Server, de 1,5 GB) ni la construcción de `Dockerfile.api` con el nuevo `.dockerignore`.
