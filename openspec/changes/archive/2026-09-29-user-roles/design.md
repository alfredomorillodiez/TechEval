## Context

Ver `proposal.md` — Why para la motivación. Los requisitos están en `specs/`.

Lo que condiciona el enfoque:

- El rol se decide en un solo punto: `TokenService.GenerateJwtToken(userId, email, isAdmin)`. Lo llaman `AuthController.Login` y `ExamTokenService.ValidateTokenAsync` (este con `isAdmin: false`).
- `ITokenService.ValidateJwtToken` no tiene ningún llamador. Es código muerto.
- La API valida el JWT con `AddJwtBearer` en `Program.cs`, sin eventos. Hoy nada consulta la base de datos para autenticar una petición.
- Los controladores usan `[Authorize(Roles = "Admin")]` en la clase, y `ExamSessionController` y `StudentPortalController` usan `Roles = "Alumno"`.
- Los errores de negocio se traducen a HTTP en `ErrorHandlingMiddleware`: `ValidationException` → 400, `NotFoundException` → 404, `ConflictException` → 409, `ForbiddenException` → 403.
- `IUnitOfWork.ExecuteInTransactionAsync` abre una transacción sobre el `DbContext` de la petición.
- `TokenService.GenerateSecureToken` ya produce 48 bytes aleatorios en Base64 URL-safe. Lo usan las invitaciones.
- Los enlaces al frontal se construyen con `FrontendBaseUrl`, en el controlador, y se pasan al servicio. Así lo hace `ExamsController`.
- La Web (Blazor WASM) crea su `HttpClient` a mano en `Program.cs`. `ApiService` no trata el `401`: devuelve `null` o un código.
- `AuthStateService` guarda `auth_token`, `auth_user` y `auth_is_admin` en `localStorage`. Cinco páginas y `MainLayout` leen `Auth.IsAdmin`.
- No hay tabla de configuración de EF para `User`. El esquema lo crean los guiones de `scripts/`, y `SchemaDriftTests` exige que modelo y guion coincidan.

## Goals / Non-Goals

**Goals:**

- Que el rol tenga una sola fuente de verdad, en la base de datos y en el código.
- Que retirar el acceso a una persona tenga efecto en su siguiente petición.
- Que la matriz de permisos se lea en un solo fichero.
- Que el cambio `evaluator-review` solo tenga que añadir una política y sus endpoints, sin tocar la autenticación.

**Non-Goals:**

- Caché de la comprobación del sello. Se medirá antes de añadirla.
- Refresh tokens. La caducidad de 8 horas no cambia.
- Auditoría de quién cambió qué usuario. Si hace falta, será otro cambio.

## Decisions

### D1. El rol es un enumerado guardado como entero, con los mismos nombres que el claim

```csharp
public enum UserRole { Admin = 1, Evaluador = 2, Alumno = 3 }
```

`User.Role` sustituye a `User.IsAdmin`. La columna `Users.Role` es `INT NOT NULL` con `CHECK (Role IN (1, 2, 3))`. El claim de rol se escribe con `role.ToString()`, así que los valores del JWT siguen siendo `"Admin"` y `"Alumno"`, como hoy. `AuthResultDto` devuelve el rol como cadena, igual que el resto de enumerados de la API (`JsonStringEnumConverter`).

Alternativas descartadas:
- **Tabla `Roles` y tabla puente `UserRoles`.** Es el modelo para varios roles por usuario, y ese caso está fuera de alcance. Añade dos tablas y una consulta más al autenticar.
- **Guardar el rol como texto.** Un error de escritura crea un rol nuevo sin que nada falle. El `CHECK` sobre enteros lo impide.
- **Mantener `IsAdmin` junto a `Role`.** Dos fuentes de verdad que acabarán diciendo cosas distintas.

### D2. Sello de seguridad comprobado en `OnTokenValidated`

`User` gana `SecurityStamp` (`Guid`, columna `UNIQUEIDENTIFIER NOT NULL`). El JWT lleva el claim `stamp`. En `Program.cs`, `JwtBearerEvents.OnTokenValidated` lee `NameIdentifier`, el rol y `stamp`, y hace una consulta:

```
SELECT IsActive, Role, SecurityStamp FROM Users WHERE Id = @id
```

Si el usuario no existe, está inactivo, tiene otro rol u otro sello, el evento llama a `context.Fail(...)` y la respuesta es `401`. Un token sin `stamp` también falla.

La lógica de la comprobación vive en una clase propia (`SecurityStampValidator`) que recibe los claims y devuelve un booleano. Así se prueba sin levantar la API.

El sello cambia (`Guid.NewGuid()`) en cuatro operaciones: cambio de rol, desactivación, restablecimiento del acceso y fijación de la contraseña. La reactivación no lo cambia, porque la desactivación ya lo cambió.

`ITokenService.GenerateJwtToken` pasa a recibir el `User`. `ValidateJwtToken` se borra, porque no tiene llamadores.

Alternativas descartadas:
- **Tokens de 15 minutos con refresh token.** Da una revocación de hasta 15 minutos, no inmediata, y exige un segundo tipo de token, su almacén y su rotación en la Web.
- **Lista negra de tokens revocados.** Hay que guardar cada token emitido o su identificador, y limpiar la lista. El sello invalida todos los tokens de un usuario con una sola escritura.
- **Caché en memoria del sello.** Recorta la consulta, pero reabre una ventana de revocación igual a la vida de la caché. Se descarta hasta que una medida demuestre que la consulta pesa.

### D3. Políticas con nombre en un solo fichero

Nuevo `src/TechEval.API/Authorization/Policies.cs`:

```csharp
public static class Policies
{
    public const string Gestion = "Gestion";   // Admin
    public const string Alumno  = "Alumno";    // Alumno
}
```

`AddTechEvalAuthorization()` registra las dos con `RequireRole`. Los controladores pasan a `[Authorize(Policy = Policies.Gestion)]` y `[Authorize(Policy = Policies.Alumno)]`. `ReviewController` queda en `Gestion` en este cambio.

La política `Correccion` (Admin y Evaluador) **no se crea aquí**. Una política sin uso invita a aplicarla antes de tiempo, y en este cambio el evaluador no debe ver ningún dato.

Aviso para `evaluator-review`: ASP.NET combina con Y lógico los atributos `[Authorize]` de la clase y del método. Un método con `Correccion` dentro de una clase con `Gestion` seguiría exigiendo `Admin`. El endpoint de señales de integridad, hoy en `ResultsController`, tendrá que moverse o la clase tendrá que pasar a atributos por método.

### D4. Servicio de gestión de usuarios en la capa de aplicación

Nuevo `IUserManagementService` / `UserManagementService` en `TechEval.Application`, con `IRepository<User>`, `IRepository<PasswordSetupToken>`, `IUnitOfWork`, `ITokenService` e `IEmailService`. El controlador (`UsersController`, política `Gestion`) solo traduce HTTP y pasa el identificador del administrador y `FrontendBaseUrl`.

| Operación | Endpoint | Errores |
|---|---|---|
| Listar | `GET /api/users?role=&active=&q=` | — |
| Crear | `POST /api/users` | 400 datos o rol `Alumno`, 409 email repetido |
| Cambiar rol | `PUT /api/users/{id}/role` | 400 transición no permitida, 404, 409 propio o último admin |
| Desactivar | `POST /api/users/{id}/deactivate` | 404, 409 propio o último admin |
| Reactivar | `POST /api/users/{id}/activate` | 404 |
| Restablecer acceso | `POST /api/users/{id}/reset-access` | 400 alumno, 404, 409 propio |

Las operaciones sobre uno mismo lanzan `ConflictException`, no `ForbiddenException`. El administrador tiene permiso; lo que choca es el estado.

La tabla de transiciones de rol (spec `user-management`) vive en un método estático puro, `RoleTransitions.IsAllowed(from, to)`, con su prueba.

`UserDto` expone `Id`, `Name`, `Email`, `Role`, `IsActive`, `CreatedAt` y `AccessPending`. `AccessPending` es `Role != Alumno && PasswordHash == ""`. Las respuestas de crear y restablecer añaden `EmailSent`.

### D5. El último administrador se protege con un bloqueo de aplicación

Con la regla "no sobre uno mismo", un administrador activo nunca puede dejar el sistema sin administradores si actúa solo. El riesgo real es la concurrencia: A desactiva a B y B desactiva a A a la vez. Las dos transacciones cuentan dos administradores y las dos confirman.

Las operaciones que pueden reducir el número de administradores activos (cambiar el rol de un `Admin` y desactivar un `Admin`) se ejecutan dentro de `ExecuteInTransactionAsync` y empiezan con:

```sql
EXEC sp_getapplock @Resource = 'techeval-admins', @LockMode = 'Exclusive', @LockOwner = 'Transaction';
```

Después cuentan los administradores activos, comprueban la regla y escriben. La segunda transacción espera a que la primera confirme, cuenta uno y lanza `ConflictException`. El bloqueo se libera con la transacción.

La llamada al bloqueo va detrás de una interfaz de infraestructura (`IAdminCountLock`) con una implementación vacía para las pruebas en memoria, que no son relacionales.

Alternativas descartadas:
- **Aislamiento `Serializable` en la transacción.** También evita el fallo, pero lo hace con un interbloqueo: SQL Server mata una de las dos transacciones y hay que traducir ese error a `409`. El bloqueo de aplicación da una espera ordenada.
- **Un `SemaphoreSlim` estático.** Solo vale con una instancia de la API.

### D6. Enlaces para fijar la contraseña: tabla propia, hash SHA-256

Nueva entidad `PasswordSetupToken`:

| Campo | Tipo | Uso |
|---|---|---|
| `Id` | `int` | clave |
| `UserId` | `int` | FK a `Users`, borrado en cascada |
| `TokenHash` | `char(64)` | SHA-256 hexadecimal del token; índice único |
| `ExpiresAt` | `DateTime` | emisión + 48 h |
| `UsedAt` | `DateTime?` | `null` hasta que se usa |
| `CreatedAt` | `DateTime` | emisión |

El token sale de `GenerateSecureToken()` (48 bytes). Se guarda su SHA-256, no PBKDF2: el token tiene 384 bits de entropía, así que no hay diccionario que probar, y el hash rápido permite buscarlo por índice.

Emitir un enlace borra los enlaces no usados del mismo usuario, dentro de la misma transacción que lo crea. El correo sale fuera de la transacción. Si falla, se registra en el log y la operación devuelve `EmailSent = false`.

Endpoints públicos en `AuthController`, con una política de ritmo nueva (`PasswordSetupPolicy`, 10 por minuto y dirección):

- `GET /api/auth/password-setup/{token}` → `200 { name, email }` o `404` genérico.
- `POST /api/auth/password-setup` con `{ token, password }` → `204`, `400` si la contraseña no cumple, o `404` genérico.

El `POST` comprueba el enlace antes que la contraseña. Un `400` solo lo ve quien tiene un enlace válido.

Al fijar la contraseña, en una transacción: `PasswordHash = PasswordHasher.Hash(password)`, `UsedAt = now`, sello nuevo.

`IEmailService` gana `SendPasswordSetupAsync(toEmail, toName, link, expiresAt, ct)`.

Alternativas descartadas:
- **Contraseña temporal escrita por el administrador.** El administrador la conoce, y hace falta forzar el cambio en el primer login.
- **Token firmado (JWT) sin tabla.** No se puede marcar como usado ni invalidar al emitir otro, salvo con el sello, y entonces cualquier cambio de rol invalidaría el enlace.

### D7. Invitaciones: comprobación en el envío y en la apertura

- **Envío.** `ExamTokenService` busca el usuario por email antes de generar el token. Si existe y su rol no es `Alumno`, lanza `ValidationException`. En el envío masivo, esa excepción se captura por candidato, como los fallos de correo.
- **Apertura.** `GetOrCreateStudentAsync` devuelve `null` si el usuario existe con otro rol o está inactivo, y `ValidateTokenAsync` responde con el mensaje de token no válido. Esta segunda comprobación cubre las invitaciones enviadas antes de que la persona cambiara de rol.

### D8. Web: el rol como cadena y un manejador para el `401`

`AuthStateService` guarda `Role` (cadena) en `localStorage` con la clave `auth_role`, y deja de usar `auth_is_admin`. Mantiene `IsAdmin` como propiedad calculada (`Role == "Admin"`) y añade `IsEvaluador` e `IsAlumno`, para que las páginas que ya comprueban `IsAdmin` no cambien. `MainLayout` pasa de `!Auth.IsAdmin` a `Auth.IsAlumno` en la barra del alumno. Si `InitializeAsync` encuentra un token sin `auth_role`, borra la sesión.

Un método `HomeFor(role)` devuelve `/admin`, `/evaluacion` o `/portal`. Lo usan el login y las páginas que redirigen.

Para el `401`, `Program.cs` crea el `HttpClient` con un `DelegatingHandler` propio (`SessionExpiryHandler`). Si una respuesta es `401` y la petición no es `api/auth/login`, el manejador avisa a un servicio singleton (`SessionEvents`). `MainLayout` se suscribe al aviso: llama a `LogoutAsync` y navega a `/login?motivo=sesion` o a `/sesion-no-valida` según el rol que tenía la sesión. El manejador no conoce la navegación, así que no hay dependencia circular entre `ApiService` y `AuthStateService`.

Páginas nuevas: `Pages/Admin/Users/UserList.razor` (`/admin/usuarios`), `Pages/SetPassword.razor` (`/fijar-contrasena/{token}`), `Pages/Evaluator/Home.razor` (`/evaluacion`) y `Pages/SessionInvalid.razor` (`/sesion-no-valida`).

### D9. Guion aditivo con SQL dinámico para `IsAdmin`

`scripts/add_user_roles.sql`, idempotente, en este orden:

1. Añade `Role INT NULL` si no existe.
2. Si `IsAdmin` existe, rellena `Role` desde `IsAdmin` con `EXEC sp_executesql`. El SQL dinámico hace falta porque SQL Server compila el lote entero, y en la segunda ejecución `IsAdmin` ya no existe.
3. Pasa `Role` a `NOT NULL` y añade `CK_Users_Role`.
4. Añade `SecurityStamp UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_SecurityStamp DEFAULT NEWID()`. SQL Server evalúa `NEWID()` por fila, así que cada usuario existente recibe un sello propio.
5. Crea `PasswordSetupTokens` con su FK y su índice único.
6. Quita `DF_Users_IsAdmin` y después `IsAdmin`.

La cabecera del guion incluye, en un comentario, el SQL para volver atrás (recrear `IsAdmin` desde `Role = 1`).

`create_database.sql` cambia la tabla `Users` y añade `PasswordSetupTokens` en el orden de dependencias.

## Risks / Trade-offs

- [Una consulta más por cada petición autenticada, incluido el auto-guardado de los candidatos] → Es una lectura por clave primaria de tres columnas. Si una medida muestra que pesa, se añade una caché de pocos segundos, con la ventana de revocación que eso implica.
- [Al desplegar, los tokens sin sello dejan de valer y un candidato con una prueba abierta pierde el guardado] → El README da una consulta que cuenta las sesiones en curso. Se despliega con el recuento a cero. El candidato afectado recupera la sesión al volver a abrir su enlace, que emite un token nuevo.
- [El token del enlace viaja en la ruta y puede quedar en el log de nginx] → Es de un solo uso, caduca a las 48 horas y la base solo guarda su hash. `index.html` añade `<meta name="referrer" content="no-referrer">` para que la ruta no salga en la cabecera `Referer`.
- [Si el correo no está configurado, no se puede dar de alta a nadie] → La respuesta avisa con `EmailSent = false`. En desarrollo, MailHog captura el correo. La aplicación no ofrece copiar el enlace, porque así el administrador podría fijar la contraseña de otra persona.
- [El único administrador olvida su contraseña y no hay otro que restablezca su acceso] → `AdminPassword` solo siembra con la base vacía. El README documenta la recuperación: con acceso a la base, un operador pone `PasswordHash` a vacío y crea un enlace. Una herramienta de consola para esto queda fuera de alcance.
- [El guion quita `IsAdmin`, y un binario anterior ya no arranca contra la base actualizada] → El guion y el binario se despliegan juntos. El comentario de la cabecera da la vuelta atrás.
- [Un evaluador creado en este cambio no tiene nada que hacer hasta `evaluator-review`] → Es deliberado: su página no muestra datos. Mejor un evaluador sin trabajo que un evaluador con acceso a la identidad de los candidatos.

## Migration Plan

1. Confirmar que no hay sesiones de examen en curso (consulta del README).
2. Parar la API.
3. Ejecutar `scripts/add_user_roles.sql`.
4. Desplegar la API y la Web nuevas.
5. Todos los usuarios con sesión abierta vuelven a iniciar sesión.

Vuelta atrás: parar la API, ejecutar el SQL del comentario del guion y desplegar los binarios anteriores. Ese SQL recrea `IsAdmin` desde `Role = 1` y da a `Role` el valor por defecto `3`, porque el binario anterior crea alumnos sin indicar el rol. El resto de columnas nuevas y la tabla nueva no molestan al binario anterior.
