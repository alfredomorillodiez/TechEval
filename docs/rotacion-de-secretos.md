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
| Credenciales SMTP | `appsettings.Local.json`, versionado desde el primer commit | Revocar la clave en el proveedor y emitir otra |

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

### 2. Cambiar la contraseña de `sa` en el servidor

El `.env` nuevo dice cuál debe ser, pero el servidor todavía tiene la anterior. Hasta que coincidan, la API no conecta.

En un despliegue con Compose y volumen persistente, el contenedor conserva la contraseña con la que se creó. Cámbiala desde dentro:

```bash
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<la anterior>' -C \
  -Q "ALTER LOGIN sa WITH PASSWORD = '<la nueva, la del .env>'"
```

### 3. Revocar la clave del proveedor de correo

En el panel del proveedor: revoca la clave anterior **antes** de emitir la nueva, para que no queden dos válidas. Pega la nueva en `SENDGRID_API_KEY` del `.env`, y el remitente en `EMAIL_FROM`.

Revocar es la parte que importa. Emitir otra sin revocar la anterior deja el problema donde estaba.

### 4. Reasignar la contraseña del administrador

`ADMIN_PASSWORD` solo se usa al sembrar el administrador en el primer arranque. La cuenta que ya existe conserva la anterior.

La forma limpia es entrar con la contraseña antigua una última vez, porque el sistema rehace el hash en cada inicio de sesión correcto. Si eso no es posible, hay que actualizar la fila a mano con un hash generado por la aplicación.

### 5. Desplegar

```bash
docker compose up -d
```

Si falta alguna variable, Compose falla nombrándola y la API se niega a arrancar nombrando la clave. Es lo previsto: un despliegue mal configurado debe parar, no arrancar a medias.

### 6. Comprobar

- La API arranca y responde.
- Un inicio de sesión de administrador funciona con la contraseña nueva.
- Un envío de invitación llega al buzón.
- El `.env` **no** aparece en `git status`.

## Lo que este procedimiento no resuelve

El historial de git sigue conteniendo los valores antiguos. Reescribirlo obliga a un `push --force` y a que todo el mundo vuelva a clonar; con los valores ya rotados, aporta poco. Es una decisión abierta, no una tarea pendiente.
