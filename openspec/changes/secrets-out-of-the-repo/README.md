# secrets-out-of-the-repo

Ningún secreto queda en el repositorio, y la API se niega a arrancar fuera de desarrollo si falta alguno o si conserva su valor de desarrollo

## Lo primero: rotar. No es opcional

Este cambio **no rota nada**. Mover un secreto no lo limpia: lo que estuvo en git sigue en el historial, y quien clonó el repositorio alguna vez lo conserva aunque hoy se borre.

Hay que emitir valores nuevos para los cuatro:

| Secreto | Dónde estuvo | Al rotar |
|---|---|---|
| Contraseña de `sa` de SQL Server | `docker-compose.yml` | Cambiarla en el servidor y en `.env` |
| Clave de firma JWT | `docker-compose.yml` y `appsettings.json` | **Caduca de golpe todas las sesiones abiertas**, incluidas las invitaciones de examen en curso |
| Contraseña del administrador | `docker-compose.yml` y `appsettings.json` | Cambiarla en `.env`; el administrador ya creado conserva la anterior hasta que se le reasigne |
| Credenciales SMTP | `appsettings.Local.json`, versionado desde el primer commit | Revocar la clave de API en el proveedor y emitir otra |

La clave JWT conviene generarla así:

```bash
openssl rand -base64 48
```

## Notas de despliegue

**Sin cambios de esquema.** Sin script SQL.

**Crear el `.env` en la máquina de destino** a partir de `.env.example`, con los valores ya rotados. Si falta alguno, Compose falla nombrando la variable y la API se niega a arrancar nombrando la clave.

**`appsettings.Local.json` ya no viaja.** Se ha sacado del índice de git y sigue en disco. Cada desarrollador conserva el suyo.

**La clave JWT nueva echa a todo el mundo.** Los administradores tendrán que volver a entrar. Peor: las invitaciones de examen en curso dejan de valer, y el candidato que esté resolviendo una prueba perderá su sesión autenticada. Conviene desplegar fuera de una ventana de exámenes, o avisar.

**Vuelta atrás.** Revertir devuelve los valores al repositorio, que es justo lo que se quería evitar. Si hay que revertir, mantener el `.env` y **no** deshacer la rotación.

## Lo que este cambio no resuelve

El historial de git sigue conteniendo los secretos antiguos. Reescribirlo obliga a un `push --force` y a que todos reclonen, y con los valores ya rotados aporta poco. Queda como decisión abierta, no como tarea pendiente.
