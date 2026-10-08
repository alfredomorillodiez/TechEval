## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- Los secretos aparecen en tres sitios: `docker-compose.yml`, `appsettings.json` y `appsettings.Local.json`. Los dos primeros con los mismos valores duplicados.
- `docker-compose.yml` ya usa `${SENDGRID_API_KEY}` para una variable. El patrón existe y nadie lo extendió al resto.
- `.gitignore` ya ignora `.env`.
- `Program.cs` trae `builder.Configuration["AdminPassword"] ?? "Admin@123!"`. La configuración que falta no falla: se sustituye sola.
- `appsettings.Development.json` está versionado y hoy solo lleva la cadena de conexión local y el nivel de log.

## Goals / Non-Goals

**Goals:**

- Que clonar el repositorio no entregue ninguna credencial de producción.
- Que un despliegue incompleto pare en vez de arrancar con un valor conocido.
- Que el desarrollador siga arrancando sin configurar nada.

**Non-Goals:**

- **No se rotan las credenciales.** Exige emitir valores nuevos y tocar los sistemas de destino. Lo hace una persona.
- No se reescribe el historial de git.
- No se elige gestor de secretos: depende del destino, que aún no está decidido.

## Decisions

### Fallar al arrancar, no avisar

La alternativa suave es registrar un aviso y seguir. Se descarta: un aviso en el log de arranque de producción lo lee nadie, y la aplicación queda sirviendo con la clave que está publicada en el repositorio.

Parar en seco convierte un problema silencioso de seguridad en un problema ruidoso de despliegue, que es la clase de problema que se arregla el mismo día.

### El arranque rechaza también el valor de desarrollo

No basta con exigir que el secreto exista. Copiar el `appsettings.Development.json` a producción, o desplegar con `ASPNETCORE_ENVIRONMENT=Development`, dejaría una clave pública haciendo de secreto y pasaría la comprobación de «no está vacío».

Por eso el validador conoce los valores de desarrollo y los rechaza explícitamente fuera de desarrollo. Cuesta una constante y cierra el agujero que la comprobación ingenua deja abierto.

### Los valores de desarrollo se versionan, y se ven

**Alternativa descartada**: generar una clave aleatoria al arrancar en desarrollo. Nada queda escrito, pero cada reinicio invalida las sesiones abiertas y el desarrollador pierde su login sin entender por qué.

Se prefiere un valor fijo en `appsettings.Development.json`, donde se lee por lo que es: una credencial de desarrollo, pública a propósito. Lo que la hace inofensiva no es esconderla, es que producción la rechace.

### Compose falla con `${VAR:?mensaje}`, no con `${VAR}`

`${VAR}` sustituye por cadena vacía cuando la variable no existe. Eso levantaría SQL Server con contraseña vacía y la API con clave de firma vacía, sin un solo error visible.

`${VAR:?mensaje}` detiene el arranque nombrando la variable. Mismo principio que el validador de la API, aplicado una capa más abajo.

### `appsettings.Local.json` se saca del índice, no del disco

`git rm --cached` deja de seguirlo y lo conserva donde está. El desarrollador no pierde su configuración de correo y el fichero deja de viajar.

Lo que no arregla, y hay que decirlo: **sigue en el historial**. Quien clonó el repositorio alguna vez conserva esas credenciales SMTP. Por eso la rotación no es opcional.

## Risks / Trade-offs

**El arranque puede fallar tras desplegar este cambio** → Es el comportamiento buscado, pero conviene no descubrirlo en producción un viernes. Mitigación: la lista de variables está en `.env.example` y en las notas de despliegue, y el mensaje de error nombra la que falta.

**Los valores de desarrollo siguen en el repositorio** → Alguien puede confundirlos con secretos reales, o desplegar con el entorno mal puesto. Mitigación: el rechazo explícito fuera de desarrollo, y un comentario en el propio fichero que dice lo que son.

**Las credenciales siguen en el historial de git** → Este cambio no las borra de ahí y no puede. Solo la rotación lo resuelve. Está escrito en la propuesta, en las notas de despliegue y en el README.

**Un despliegue con `ASPNETCORE_ENVIRONMENT` mal puesto pasa las comprobaciones** → Si alguien despliega marcando Development, el validador no exige nada. Mitigación parcial: el rechazo de los valores de desarrollo no se aplica ahí, pero Swagger sí se activaría y CORS pasaría a `AllowAnyOrigin`, que son señales visibles. No se añade una comprobación adicional porque el entorno es, por definición, lo que declara quien despliega.

## Migration Plan

**Antes de desplegar, rotar. No es opcional.** Cuatro valores estuvieron en git y hay que darlos por comprometidos:

1. La contraseña de `sa` de SQL Server.
2. La clave de firma JWT. Al cambiarla, todas las sesiones abiertas caducan de golpe, incluidas las invitaciones en curso.
3. La contraseña del administrador.
4. Las credenciales SMTP de `appsettings.Local.json`.

Después:

1. Crear el `.env` en la máquina de destino a partir de `.env.example`, con los valores nuevos.
2. Desplegar. Si falta algo, la API para y dice qué falta.
3. Comprobar que el administrador entra con su contraseña nueva.

Vuelta atrás: revertir el despliegue devuelve los valores al repositorio, que es justo lo que se quería evitar. Si hay que revertir, mantener el `.env` y las credenciales rotadas.
