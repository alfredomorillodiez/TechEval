## Context

Ver `proposal.md` — Why para la motivación.

Lo que condiciona el enfoque:

- Hay cuatro usuarios en la base de datos de desarrollo, todos con hash de 64 caracteres: SHA-256 hexadecimal.
- `PasswordHash` está declarado `IsRequired()` sin longitud máxima, así que admite un valor más largo sin tocar el esquema.
- El acceso del candidato ya funciona sin contraseña: `ValidateTokenAsync` emite un JWT de alumno al abrir la invitación.
- `Program.cs` calcula el hash del administrador en línea con `SHA256.HashData` y se lo pasa a `DbSeeder`. La lógica de hash está en dos sitios.

## Goals / Non-Goals

**Goals:**

- Que conocer el email de un candidato no baste para entrar en su portal.
- Que un volcado de la tabla `Users` no permita recuperar contraseñas con una tabla precalculada.
- Que nadie tenga que cambiar su contraseña por este cambio.

**Non-Goals:**

- No se añade un flujo de establecer o restablecer contraseña. Sin él, el alumno pierde el acceso al portal fuera de la sesión de la invitación.
- No se limita el número de intentos de login.
- No se toca la duración del JWT.

## Decisions

### PBKDF2 a mano, sin añadir un paquete

**Alternativa descartada**: `PasswordHasher<TUser>` de `Microsoft.Extensions.Identity.Core`. Es código probado y lo razonable en un proyecto que ya use Identity. Este no lo usa: entraría un paquete y su modelo de usuario en la capa de aplicación solo para derivar una cadena.

`Rfc2898DeriveBytes.Pbkdf2` está en `System.Security.Cryptography`, ya disponible. Son veinte líneas y el formato queda bajo control del proyecto.

El riesgo de escribir criptografía a mano es real, pero aquí no se inventa nada: la primitiva es de la plataforma y lo único propio es la serialización de sus parámetros.

### El valor almacenado lleva sus propios parámetros

Formato: `pbkdf2.sha256.<iteraciones>.<sal base64>.<hash base64>`.

Guardar el algoritmo y las iteraciones junto al hash permite subir el coste dentro de unos años sin invalidar lo existente: los hashes viejos siguen verificando con sus parámetros, y se reescriben al entrar. Un formato que solo guardase sal y hash obligaría a un cambio con migración forzada.

600 000 iteraciones es la recomendación vigente de OWASP para PBKDF2-HMAC-SHA256. Cuesta unos cientos de milisegundos por login, que es precisamente el punto.

### Los hashes antiguos se migran al entrar, no de golpe

Un script no puede convertir SHA-256 en PBKDF2: haría falta la contraseña en claro, que nadie tiene. La única ocasión en que el sistema la ve es el login.

Así que `Verify` devuelve, además de si la contraseña es correcta, si el hash estaba en formato antiguo. `AuthController` reescribe en ese caso. Un usuario que nunca vuelva a entrar conserva su hash débil, lo cual es aceptable: su cuenta tampoco se usa.

**Alternativa descartada**: invalidar todas las contraseñas y forzar un restablecimiento. Es más limpio criptográficamente y mucho peor para quien usa el producto, por un riesgo que la migración al entrar ya reduce.

### Un hash vacío significa «sin contraseña», y se rechaza siempre

**Alternativa descartada**: generar una contraseña aleatoria que nadie conozca. El efecto es el mismo —nadie puede entrar— pero deja la duda de si existe una contraseña válida por ahí. El hash vacío dice lo que quiere decir.

**Alternativa descartada**: una columna nueva `HasPassword`. Más explícita todavía, pero exige tocar el esquema para expresar algo que la cadena vacía ya expresa sin ambigüedad.

La comprobación va en `PasswordHasher.Verify`, no en el controlador: así ningún camino futuro de autenticación puede saltársela por olvido.

### La contraseña del alumno no se sustituye, se elimina

El aprovisionamiento deja de derivar contraseña. No se genera una aleatoria porque no habría forma de entregársela al candidato: no hay correo de bienvenida ni pantalla donde establecerla.

El precio es el acceso al portal fuera de la sesión de la invitación. Está escrito en la propuesta y no se disimula.

## Risks / Trade-offs

**El alumno pierde el portal pasadas ocho horas** → Es el precio directo de cerrar el agujero. Mitigación: la invitación puede reabrirse mientras el token siga vivo, y el enlace vuelve a autenticar. La solución buena es un correo de «establece tu contraseña», anotado como trabajo aparte.

**El login pasa de microsegundos a cientos de milisegundos** → Es el objetivo, no un efecto secundario. Pero también convierte el login en un punto caro de atacar por volumen: sin límite de intentos, muchas peticiones simultáneas consumen CPU. El hallazgo del límite de intentos gana importancia con este cambio.

**Las cuentas de alumno existentes conservan su contraseña adivinable** → Este cambio solo afecta a las que se creen a partir de ahora. Mitigación: vaciar el `PasswordHash` de las cuentas de alumno ya creadas es una sentencia `UPDATE` de una línea, y va en las notas de despliegue.

**Criptografía escrita en el proyecto** → La primitiva es de la plataforma; lo propio es el formato. El riesgo está en la comparación del hash, que debe ser en tiempo constante para no filtrar información por temporización. Se usa `CryptographicOperations.FixedTimeEquals`.

## Migration Plan

Sin cambios de esquema.

Antes o después del despliegue, vaciar la contraseña de las cuentas de alumno ya aprovisionadas, que hoy tienen la parte local de su email:

```sql
UPDATE dbo.Users SET PasswordHash = '' WHERE IsAdmin = 0;
```

Los administradores no se tocan: su hash SHA-256 sigue verificando y se migra solo en el siguiente login.

Vuelta atrás: revertir el despliegue. Los hashes ya migrados a PBKDF2 **dejarían de verificar** con el código anterior, así que los administradores que hayan entrado tras el cambio necesitarían que se les reasignase la contraseña. Conviene tenerlo en cuenta antes de revertir.
