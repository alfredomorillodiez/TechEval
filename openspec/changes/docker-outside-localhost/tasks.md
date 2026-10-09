## 1. Imagen de la web

- [x] 1.1 `docker/web-appsettings.sh`, `Dockerfile.web` (copiar el guion a `/docker-entrypoint.d/`, quitar CRLF, hacerlo ejecutable, `ENV API_BASE_URL`) y `.gitattributes` con `*.sh text eol=lf` (design.md D1, D5). Verificar el guion con seis casos en un directorio temporal: sin variable, sin barra final, con barra final, con ruta, con comilla (sale con 1 y no escribe) y con barra invertida (ídem); en todos los éxitos, ninguna copia `.br` ni `.gz` queda; `sh -n` pasa.
- [x] 1.2 `nginx.conf` con las dos reglas (design.md D3). Verificar la expresión regular contra los 62 nombres de fichero de un `dotnet publish` real: 53 casan, los 9 sin huella no, ninguno con huella queda fuera.
- [x] 1.3 `.dockerignore` (design.md D5). Verificar que no excluye nada que usen los dos `Dockerfile` (`src/`, `nginx.conf`, `docker/`).

## 2. Compose y documentación

- [x] 2.1 `docker-compose.yml` (`MSSQL_PID` obligatoria, `FrontendBaseUrl` y `AllowedOrigins` desde `FRONTEND_BASE_URL`, `API_BASE_URL` en la web) y `.env.example` (design.md D2, D4). Verificar con `docker compose config`: falla nombrando `MSSQL_PID` si falta; con los valores por defecto da `http://localhost:5001` y `http://localhost:5000/`; con `FRONTEND_BASE_URL` y `API_BASE_URL` propias salen las propias en los tres sitios.
- [x] 2.2 `README.md` y `documentacion.md`: variables nuevas, cómo desplegar fuera de `localhost`, y la edición de SQL Server. `openspec validate docker-outside-localhost --strict` pasa.

## 3. Con Docker

- [ ] 3.1 Con el demonio de Docker en marcha: `docker compose build web`, `docker run --rm -e API_BASE_URL=https://api.ejemplo.com techeval-web nginx -t`, y con el contenedor en marcha, `curl -sI` sobre `/index.html`, `/appsettings.json`, `/css/app.css`, `/_framework/blazor.boot.json` (todas con `no-cache`), un `.wasm` con huella (con `immutable`), una ruta profunda como `/admin/results` (con `no-cache`) y `/appsettings.json` con el cuerpo de la URL pedida. Pendiente: el demonio no estaba en marcha al implementar.
