# fix-delivery-and-docs

Que el proyecto se pueda levantar siguiendo sus propias instrucciones, y que esas instrucciones digan la verdad

## Why

Cuatro cosas de la entrega, encontradas al verificar los arreglos del 16·09. Ninguna es de lógica de negocio; las cuatro hacen que el proyecto mienta sobre sí mismo.

**E11 — el arranque documentado con Docker no funciona.** El `healthcheck` de `sqlserver` invoca `/opt/mssql-tools/bin/sqlcmd`, que no existe en la imagen de SQL Server 2022: allí solo está `/opt/mssql-tools18`. El servicio nunca pasa a `healthy`, y `api` lo espera con `condition: service_healthy`. Así que `docker compose up` levanta la base de datos y se queda ahí para siempre. El README empieza por ahí.

**E9 — el paquete de Bootstrap está bloqueado.** El atributo `integrity` de `bootstrap.bundle.min.js` no coincide con lo que llega, y el navegador bloquea el recurso en cada carga. La aplicación corre sin el JavaScript de Bootstrap, y el fallo es silencioso salvo que alguien abra la consola.

**E10 — la documentación describe un sistema que ya no existe.** `documentacion.md` conserva 43 menciones del modelo de IA local, una sección 8 entera y su servicio de Compose. Esa función se retiró hace una semana.

**S7 — falta la rotación.** El cambio `secrets-out-of-the-repo` sacó los secretos del repositorio, pero no rotó nada, y sin rotar el hallazgo sigue abierto: lo que estuvo en git sigue en git. La rotación la tiene que hacer una persona, porque exige emitir credenciales reales y tocar los sistemas de destino. Lo que sí se puede preparar es la herramienta.

## What Changes

- El `healthcheck` apunta a `mssql-tools18` y usa `-C`, que la versión 18 exige.
- El `integrity` de Bootstrap pasa al valor que corresponde al contenido que se sirve.
- `documentacion.md` deja de describir la generación con IA.
- Un guion de rotación que **ejecuta la persona**, genera los valores en su máquina y escribe el `.env`. Ni los muestra por pantalla ni los envía a ninguna parte.
- Una lista de comprobación de la rotación, con los pasos que no se pueden automatizar.

## Capabilities

- `deployment-ops` — el arranque con Compose y la rotación de secretos

## Impact

**Sin cambios de esquema.** Sin cambios de API. Sin cambios de comportamiento de la aplicación.

**El `healthcheck` arreglado cambia lo que hace `docker compose up`**: donde antes se quedaba esperando, ahora arranca la API. Es el efecto buscado.

**Bootstrap vuelve a cargar.** Lo que dependa de su JavaScript —desplegables, ventanas modales, avisos descartables— pasa a funcionar. Conviene mirar las pantallas que lo usen, porque llevan tiempo comportándose de otra manera.

**El guion de rotación no rota nada por sí solo.** Genera valores y escribe el `.env` local. Cambiar la contraseña de `sa` en el servidor, revocar la clave de SMTP en el proveedor y reasignar la del administrador siguen siendo pasos manuales, y están en la lista.
