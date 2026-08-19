## Why

Hoy el candidato que resuelve una prueba no es un usuario del sistema: `ExamToken` y `ExamResult` guardan `CandidateName`/`CandidateEmail` como texto suelto, sin relación entre las distintas pruebas de una misma persona. Esto hace imposible mostrarle a un alumno un historial de sus propias notas, o mostrarle qué pruebas tiene pendientes. Se necesita una identidad persistente por alumno para poder ofrecer ese historial.

## What Changes

- Se añade el rol `Alumno` a `User` (reutilizando el bool `IsAdmin` existente: `IsAdmin = false` ahora representa un alumno en vez de un caso no usado).
- **BREAKING**: `AuthController.Login` deja de exigir `IsAdmin = true`; ahora permite el login de cualquier usuario activo (admin o alumno) con credenciales válidas.
- Al abrir un link de invitación a una prueba (`/prueba/{Token}`), si no existe un `User` con el email del candidato, el sistema lo crea automáticamente:
  - `username` = parte local del email (ej. `alejandro.robles@pronet-ise.com` → `alejandro.robles`)
  - `password` = el mismo string que el `username`, hasheada con el mismo esquema SHA-256 ya usado para admins
  - El nombre para mostrar se toma del `CandidateName` indicado por el admin al crear la invitación, y se fija de forma permanente en la primera creación (invitaciones futuras al mismo email no lo actualizan)
- El primer clic sobre el link autentica automáticamente al alumno (auto-login), sin pedir credenciales.
- Las invitaciones (`ExamToken`) quedan asociadas a un `UserId` en vez de depender únicamente de `CandidateEmail` suelto. Se permite invitar al mismo alumno a la misma prueba más de una vez; cada invitación genera una entrada independiente en su historial.
- Se añade un portal para el alumno, accesible tras login, con dos secciones: pruebas **pendientes** (invitaciones no usadas) y pruebas **realizadas** (resultados con nota, porcentaje y fecha), mostrando únicamente los datos del propio alumno autenticado.
- El login sigue siendo una única pantalla compartida (`/login`) para admin y alumno; tras autenticar, la redirección depende del rol.
- **BREAKING**: se elimina el histórico existente de `ExamToken`/`ExamResult` basado en `CandidateEmail` suelto (datos previos a este cambio) como parte del despliegue; no hay migración retroactiva a `User`.

## Capabilities

### New Capabilities
- `student-portal`: portal autenticado del alumno para consultar sus pruebas pendientes y su historial de pruebas realizadas con notas.

### Modified Capabilities
- `authentication`: el login deja de estar restringido a administradores; se añade el rol `Alumno` y el aprovisionamiento automático de cuentas de alumno a partir del email de una invitación.
- `exam-delivery`: las invitaciones a una prueba (`ExamToken`) se asocian a un `User` (alumno), resuelto o creado en el momento en que se abre el enlace; se permiten reinvitaciones a la misma prueba para el mismo alumno.
- `exam-results`: cada `ExamResult` queda asociado al `User` (alumno) dueño de la sesión, además de conservar `CandidateName`/`CandidateEmail`.
- `candidate-experience`: el acceso a `/prueba/{Token}` pasa de anónimo a autenticado (auto-login en el primer uso); el resto del flujo de resolución de la prueba (bienvenida, temporizador, autoguardado, auto-envío, pantalla final) no cambia.

## Impact

- **Dominio**: `User` gana significado para el rol no-admin (ya existe el campo `IsAdmin`); `ExamToken` (exam-delivery) y `ExamResult` (exam-results) necesitan una relación con `User` (nueva FK `UserId` o equivalente).
- **API**: `AuthController.Login` (quita el filtro `IsAdmin`); nuevo/s endpoint/s para listar pendientes y realizadas del alumno autenticado, protegidos por `[Authorize]` filtrando por el `UserId` del token; el endpoint de validación/inicio de `/prueba/{Token}` pasa a crear el `User` si no existe y a emitir un JWT de auto-login.
- **Frontend (Blazor)**: nueva página de portal del alumno; ajuste de la redirección post-login según rol; el flujo de `/prueba/{Token}` pasa a operar con sesión autenticada.
- **Datos**: se trunca el histórico existente de `ExamToken`/`ExamResult` como parte del despliegue de este cambio (decisión explícita del usuario, sin migración retroactiva).
- **Seguridad**: contraseña inicial del alumno igual a su username (riesgo aceptado explícitamente dado el tamaño y naturaleza cerrada del grupo, ~30 alumnos internos); sin obligación de cambio de contraseña en el primer login.
