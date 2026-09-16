# login-rate-limiting

Limitar el ritmo de los dos endpoints que un desconocido puede llamar sin credencial previa, para que nadie pueda agotar el servidor probando

## Why

`POST /api/auth/login` acepta intentos sin freno. Hallazgo `S5` de la auditoría.

El motivo cambió el 16·09. Antes era un problema de adivinanza: con la contraseña del alumno igual a la parte local de su email (`S2`) y hashes SHA-256 sin sal (`S3`), probar salía barato y acertar era probable.

Los dos están cerrados, así que adivinar ya no sirve. Pero `S3` sustituyó SHA-256 por PBKDF2 con 600 000 iteraciones, y eso convirtió cada intento en **cientos de milisegundos de CPU**. El problema pasó de la adivinanza al agotamiento: unas pocas peticiones simultáneas mantienen ocupados todos los hilos del servidor, y el resto de la aplicación deja de responder. No hace falta acertar ninguna contraseña.

`POST /api/exam/validate/{token}` tiene el mismo perfil: es anónimo por diseño, porque la credencial es el token del enlace, y quien lo llame en bucle puede recorrer tokens.

## What Changes

- Limitador de ritmo sobre `login`, por dirección de origen.
- Limitador de ritmo sobre `exam/validate`, por dirección de origen, más permisivo: un candidato legítimo recarga su enlace varias veces.
- El rechazo responde `429` con `ProblemDetails`, coherente con el resto de errores de la API.
- La cabecera `Retry-After` indica cuándo se puede reintentar.

## Capabilities

- `authentication` — el límite de intentos de inicio de sesión
- `exam-taking` — el límite sobre la validación del enlace de examen

## Impact

**Sin cambios de esquema.** Sin script SQL.

**El límite es por proceso, no compartido.** Con una sola instancia de la API basta. Con varias detrás de un balanceador, cada una cuenta por su lado, y el límite efectivo se multiplica por el número de instancias. Hoy el despliegue es de una instancia.

**Riesgo de contar mal el origen.** Detrás de un proxy inverso, todas las peticiones llegan con la dirección del proxy, y un solo candidato agotaría el cupo de todos. El cambio incluye la configuración de cabeceras reenviadas para que la dirección que se cuenta sea la real.
