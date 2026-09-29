## 0. Antes de empezar

- [x] 0.1 Confirmar en git el cambio `exam-integrity-signals`, que sigue sin confirmar y toca los mismos ficheros. Verificar que `git status` no muestra cambios pendientes de ese trabajo.

## 1. Esquema y dominio

- [x] 1.1 Crear el enumerado `UserRole` (`Admin = 1`, `Evaluador = 2`, `Alumno = 3`) en `src/TechEval.Domain/Enums/`. En `User`, sustituir `IsAdmin` por `Role` y añadir `SecurityStamp` (`Guid`, inicializado con `Guid.NewGuid()`). Verificar que la solución no compila y anotar cada uso de `IsAdmin` que falla: es la lista de las tareas siguientes.
- [x] 1.2 Crear la entidad `PasswordSetupToken` en `src/TechEval.Domain/Entities/` con los campos de design.md D6. Verificar que compila.
- [x] 1.3 Configurar en `AppDbContext`: `Role` guardado como entero con `HasCheckConstraint("CK_Users_Role", ...)`, `SecurityStamp` obligatorio, tabla `PasswordSetupTokens` con FK a `Users` en cascada e índice único sobre `TokenHash`. Verificar que compila.
- [x] 1.4 Crear `scripts/add_user_roles.sql` con los seis pasos de design.md D9 y el SQL de vuelta atrás en la cabecera. Verificar sobre una base local con un administrador y dos alumnos: los roles quedan en 1 y 3, cada sello es distinto, `IsAdmin` desaparece, y una segunda ejecución termina sin error.
- [x] 1.5 Llevar `Role`, `SecurityStamp`, `CK_Users_Role` y la tabla `PasswordSetupTokens` a `scripts/create_database.sql`, y quitar `IsAdmin`. Corregir de paso el comentario obsoleto de `PasswordHash` ("SHA-256 hex lowercase"). Verificar que `SchemaDriftTests` pasa.

## 2. Token y revocación

- [x] 2.1 Cambiar `ITokenService.GenerateJwtToken` para que reciba el `User` y emita el claim de rol con `Role.ToString()` y el claim `stamp`. Quitar el claim `isAdmin` y el método sin uso `ValidateJwtToken`. Verificar que compila.
- [x] 2.2 Actualizar los dos llamadores: `AuthController.Login` y `ExamTokenService.ValidateTokenAsync`. `AuthResultDto` pasa a `(Token, Name, Email, Role)`. Verificar que compila.
- [x] 2.3 Crear `SecurityStampValidator` (design.md D2): recibe el identificador, el rol y el sello del token y responde si siguen coincidiendo con la base. Verificar con pruebas: usuario sin cambios pasa; inactivo, con otro rol, con otro sello, inexistente o token sin sello fallan.
- [x] 2.4 Enganchar el validador en `JwtBearerEvents.OnTokenValidated` en `Program.cs`, con `context.Fail` si no pasa. Verificar a mano con Swagger: un token vale; después de cambiar `SecurityStamp` en la base, el mismo token recibe `401`.
- [x] 2.5 Actualizar `DbSeeder` para sembrar el administrador con `Role = UserRole.Admin`. Verificar que una base vacía arranca con el administrador y que el login devuelve el rol `Admin`.

## 3. Políticas de autorización

- [x] 3.1 Crear `src/TechEval.API/Authorization/Policies.cs` con `Gestion` y `Alumno`, y `AddTechEvalAuthorization()` que las registra. Sustituir `AddAuthorization()` en `Program.cs`. Verificar que compila.
- [x] 3.2 Cambiar `[Authorize(Roles = ...)]` por `[Authorize(Policy = ...)]` en `CategoriesController`, `QuestionsController`, `ExamsController`, `ResultsController`, `ReviewController`, `ExamSessionController` y `StudentPortalController`. Verificar con `grep` que no queda ningún `Roles =` en `src/TechEval.API`.
- [x] 3.3 Pruebas de autorización: un token de `Evaluador` recibe `403` en un endpoint de gestión, en `api/review/pending` y en el portal; un token de `Alumno` recibe `403` en gestión. Verificar que pasan.

## 4. Invitaciones solo para alumnos

- [x] 4.1 En el envío individual y masivo de `ExamTokenService`, rechazar con `ValidationException` un email que pertenece a un usuario de otro rol, antes de generar el token (design.md D7). En el masivo, el rechazo queda en el resultado de ese candidato. Verificar con pruebas: el individual da error sin token ni correo; el masivo envía a los demás.
- [x] 4.2 En `GetOrCreateStudentAsync`, no reutilizar un usuario de otro rol ni un alumno inactivo, y hacer que `ValidateTokenAsync` responda como token no válido, sin JWT. Crear el alumno nuevo con `Role = UserRole.Alumno`. Verificar con pruebas los tres casos de la spec `authentication`.
- [x] 4.3 Actualizar las pruebas que construyen usuarios con `IsAdmin` (`StudentPortalPendingTests` y las que falle la compilación). Verificar que toda la batería pasa.

## 5. Enlace para fijar la contraseña

- [x] 5.1 Añadir `SendPasswordSetupAsync` a `IEmailService` y a `SmtpEmailService`, con una plantilla HTML en la línea de la invitación: nombre, enlace y caducidad. Verificar con MailHog que el correo llega y el enlace es correcto.
- [x] 5.2 Crear en la capa de aplicación la emisión de enlaces: token con `GenerateSecureToken`, hash SHA-256, caducidad de 48 horas, borrado de los enlaces no usados del usuario, todo en una transacción; correo fuera de ella, con el fallo registrado y `EmailSent = false`. Verificar con pruebas que el valor guardado no es el token y que emitir un segundo enlace invalida el primero.
- [x] 5.3 Crear la comprobación y la fijación: respuesta genérica para enlace inexistente, caducado, usado o de cuenta inactiva; contraseña de 12 a 128 caracteres; al fijar, hash con `PasswordHasher`, `UsedAt` y sello nuevo en una transacción. Verificar con pruebas cada escenario de la spec `user-management` sobre el enlace y la fijación.
- [x] 5.4 Añadir `PasswordSetupPolicy` (10 por minuto y dirección) en `RateLimiting.cs`, y los endpoints `GET /api/auth/password-setup/{token}` y `POST /api/auth/password-setup` en `AuthController`. Verificar en Swagger las respuestas `200`/`204`, `400`, `404` y `429`, y que `RateLimitRejectionTests` sigue pasando.

## 6. Gestión de usuarios en la API

- [x] 6.1 Crear `RoleTransitions.IsAllowed(from, to)` con la tabla de la spec. Verificar con una prueba que recorre las nueve combinaciones.
- [x] 6.2 Crear `IAdminCountLock` en infraestructura, con la implementación `sp_getapplock` de design.md D5 y una vacía para las pruebas en memoria. Verificar que compila y que la implementación real se registra en la inyección de dependencias.
- [x] 6.3 Crear `UserDto`, `CreateUserDto`, `ChangeRoleDto` y las respuestas con `EmailSent` en `src/TechEval.Application/DTOs/`. Verificar que ningún DTO expone `PasswordHash`, `SecurityStamp` ni enlaces.
- [x] 6.4 Crear `UserManagementService` con listar (filtros por rol, estado y texto) y crear (solo `Admin` o `Evaluador`, email único, envío del enlace). Verificar con pruebas: alta correcta, email repetido da `409`, rol `Alumno` da `400`, fallo del correo deja la cuenta creada.
- [x] 6.5 Añadir cambiar rol, desactivar, reactivar y restablecer acceso, con el bloqueo en las operaciones que reducen administradores, el cambio de sello donde toca y el envío del enlace al pasar un alumno sin contraseña a otro rol. Verificar con pruebas: cada regla sobre uno mismo da `409`; paso a `Alumno` da `400`; restablecer un alumno da `400`; la reactivación no restaura el sello anterior.
- [x] 6.6 Crear `UsersController` con la política `Gestion` y los endpoints de design.md D4. Registrar el servicio en `Program.cs`. Verificar en Swagger cada endpoint y que un token de `Evaluador` recibe `403`.
- [x] 6.7 Prueba de concurrencia contra SQL Server local: dos peticiones simultáneas en las que cada administrador desactiva al otro. Verificar que una responde `200`, la otra `409`, y queda un administrador activo.

## 7. Web: sesión y rol

- [x] 7.1 En `AuthStateService`, sustituir `IsAdmin` guardado por `Role` con la clave `auth_role`; mantener `IsAdmin` calculado y añadir `IsEvaluador` e `IsAlumno`; borrar la sesión si hay token sin rol; limpiar también `auth_is_admin` al cerrar. Añadir `HomeFor(role)`. Verificar en el navegador que una sesión de la versión anterior lleva a `/login`.
- [x] 7.2 Actualizar `Login.razor` para usar `result.Role` y `HomeFor`, y para mostrar el mensaje de `?motivo=sesion`. Verificar el login con los tres roles y la redirección de cada uno.
- [x] 7.3 Crear `SessionEvents` y `SessionExpiryHandler` (design.md D8), y montar el `HttpClient` de `Program.cs` con el manejador. `MainLayout` se suscribe, cierra la sesión y navega según el rol. Verificar: con la aplicación abierta, desactivar al usuario en otra pestaña y hacer una acción lleva al login con el mensaje; un login fallido no dispara el aviso.
- [x] 7.4 En `MainLayout`, mostrar la barra lateral según el rol: la del administrador gana "Usuarios"; la del evaluador solo lleva "Inicio"; la barra superior del alumno pasa a depender de `IsAlumno`. Verificar a mano con los tres roles.
- [x] 7.5 Revisar las comprobaciones de acceso de las páginas bajo `/admin`: sin sesión van a `/login`; con otro rol van a `HomeFor(role)`. Verificar navegando a `/admin/results` como evaluador y como alumno.
- [x] 7.6 Crear `Pages/Evaluator/Home.razor` (`/evaluacion`) con el nombre y el aviso, sin llamadas a la API de datos, y `Pages/SessionInvalid.razor` (`/sesion-no-valida`) para el alumno. Verificar a mano las dos páginas.
- [x] 7.7 Añadir `<meta name="referrer" content="no-referrer">` a `wwwroot/index.html`. Verificar en las herramientas de desarrollo que las peticiones salen sin `Referer`.

## 8. Web: páginas nuevas

- [x] 8.1 Añadir a `ApiService` los métodos de usuarios y del enlace para fijar la contraseña, devolviendo el código y el mensaje de error como `SubmitReviewAsync`. Verificar que compila.
- [x] 8.2 Crear `Pages/Admin/Users/UserList.razor` (`/admin/usuarios`) con el listado, los filtros, el modal de alta y las acciones por fila, sin acciones sobre la propia fila y con confirmación para desactivar y restablecer. Verificar a mano cada escenario de la spec `admin-console` sobre esta página, con MailHog.
- [x] 8.3 Crear `Pages/SetPassword.razor` (`/fijar-contrasena/{token}`): comprobación al cargar, formulario con repetición, mensaje genérico si el enlace no vale, paso al login al terminar. Verificar el recorrido completo: alta de un evaluador, enlace desde MailHog, contraseña fijada y login con rol `Evaluador`.

## 9. Documentación y cierre

- [x] 9.1 Actualizar `README.md` y `documentacion.md`: los tres roles y su matriz, la gestión de usuarios, el enlace para fijar la contraseña, la recuperación del único administrador con acceso a la base, y la nota de despliegue con la consulta que cuenta las sesiones en curso. Verificar que la consulta funciona contra la base local.
- [x] 9.2 Ejecutar la batería completa y `openspec validate user-roles --strict`. Verificar que las dos pasan.
- [x] 9.3 Recorrido de extremo a extremo en local: crear un evaluador, fijar su contraseña, entrar, ver la bienvenida, recibir `403` en la API de gestión, ser desactivado y perder la sesión en la siguiente acción. Verificar cada paso contra la base de datos, no solo contra la interfaz.
