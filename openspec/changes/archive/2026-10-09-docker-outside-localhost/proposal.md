## Why

El hallazgo E12 del análisis del 08·10·2026: el arranque con Docker solo sirve en `localhost`, y ni siquiera ahí del todo. Cuatro causas, todas en los ficheros de despliegue:

- **La web llama a la API que no es.** `ApiBaseUrl` sale de `wwwroot/appsettings.json`, que el `dotnet publish` copia tal cual a la imagen: `https://localhost:60807/`, el puerto de desarrollo. Con Compose la API está en `http://localhost:5000`. La imagen no se puede apuntar a otra API sin reconstruirla.
- **Los enlaces de los correos apuntan a la web de desarrollo.** Compose no define `FrontendBaseUrl`, así que la API usa el de `appsettings.json` (`https://localhost:60805`). Una invitación enviada desde el stack de Docker lleva a un puerto donde no hay nada. Y `AllowedOrigins` tampoco se define: vale `http://localhost:5001` por defecto, que solo es correcto en este equipo.
- **nginx marca como inmutable lo que no lo es.** Pone `Cache-Control: public, immutable` durante un año a todo `.js`, `.css`, `.wasm` y `.dat`. De los 62 ficheros que publica la web, 9 no llevan huella en el nombre: `blazor.webassembly.js`, `dotnet.js`, `css/app.css` y los tres `js/exam-*.js`. Tras una actualización, el navegador sigue con la versión vieja durante un año. `blazor.boot.json`, que lista los ficheros que sí tienen huella, y `index.html` no llevan cabecera de caché: el navegador decide por su cuenta cuánto guardarlos.
- **SQL Server se levanta como `Developer`.** Esa edición no tiene licencia para producción, y nadie decide nada: el valor está escrito en `docker-compose.yml`.

## What Changes

- **La URL de la API de la web se fija al arrancar el contenedor.** La imagen escribe `appsettings.json` desde la variable `API_BASE_URL` (por defecto `http://localhost:5000/`) antes de arrancar nginx. Una misma imagen sirve para cualquier despliegue.
- **Una sola variable, `FRONTEND_BASE_URL`, para los enlaces y para CORS.** Compose la pasa a la API como `FrontendBaseUrl` y como `AllowedOrigins`. Por defecto `http://localhost:5001`. Fuera de este equipo se define en `.env` y la API deja de mandar enlaces y de aceptar orígenes que no son los suyos.
- **nginx solo marca como inmutable lo que lleva huella** en el nombre (`dotnet.native.6sals8nuk0.js`). Todo lo demás se revalida en cada carga con `Cache-Control: no-cache`: el navegador lo guarda, pregunta si cambió y recibe un `304` barato.
- **La edición de SQL Server se elige a propósito.** `MSSQL_PID` pasa a ser obligatoria en `.env`, con `Developer` como valor de ejemplo para desarrollo y una nota que dice que producción necesita `Express`, `Standard` o `Enterprise`.
- **`.env.example` y la documentación** explican las tres variables nuevas y cómo desplegar fuera de `localhost`: dónde termina TLS, qué URL va en cada variable y qué hacer con el proxy.
- **Fin de línea de los guiones de la imagen.** Un `.gitattributes` fuerza `eol=lf` en `*.sh`: con CRLF, el guion de arranque falla en Alpine con un error que no dice la causa.

**BREAKING:** `docker-compose.yml` exige `MSSQL_PID` en `.env`. Quien ya tiene un `.env` debe añadirla.

Fuera de alcance:

- TLS en el contenedor. nginx sigue escuchando en el 80; el certificado lo pone un proxy delante, como ya dice el README.
- Publicar el puerto de SQL Server solo en `127.0.0.1`.
- Huella en los ficheros propios (`js/exam-*.js`, `css/app.css`). Con `no-cache` cuestan un `304` por carga; ponerles huella exige tocar `index.html` en cada cambio.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `deployment-ops`: la caché de nginx, la URL de la API del cliente, los enlaces y el origen CORS en Compose, y la edición de SQL Server.

## Impact

- **Ficheros**: `docker-compose.yml`, `Dockerfile.web`, `nginx.conf`, `.env.example`, `.gitattributes` (nuevo) y `docker/web-appsettings.sh` (nuevo).
- **Código de la aplicación**: ninguno.
- **Documentación**: `README.md` y `documentacion.md` (sección Docker y referencia de configuración).
- **Verificación**: la sintaxis de `nginx.conf` y las cabeceras de caché se comprueban con las imágenes reales; si el demonio de Docker no está disponible, se comprueban con `docker compose config`, el guion de arranque probado en un directorio temporal y la expresión regular contra los 62 nombres de fichero publicados.
