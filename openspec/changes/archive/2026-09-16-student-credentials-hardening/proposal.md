## Why

Dos agujeros en las credenciales, ambos recogidos hoy en el spec de `authentication` como si fueran el comportamiento deseado.

**La contraseña del alumno es la parte local de su email.** Al abrir la invitación se crea la cuenta con `PasswordHash = Hash(username)`, y `username` es lo que va antes de la arroba. Con solo el email de un candidato —un dato que circula en cualquier proceso de selección— un tercero entra en `/portal` y ve sus pruebas pendientes y sus notas. No hay pantalla de cambio de contraseña, así que el candidato no puede protegerse aunque lo sepa.

**Las contraseñas se guardan con SHA-256 sin sal.** SHA-256 es rápido a propósito, y eso lo hace mal candidato para contraseñas: una tarjeta gráfica prueba miles de millones por segundo. Sin sal, dos usuarios con la misma contraseña comparten hash, y una tabla precalculada rompe las contraseñas comunes en segundos. Afecta también a la cuenta de administrador.

Los dos se refuerzan: la contraseña del alumno es adivinable **y** su hash es débil.

## What Changes

- Las cuentas de alumno que se aprovisionan al abrir una invitación **dejan de tener contraseña utilizable**. Su `PasswordHash` queda vacío y la verificación rechaza siempre un hash vacío. El acceso del candidato sigue siendo el enlace de la invitación, que ya emite su sesión autenticada.
- El hash pasa de SHA-256 sin sal a **PBKDF2-HMAC-SHA256 con sal aleatoria de 16 bytes y 600 000 iteraciones**. Se guardan juntos el algoritmo, las iteraciones, la sal y el hash, para poder subir el coste más adelante sin invalidar lo existente.
- Los hashes SHA-256 que ya existen **siguen verificando**, y en el primer login correcto se reescriben en el formato nuevo. La migración es silenciosa y no necesita que nadie cambie su contraseña.
- El administrador inicial se siembra con el esquema nuevo.

Consecuencia que conviene conocer: **el portal del alumno solo es accesible mientras dure la sesión que emite la invitación**, ocho horas por defecto. Un candidato que quiera volver una semana después no podrá entrar. Antes sí podía, pero también podía cualquiera que conociese su email. La forma buena de recuperar ese acceso es un enlace de «establece tu contraseña» por correo, que es trabajo aparte.

Fuera de alcance:

- El flujo de establecer o restablecer contraseña para el alumno.
- El límite de intentos de login, que es otro hallazgo.
- La comprobación de propiedad de la sesión de examen, que va en su propio cambio.

## Capabilities

### New Capabilities
(ninguna)

### Modified Capabilities

- `authentication`: el requisito "Hash de contraseña" cambia de esquema y añade la verificación de hashes antiguos y su migración. El requisito "Aprovisionamiento automático de cuenta de alumno" deja de derivar una contraseña del email. El requisito "Aprovisionamiento de administrador inicial" pasa al esquema nuevo.

## Impact

- **Aplicación**: `PasswordHasher` pasa de una función a dos: `Hash` produce el formato nuevo y `Verify` acepta los dos formatos e informa de si hace falta migrar. `ExamTokenService.GetOrCreateStudentAsync` (línea 167) deja de calcular contraseña.
- **API**: `AuthController.VerifyPassword` usa `PasswordHasher.Verify` y reescribe el hash tras un login correcto con formato antiguo. `Program.cs` (línea 130) siembra el administrador con el esquema nuevo en vez de con `SHA256.HashData` en línea.
- **Base de datos**: sin cambios de esquema. `PasswordHash` ya es `NVARCHAR` sin longitud máxima declarada, así que admite el formato nuevo, más largo.
- **Pruebas**: no hay ninguna sobre `PasswordHasher` ni sobre el login. Se añaden.
