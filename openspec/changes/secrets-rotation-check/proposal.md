## Why

S7 sigue abierto: los secretos que estuvieron en git hasta el 16·09·2026 (la contraseña de `sa`, la clave JWT, la contraseña del administrador y las credenciales SMTP) siguen en el historial, y la rotación la tiene que hacer una persona. Dos cosas la dificultan hoy, y las dos se pueden arreglar sin tocar ningún sistema de destino:

- **Los guiones de rotación rompen el despliegue.** `rotar-secretos.sh` y `.ps1` reescriben `.env` entero a partir de una plantilla fija. Con `docker-outside-localhost`, `docker-compose.yml` exige también `MSSQL_PID`, `FRONTEND_BASE_URL` y `API_BASE_URL`; un `.env` que sale del guion no los lleva y Compose no arranca. Y ya hoy borran `O365_TENANT_ID`, `O365_CLIENT_ID` y `EMAIL_FROM`, que no cambian al rotar (el propio procedimiento lo dice), obligando a volver a buscarlos.
- **No hay forma de saber si la rotación se hizo.** El procedimiento termina con «comprueba que funciona», que no dice nada: un despliegue con los valores viejos funciona igual de bien. Lo único que prueba que se rotó es que el valor que está en uso **no aparece en el historial**, y eso se puede comprobar con `git log -S`.

Dato verificado el 09·10·2026: el secreto de cliente de Microsoft Graph **nunca estuvo en git**. El historial solo lo menciona con marcadores de posición en la documentación. No entra en la lista de lo que hay que rotar por haber estado versionado.

## What Changes

- **Los guiones de rotación conservan lo que no es secreto.** Copian del `.env` anterior `MSSQL_PID`, `FRONTEND_BASE_URL`, `API_BASE_URL`, `O365_TENANT_ID`, `O365_CLIENT_ID` y `EMAIL_FROM`. Si no estaban, escriben los valores de ejemplo de `.env.example` para las dos URL y dejan `MSSQL_PID` vacío, para que Compose obligue a elegir. Los cuatro secretos (`SA_PASSWORD`, `JWT_SECRET_KEY`, `ADMIN_PASSWORD`, `O365_CLIENT_SECRET`) siguen sin copiarse: los tres primeros se generan nuevos y el último queda vacío, porque lo emite Entra ID.
- **Nuevo guion `comprobar-rotacion` (`.ps1` y `.sh`).** Lee `.env` y, para cada secreto, busca su valor en todo el historial de git (`git log --all -S`). Si aparece, dice cuál (por nombre, nunca por valor) y sale con error. También falla si `.env` o `appsettings.Local.json` están rastreados, si falta un secreto, si un valor es demasiado corto para comprobarlo o si el repositorio es una copia superficial (un historial incompleto daría un falso «todo bien»). No imprime ningún valor.
- **El procedimiento de rotación** (`docs/rotacion-de-secretos.md`) usa la comprobación como paso final, aclara que el secreto de Graph no estuvo versionado, y lista las variables nuevas.

Fuera de alcance, porque exigen acceso a sistemas de destino y siguen siendo de una persona: cambiar la contraseña de `sa` en el servidor, revocar la credencial SMTP del buzón, emitir y revocar secretos en Entra ID, y reasignar la contraseña del administrador ya creado. Tampoco se reescribe el historial.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `deployment-ops`: el guion de rotación conserva lo que no es secreto, y hay una comprobación de que la rotación se hizo.

## Impact

- **Ficheros**: `scripts/rotar-secretos.sh`, `scripts/rotar-secretos.ps1`, `scripts/comprobar-rotacion.sh` y `.ps1` (nuevos), `docs/rotacion-de-secretos.md`.
- **Código de la aplicación**: ninguno.
- **Verificación**: los guiones se prueban en repositorios temporales con valores inventados (nunca con el `.env` real), incluido el caso de un valor que sí está en el historial.
