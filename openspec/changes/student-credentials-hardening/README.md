# student-credentials-hardening

Las cuentas de alumno dejan de tener contraseña derivada de su email, y el hash pasa de SHA-256 sin sal a PBKDF2 con sal e iteraciones

## Notas de despliegue

**Sin cambios de esquema.** `PasswordHash` no declara longitud máxima, así que admite el formato nuevo, que ocupa unos 90 caracteres frente a los 64 anteriores.

**Vaciar la contraseña de las cuentas de alumno ya creadas.** Hoy tienen la parte local de su email, que este cambio deja de generar pero no borra de lo existente:

```sql
UPDATE dbo.Users SET PasswordHash = '' WHERE IsAdmin = 0;
```

Los administradores no se tocan: su hash SHA-256 sigue verificando y se reescribe solo en su siguiente login correcto.

**Aviso sobre la vuelta atrás.** Los hashes ya migrados a PBKDF2 **dejarían de verificar** con el código anterior. Un administrador que haya entrado después del despliegue no podría entrar tras revertir, y habría que reasignarle la contraseña a mano. Conviene tenerlo presente antes de revertir.

**Consecuencia funcional.** El portal del alumno queda accesible solo mientras dure la sesión que emite la invitación, ocho horas por defecto. Un candidato que quiera volver una semana después no podrá entrar. Antes sí, pero también podía cualquiera que conociese su email. Recuperar ese acceso pide un correo de «establece tu contraseña», que es trabajo aparte.

**El login pasa a costar cientos de milisegundos.** Es el objetivo de PBKDF2. También convierte el login en un punto caro de atacar por volumen, así que el límite de intentos gana importancia.
