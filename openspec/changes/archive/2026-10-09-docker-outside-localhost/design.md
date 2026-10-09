## Context

Ver `proposal.md` — Why. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- Una web Blazor WebAssembly se ejecuta **en el navegador**. `ApiBaseUrl` y `FrontendBaseUrl` no son direcciones entre contenedores: son direcciones tal como las ve quien usa la aplicación. Dentro de la red de Compose la API es `http://api:8080`; para el navegador es `http://localhost:5000` o el dominio público. Por eso no sirve poner el nombre del servicio de Compose.
- `dotnet publish` copia `wwwroot/appsettings.json` a la salida, y la web lo lee con `builder.Configuration["ApiBaseUrl"]` en `Program.cs`. El fichero se pide al servidor en cada arranque de la aplicación, así que se puede sustituir sin tocar el código.
- Esa publicación deja también `appsettings.json.br` y `appsettings.json.gz`. nginx no los sirve hoy (`gzip on`, sin `gzip_static`), pero un cambio de configuración futuro los serviría con la URL vieja.
- La imagen `nginx:alpine` ejecuta, antes de arrancar, los `*.sh` ejecutables de `/docker-entrypoint.d/` con `set -e`: un fallo detiene el contenedor.
- La API ya lee `FrontendBaseUrl` y `AllowedOrigins` de la configuración; Compose pasa variables de entorno con `__` o con el nombre plano, y ASP.NET las lee como configuración. Ningún cambio de código.
- Lo publicado por la web: 62 ficheros sin contar las copias comprimidas. 53 llevan una huella de diez caracteres en el nombre (`dotnet.native.6sals8nuk0.js`, `icudt_CJK.tjcz0u77k5.dat`, 48 `.wasm`) y 9 no (`index.html`, `appsettings.json`, `css/app.css`, `js/exam-*.js` ×3, `_framework/blazor.boot.json`, `blazor.webassembly.js`, `dotnet.js`).
- El demonio de Docker Desktop no estaba en marcha al implementar; se arrancó después para la tarea 3.1.

## Goals / Non-Goals

**Goals:**

- Que la misma imagen de la web sirva en cualquier despliegue, apuntando a la API que se le diga al arrancar.
- Que los enlaces de los correos y el origen CORS no puedan quedar desincronizados entre sí.
- Que ninguna actualización deje a un navegador con ficheros viejos.

**Non-Goals:**

- TLS en el contenedor, publicar SQL Server solo en `127.0.0.1`, ni poner huella a `css/app.css` y `js/exam-*.js`.

## Decisions

### D1. `appsettings.json` se genera al arrancar, con un guion en `/docker-entrypoint.d/`

`docker/web-appsettings.sh` se copia a `/docker-entrypoint.d/40-appsettings.sh`. Escribe el fichero con `printf` y no con `envsubst` ni `jq`: la imagen es `nginx:alpine` y no trae `jq`, y `envsubst` sustituiría todo `$` de una plantilla, que aquí no hay.

- Sin la variable, `http://localhost:5000/`: lo que sirve el stack de Compose en este equipo.
- La barra final se añade. `HttpClient.BaseAddress` con `https://x/api` descarta `api` al resolver una ruta relativa.
- Una comilla o una barra invertida **detienen el arranque**: escribirlas rompería el JSON, y la web arrancaría sin saber dónde está la API, con un error en la consola del navegador y nada en el servidor. Mejor un contenedor que no arranca y dice por qué.
- Se borran `appsettings.json.br` y `.gz`.
- `HTML_DIR` se puede cambiar solo para probar el guion fuera de un contenedor.

Alternativas descartadas:

- **Un argumento de construcción (`ARG`)**: ata la imagen a un despliegue y obliga a reconstruirla para cambiar de API.
- **Que la web use una ruta relativa (`/api`) y nginx haga de proxy hacia la API.** Elimina CORS y la variable, y es probablemente lo mejor a largo plazo, pero cambia la arquitectura (la API pasa a no ser pública) y el límite de tasa por origen de S5 vería siempre la dirección del contenedor de nginx. Es un cambio aparte.

### D2. `FRONTEND_BASE_URL` alimenta `FrontendBaseUrl` y `AllowedOrigins`

Son siempre la misma dirección (el origen de la web) y tenerlas en dos variables permite que dejen de coincidir: la invitación lleva a una web que la API rechaza por CORS. Un `${VAR:-valor}` con el mismo valor por defecto en las dos líneas. El valor es un **origen**: sin barra final, porque CORS compara cadenas y porque `FrontendBaseUrl` se concatena con `/prueba/{token}`.

No se valida en el arranque que no sea `localhost`: `localhost` es el valor correcto para probar en este equipo, incluso con `ASPNETCORE_ENVIRONMENT=Production`.

### D3. nginx: dos reglas

1. `location ~* "\.[a-z0-9]{10}\.(js|wasm|dat)$"` → `public, max-age=31536000, immutable`. Es la forma en que .NET 9 pone la huella (diez caracteres en base 36 entre el nombre y la extensión). Sin `try_files`: un nombre con huella que ya no existe responde 404 y no `index.html`, para no servir HTML como si fuera un `.wasm`.
2. `location /` → `try_files` y `no-cache`. `no-cache` no es «no guardar»: el navegador guarda y pregunta con `If-None-Match`; nginx responde `304` con la caché de `ETag` y `Last-Modified` de ficheros estáticos que ya genera.

Se quita `expires 1y`: junto con `add_header Cache-Control` mandaba dos cabeceras `Cache-Control`. Se quitan `png|jpg|gif|ico` de la regla: la web no publica ninguna imagen, y si algún día lo hace, sin huella se revalida.

La expresión regular se comprobó contra los 62 nombres publicados: 53 casan, los 9 sin huella no, y ninguno con huella queda fuera.

### D4. `MSSQL_PID` obligatoria

Como las demás variables de Compose: `${MSSQL_PID:?mensaje}`. Una variable con un valor por defecto `Developer` heredaría la edición sin licencia a quien despliegue sin leer; obligar a elegir es lo único que hace que alguien lo lea. `.env.example` trae `MSSQL_PID=Developer` para desarrollo.

### D5. `.dockerignore` y `.gitattributes`

- Sin `.dockerignore`, `COPY src/ src/` trae al contenedor los `bin/` y `obj/` de quien construye. Un `obj/project.assets.json` de Windows, con sus rutas, sustituye al que `dotnet restore` acaba de generar y rompe la compilación en Linux. Con la carpeta de trabajo de este equipo, que tiene `bin/` y `obj/` de varias compilaciones, es seguro que ocurre. Además, `.env` (con los secretos) entra en el contexto aunque ninguna instrucción lo copie.
- `.gitattributes` con `*.sh text eol=lf`: git en Windows deja CRLF en la copia de trabajo, y un guion con `#!/bin/sh\r` falla en Alpine con «not found». El `sed` del Dockerfile es la segunda red.

## Risks / Trade-offs

- **Verificado con la imagen de la web el 09·10·2026** (tarea 3.1): `nginx -t`, el guion de arranque dentro del contenedor y las cabeceras reales. **No verificado:** el stack completo con `docker compose up` (exige la imagen de SQL Server) y la construcción de `Dockerfile.api` con el nuevo `.dockerignore`.
- **`no-cache` cuesta una petición condicional por fichero sin huella** (nueve) en cada carga. Es un `304` de unos cientos de bytes.
- **`MSSQL_PID` obligatoria rompe los `.env` existentes.** Es un mensaje claro y una línea que añadir.
- **`appsettings.json` generado al arrancar** significa que cambiar la URL pide reiniciar el contenedor, no reconstruirlo.
