## 1. Dominio y persistencia

- [x] 1.1 Añadir `UserId` (nullable) y navegación a `User` en `ExamToken` (src/TechEval.Domain/Entities/ExamToken.cs)
- [x] 1.2 Añadir `UserId` (nullable) y navegación a `User` en `ExamResult` (src/TechEval.Domain/Entities/ExamResult.cs)
- [x] 1.3 Actualizar `AppDbContext`/configuraciones de EF Core para las nuevas FKs (src/TechEval.Infrastructure/Data/AppDbContext.cs, Configurations/)
- [x] 1.4 ~~Generar migración de EF Core~~ — el proyecto no usa migraciones EF (usa `EnsureCreatedAsync` + `scripts/create_database.sql` manual); en su lugar se añadieron las columnas `UserId`/`Username` directamente a `scripts/create_database.sql`

## 2. Autenticación: login genérico y aprovisionamiento

- [x] 2.1 Quitar el filtro `&& u.IsAdmin` en `AuthController.Login` (src/TechEval.API/Controllers/AuthController.cs) para permitir login de cualquier `User` activo
- [x] 2.2 Incluir el rol (`Admin`/`Alumno`) en `AuthResultDto` si aún no viaja explícito, para que el frontend pueda redirigir correctamente
- [x] 2.3 Crear un servicio de aprovisionamiento que, dado un email y un nombre, busque un `User` existente o cree uno nuevo con `username`/`password` derivados de la parte local del email (mismo hash SHA-256 que usa `AuthController.VerifyPassword`) — extraído a `PasswordHasher` compartido
- [x] 2.4 Integrar el aprovisionamiento + emisión de JWT dentro de `IExamTokenService.ValidateTokenAsync` (expuesto por `ExamSessionController.Validate`, src/TechEval.API/Controllers/ExamSessionController.cs), asociando el `User` resultante al `ExamToken.UserId` si aún no estaba asociado
- [x] 2.5 Añadir `[Authorize(Roles = "Alumno")]` (o el valor de rol equivalente) como policy reutilizable para los endpoints del portal

## 3. Exam delivery: invitaciones atadas a un alumno

- [x] 3.1 Confirmar que la creación de `ExamToken` (envío de examen) no exige seleccionar un `User` existente — sigue aceptando `CandidateName`/`CandidateEmail` libres, como hoy
- [x] 3.2 Verificar que reinvitar al mismo `CandidateEmail` para el mismo `ExamId` no está bloqueado por ninguna restricción de unicidad existente

## 4. Exam results: historial por alumno

- [x] 4.1 Al persistir `ExamResult` en `SubmitExamAsync`, copiar el `UserId` desde el `ExamToken`/`ExamSession` de la sesión que se está completando
- [x] 4.2 Verificar que los endpoints de administración de resultados (`ResultsController`) siguen funcionando igual, ahora con `UserId` disponible como dato adicional (sin cambiar su contrato actual)

## 5. Portal del alumno (API)

- [x] 5.1 Crear endpoint de pruebas pendientes: `ExamToken` con `UserId` = usuario autenticado, `IsUsed = false`, no expirado
- [x] 5.2 Crear endpoint de pruebas realizadas: `ExamResult` con `UserId` = usuario autenticado, ordenados por `CompletedAt` descendente
- [x] 5.3 Proteger ambos endpoints con `[Authorize(Roles = "Alumno")]` y filtrar siempre por el `UserId` extraído del JWT (nunca por parámetro de la request)

## 6. Frontend (Blazor)

- [x] 6.1 Ajustar la pantalla de login compartida para redirigir según el rol devuelto (`Admin` → Dashboard existente, `Alumno` → portal nuevo)
- [x] 6.2 Crear la página del portal del alumno con las secciones "Pendientes" y "Realizadas"
- [x] 6.3 Ajustar el flujo de `/prueba/{Token}` (TakeExam.razor) para almacenar la sesión autenticada devuelta por `validate` antes de mostrar la pantalla de bienvenida
- [x] 6.4 Añadir en el portal el botón "Comenzar" sobre una pendiente, reutilizando la navegación existente hacia el flujo de resolución de examen
- [x] 6.5 (no prevista originalmente) Corregir los guards `if (!Auth.IsAuthenticated)` en las 12 páginas de `/admin/*` a `if (!Auth.IsAuthenticated || !Auth.IsAdmin)` — necesario porque ahora un alumno también puede estar autenticado

## 7. Limpieza de datos existentes (destructivo — requiere confirmación explícita antes de ejecutar)

- [x] 7.1 Preparar un script de limpieza (`scripts/reset_exam_history.sql`) que elimina `ExamToken` (y por cascada `ExamSession`/`UserAnswer`/`ExamResult`) sin tocar `Users`, `Categories`, `Questions` ni `Exams` — **nota:** el `create_database.sql` existente NO sirve para esto, ya que borra y recrea todo el esquema incluyendo el banco de preguntas
- [x] 7.2 **Pedir confirmación explícita al usuario inmediatamente antes de ejecutar ese script contra cualquier base de datos real** — confirmado por el usuario y ejecutado: 28 `ExamTokens` eliminados (con cascada a `ExamSessions`/`UserAnswers`/`ExamResults`); verificado que `Users` (1), `Exams` (8), `Questions` (218) y `Categories` (9) quedaron intactos

## 8. Validación

- [x] 8.1 Probar manualmente: primera apertura de un link nuevo crea el `User`, autentica automáticamente y muestra la bienvenida — verificado contra la BD local (`validate` creó el `User`, hash de password correcto, `ExamToken.UserId` asociado) y visualmente en navegador (portal con la invitación pendiente)
- [x] 8.2 Probar manualmente: segunda invitación al mismo alumno reutiliza el `User` y añade una entrada nueva al historial — verificado: dos invitaciones al mismo email reutilizan el mismo `UserId` y ambas aparecen como pendientes independientes
- [x] 8.3 Probar manualmente: un alumno no puede ver pendientes/realizadas de otro alumno cambiando parámetros de la request — verificado con dos alumnos distintos (aislamiento correcto) y con tokens cruzados admin↔alumno (403 en ambos endpoints nuevos)
- [x] 8.4 Probar manualmente: login de administrador sigue funcionando y redirige al Dashboard existente — verificado por API y visualmente en navegador (login → `/admin` → sidebar completo sin regresiones)
