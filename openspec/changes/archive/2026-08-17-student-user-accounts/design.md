## Context

Hoy `User` (src/TechEval.Domain/Entities/User.cs) solo representa administradores (`IsAdmin = true`, `PasswordHash` SHA-256, `IsActive`). El candidato que resuelve una prueba nunca pasa por esta tabla: `ExamToken` guarda `CandidateName`/`CandidateEmail` como texto suelto y `ExamSessionController` (src/TechEval.API/Controllers/ExamSessionController.cs) expone el flujo completo de resolución de examen **sin autenticación JWT** (`GET validate/{token}` → `POST start/{token}` → `POST answer/{sessionId}` → `POST submit`).

El sustrato de autenticación ya anticipa un rol no-admin sin usarlo: `TokenService.GenerateJwtToken` (src/TechEval.Infrastructure/Security/TokenService.cs:41) ya emite `Role = "User"` cuando `isAdmin = false`, pero `AuthController.Login` filtra explícitamente `&& u.IsAdmin` (src/TechEval.API/Controllers/AuthController.cs:31), así que ese camino nunca se ha ejercitado. El grupo objetivo es pequeño y cerrado (~30 alumnos internos, 1 admin), sin necesidad de auto-registro abierto.

## Goals / Non-Goals

**Goals:**
- Dar a cada alumno una identidad persistente (`User`) para poder mostrarle un historial de sus propias pruebas realizadas y notas.
- Mantener el flujo de resolución de examen ya especificado (bienvenida, temporizador, autoguardado, auto-envío) sin cambios perceptibles para el alumno, salvo que ahora ocurre autenticado.
- Aprovisionar la cuenta del alumno sin fricción (auto-creación + auto-login en el primer clic del link de invitación).

**Non-Goals:**
- No se implementa auto-registro abierto (el alumno nunca crea su propia cuenta desde cero; siempre nace de una invitación del admin).
- No se implementa cambio de contraseña, recuperación de contraseña, ni política de expiración de contraseña para alumnos.
- No se migran retroactivamente los `ExamToken`/`ExamResult` existentes al nuevo modelo basado en `User`.
- No se introduce un tercer rol ni un sistema de permisos granular más allá de `Admin`/`Alumno`.

## Decisions

**1. Reutilizar `User.IsAdmin` como distinción de rol, en vez de introducir un enum `Role`.**
Solo hay dos roles y no se prevé un tercero. El claim `Role = "User"|"Admin"` que ya emite `TokenService` se reutiliza tal cual (o se renombra su valor a `"Alumno"` por claridad en el token, ya que el termino de negocio es "alumno" no "user"). Alternativa descartada: añadir un enum `UserRole` — más flexible a futuro, pero es complejidad innecesaria para dos roles fijos.

**2. El aprovisionamiento del `User` ocurre en `GET api/exam/validate/{token}`, no en la creación de la invitación.**
Es el primer punto de contacto del backend cuando el alumno abre el link (antes de mostrar la pantalla de bienvenida, según el requirement ya existente de `candidate-experience`). Ahí, si `ExamToken.UserId` es nulo, se busca un `User` por email; si no existe, se crea (`username`/`password` derivados del email) y se asocia. La respuesta de `validate` pasa a incluir el JWT de auto-login para que el frontend lo guarde de inmediato. Alternativa descartada: aprovisionar en `POST start/{token}` — se descarta porque `validate` ya es el punto donde el spec exige tener todo resuelto antes de pintar cualquier UI, y retrasar el login a `start` dejaría una ventana sin sesión mientras se muestra la bienvenida.

**3. `ExamToken` (capacidad `exam-delivery`, y por extensión `ExamResult` en `exam-results`) ganan una FK `UserId`, pero `CandidateName`/`CandidateEmail` se mantienen como los datos que el admin escribe al crear la invitación.**
El admin sigue sin elegir de una lista de usuarios existentes al invitar — sigue tecleando nombre/email como hoy. `UserId` se resuelve de forma perezosa (lazy) cuando el link se abre, no en el momento de creación del `ExamToken`. Esto preserva el flujo de trabajo actual del admin sin cambios.

**4. `username`/`password` derivados determinísticamente del email (`parte-local`), sin verificación adicional.**
Riesgo aceptado explícitamente: cualquiera que conozca el email de un alumno puede loguearse como él. Se acepta porque es un grupo cerrado interno de ~30 personas y prioriza cero fricción sobre robustez de credenciales. Si dos invitaciones distintas llegan con la misma parte local de email pero distinto dominio (extremadamente improbable en este contexto), se tratarían como usuarios distintos porque la búsqueda de "¿existe ya el User?" se hace por email completo, no por username derivado.

**5. El nombre del `User` se fija en su primera creación y no se actualiza en invitaciones posteriores.**
Decisión explícita: evita que un error de tipeo del admin en una invitación futura corrompa el nombre ya establecido del alumno.

**6. Reinvitaciones sin deduplicación.**
Invitar al mismo alumno a la misma prueba más de una vez es válido y genera entradas independientes en `ExamResult`/histórico. No se agrega ninguna restricción de unicidad `(UserId, ExamId)`.

**7. Nueva capacidad `student-portal` con endpoints separados de `authentication`/`exam-management`.**
"Pendientes" = `ExamToken` con `UserId` = usuario autenticado, `IsUsed = false`, no expirado. "Realizadas" = `ExamResult` unidos por `UserId`. Ambos endpoints exigen `[Authorize(Roles = "Alumno")]` (o el valor de rol elegido) y filtran siempre por el `UserId` extraído del JWT — nunca por un parámetro de la request, para evitar que un alumno consulte el historial de otro cambiando un id en la URL.

## Risks / Trade-offs

- **[Riesgo] `password == username` es trivialmente adivinable dentro de la organización** → Mitigación: aceptado explícitamente por el negocio dado el tamaño y naturaleza cerrada del grupo; no requiere acción adicional en este cambio, pero queda documentado como decisión consciente, no como descuido.
- **[Riesgo] Truncar el histórico existente (`ExamToken`/`ExamResult` previos) es una operación destructiva e irreversible** → Mitigación: se ejecuta como paso explícito y aislado en el plan de migración (ver abajo), y debe confirmarse en el momento de implementar antes de correr cualquier DELETE/TRUNCATE contra una base de datos real; no se asume silenciosamente durante la implementación de otras tareas.
- **[Riesgo] `AuthController.Login` deja de filtrar por `IsAdmin`** → cualquier `User` activo con password correcta puede loguear. Mitigación: la autorización por endpoint ya distingue por rol (`[Authorize(Roles = "Admin")]` vs `[Authorize(Roles = "Alumno")]`), así que un alumno autenticado no gana acceso a endpoints de gestión solo por poder loguear.
- **[Trade-off] `ExamSessionController` deja de ser completamente anónimo.** El comentario actual "Endpoints públicos para candidatos — sin autenticación JWT" deja de ser preciso; `validate` pasa a emitir un JWT y los siguientes pasos del flujo podrían requerirlo. Se acepta el cambio de naturaleza de este controller porque es el costo directo de tener identidad persistente.

## Migration Plan

1. Añadir `UserId` (nullable) a `ExamToken` y a `ExamResult` vía migración EF Core.
2. Truncar/eliminar los registros existentes de `ExamToken`, `ExamSession`, `UserAnswer` y `ExamResult` que no tengan `UserId` (es decir, todo el histórico previo a este cambio) — **paso destructivo, requiere confirmación explícita en el momento de ejecutarlo**, no se ejecuta automáticamente como parte de un seeder silencioso.
3. Desplegar el nuevo flujo de aprovisionamiento/login/portal.
4. A partir de este punto, toda invitación nueva genera su `User` asociado en el primer acceso; no hace falta ninguna reconciliación adicional porque no queda histórico previo con el que conciliar.

No hay estrategia de rollback de datos (el histórico truncado no se recupera); el rollback de código es el mecanismo estándar (revertir el despliegue) si se detecta un problema antes del paso 2.

## Open Questions

- ¿El claim de rol en el JWT debe renombrarse de `"User"` a `"Alumno"` (cambio de contrato para cualquier consumidor del token), o se mantiene `"User"` por compatibilidad y solo cambia la etiqueta en la UI?
- ¿Los endpoints de `student-portal` viven en un nuevo `StudentPortalController`, o se agregan a `ExamSessionController` ahora que ya no es puramente anónimo?
