# Rotación de secretos

Procedimiento para sustituir los cuatro secretos que estuvieron versionados en el repositorio hasta el 16·09·2026.

## Por qué es obligatorio

Los valores se sacaron del repositorio, pero **siguen en el historial de git**. Quien haya clonado el repositorio alguna vez los conserva aunque hoy no aparezcan en ningún fichero. Hay que darlos por comprometidos.

Mover un secreto no lo limpia. Solo lo limpia emitir otro.

## Lo que hay que rotar

| Secreto | Dónde estuvo | Qué hay que hacer |
|---|---|---|
| Contraseña de `sa` de SQL Server | `docker-compose.yml` | Cambiarla en el servidor **y** en `.env` |
| Clave de firma JWT | `docker-compose.yml` y `appsettings.json` | Solo en `.env`. **Cierra todas las sesiones abiertas** |
| Contraseña del administrador | `docker-compose.yml` y `appsettings.json` | En `.env` **y** reasignarla a la cuenta ya creada |
| Credenciales SMTP | `appsettings.Local.json`, versionado desde el primer commit | Cambiar la contraseña del buzón, aunque TechEval ya no la use (ver el paso 3) |

**Lo que no hay que rotar por haber estado en git:** el secreto de cliente de Microsoft Graph (`O365_CLIENT_SECRET`). Se comprobó el 09·10·2026 que no ha estado en ningún commit: el historial solo lo nombra con marcadores de posición en la documentación. Se rota igualmente si alguien lo ha compartido por otro medio, o cuando caduque el de Entra ID.

## Antes de empezar

> **Rotar la clave JWT cierra todas las sesiones abiertas.** Eso incluye las invitaciones de examen en curso: un candidato que esté resolviendo una prueba perderá su sesión autenticada.
>
> Elige una hora sin exámenes. Si no la hay, avisa a los candidatos afectados y prepárate para reenviar invitaciones.

Los administradores también tendrán que volver a entrar. Eso no tiene más consecuencia que la molestia.

## Pasos

### 1. Generar los valores nuevos

```bash
./scripts/rotar-secretos.sh
```

En Windows:

```powershell
pwsh scripts/rotar-secretos.ps1
```

El guion escribe `.env` y guarda una copia del anterior con marca de tiempo. **No muestra los valores**: no hace falta verlos, y verlos los expone al historial de la terminal.

Del `.env` anterior **conserva lo que no es un secreto** y no cambia al rotar: `MSSQL_PID`, `FRONTEND_BASE_URL`, `API_BASE_URL`, `O365_TENANT_ID`, `O365_CLIENT_ID` y `EMAIL_FROM`. Los cuatro secretos nunca se copian: tres se generan nuevos y `O365_CLIENT_SECRET` queda vacío, porque lo emite Entra ID. Si el `.env` anterior no tenía `MSSQL_PID`, el guion lo deja vacío y lo avisa: Compose no arranca hasta que elijas la edición (`Developer` solo vale para desarrollo).

> **La copia `.env.<fecha>.bak` lleva los secretos que hay que dar por comprometidos.** `.gitignore` la cubre (`.env.*`), pero sigue en el disco: **bórrala cuando la rotación esté completa**, y no la copies a ninguna otra parte.

### 2. Cambiar la contraseña de `sa` en el servidor

El `.env` nuevo dice cuál debe ser, pero el servidor todavía tiene la anterior. Hasta que coincidan, la API no conecta.

En un despliegue con Compose y volumen persistente, el contenedor conserva la contraseña con la que se creó. Cámbiala desde dentro:

```bash
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<la anterior>' -C \
  -Q "ALTER LOGIN sa WITH PASSWORD = '<la nueva, la del .env>'"
```

### 3. Revocar la credencial del correo

Las credenciales SMTP que estuvieron en git ya no las usa TechEval: desde el 02·10·2026 el correo sale por Microsoft Graph. Siguen en el historial, así que hay que cambiar la contraseña de ese buzón igualmente.

Para el secreto de cliente de la aplicación de Entra ID: en el registro de la aplicación, emite uno nuevo y revoca el anterior. Pega el nuevo en `O365_CLIENT_SECRET` del `.env`. `O365_TENANT_ID`, `O365_CLIENT_ID` y `EMAIL_FROM` no cambian.

Revocar es la parte que importa. Emitir otro sin revocar el anterior deja el problema donde estaba.

### 4. Reasignar la contraseña del administrador

`ADMIN_PASSWORD` solo se usa al sembrar el administrador en el primer arranque. La cuenta que ya existe conserva la anterior.

La forma limpia es entrar con la contraseña antigua una última vez, porque el sistema rehace el hash en cada inicio de sesión correcto. Si eso no es posible, hay que actualizar la fila a mano con un hash generado por la aplicación.

### 5. Desplegar

```bash
docker compose up -d
```

Si falta alguna variable, Compose falla nombrándola y la API se niega a arrancar nombrando la clave. Es lo previsto: un despliegue mal configurado debe parar, no arrancar a medias.

### 6. Comprobar

Primero, la comprobación que dice si los valores en uso siguen en el historial. Es lo único que prueba que se rotó: un despliegue con los valores viejos funciona igual de bien que uno con los nuevos.

```bash
./scripts/comprobar-rotacion.sh
```

En Windows:

```powershell
pwsh scripts/comprobar-rotacion.ps1
```

Para `SA_PASSWORD`, `JWT_SECRET_KEY`, `ADMIN_PASSWORD` y `O365_CLIENT_SECRET`, busca el valor del `.env` en todo el historial (`git log --all -S`). **No imprime ningún valor**, solo el nombre de la variable y el commit donde apareció. Falla, con código de salida 1, si:

- algún valor aparece en cualquier commit (no está rotado);
- falta algún secreto, o es demasiado corto para comprobarlo (menos de 8 caracteres);
- `.env` o `appsettings.Local.json` están rastreados por git;
- el repositorio es una copia superficial (`--depth`): un historial incompleto daría un falso «todo bien». Con `git fetch --unshallow` se arregla.

Esto **no** comprueba lo que no está en git: que la contraseña de `sa` del servidor sea la del `.env`, que el secreto de cliente anterior esté revocado en Entra ID, ni que el administrador ya creado tenga la contraseña nueva. Esos son los pasos 2 a 4.

Después:

- La API arranca y responde.
- Un inicio de sesión de administrador funciona con la contraseña nueva.
- Un envío de invitación llega al buzón.
- El `.env` **no** aparece en `git status`.

## Lo que este procedimiento no resuelve

El historial de git sigue conteniendo los valores antiguos. Reescribirlo obliga a un `push --force` y a que todo el mundo vuelva a clonar; con los valores ya rotados, aporta poco. Es una decisión abierta, no una tarea pendiente.
