## Why

Hoy la aplicación solo distingue dos perfiles, y lo hace con un booleano: `Users.IsAdmin`. El rol del JWT se calcula a partir de ese campo (`isAdmin ? "Admin" : "Alumno"`). No hay forma de dar a otra persona un acceso limitado: quien entra en la consola lo ve y lo cambia todo, incluido el banco de preguntas.

Además, no hay gestión de usuarios. El único administrador lo crea `DbSeeder` en el primer arranque. Los alumnos nacen al abrir una invitación. Nadie puede dar de alta, desactivar ni cambiar el acceso de una persona desde la aplicación. Y si alguien pierde el acceso, su JWT sigue válido hasta ocho horas.

Este cambio es el primero de dos. Prepara el terreno para el rol `Evaluador`, que corregirá pruebas en el cambio siguiente (`evaluator-review`).

## What Changes

- **BREAKING — El rol es un dato propio.** `Users.Role` reemplaza a `Users.IsAdmin`. Los valores son `Admin`, `Evaluador` y `Alumno`. Cada usuario tiene un solo rol. Un guion aditivo rellena el rol desde `IsAdmin` y después quita la columna vieja.
- **BREAKING — El JWT lleva el rol y un sello de seguridad.** El claim de rol toma el valor de `Users.Role`. El claim `isAdmin` desaparece. Un claim nuevo lleva el `SecurityStamp` del usuario. La API comprueba en cada petición que el usuario sigue activo, con el mismo rol y el mismo sello. Si algo cambió, la API responde `401`. Los tokens emitidos antes del despliegue dejan de valer.
- **BREAKING — `AuthResultDto` devuelve `Role`** en lugar de `IsAdmin`.
- **Políticas de autorización con nombre.** Los controladores usan políticas (`Gestion`, `Alumno`) en lugar de cadenas de roles. Un cambio de la matriz de permisos toca un solo sitio.
- **Gestión de usuarios.** Un endpoint nuevo, solo para administradores, permite listar, crear, cambiar el rol, desactivar, reactivar y restablecer el acceso. El sistema protege tres reglas:
  - Un administrador no puede cambiar su propio rol.
  - Un administrador no puede desactivarse a sí mismo.
  - Siempre queda al menos un administrador activo.
- **Enlace para fijar la contraseña.** Al crear un administrador o un evaluador, y al restablecer su acceso, el sistema envía por correo un enlace de un solo uso para fijar la contraseña. El administrador nunca conoce la contraseña de otra persona.
- **Invitaciones solo para alumnos.** El sistema rechaza una invitación a un correo que ya pertenece a un administrador o a un evaluador. Así se mantiene la regla de un rol por usuario.
- **Web según el rol.** El login redirige según el rol. La barra lateral muestra solo las opciones del rol. La página nueva `/admin/usuarios` presenta la gestión de usuarios. Si la API responde `401`, la Web cierra la sesión y vuelve al login.
- **Evaluador sin datos todavía.** En este cambio, un evaluador puede entrar, pero solo ve una página de bienvenida sin datos. Su trabajo llega con `evaluator-review`. Así, entre los dos cambios, un evaluador no tiene acceso a nada que no deba ver.

Fuera de alcance, y previsto para `evaluator-review`:

- La asignación de evaluadores a pruebas.
- La cola y la corrección del evaluador, a ciegas.
- La reserva de un resultado mientras alguien lo corrige.
- El historial de correcciones propias.

Fuera de alcance, sin fecha:

- La recuperación de contraseña por iniciativa del propio usuario ("he olvidado mi contraseña"). Hoy la pide al administrador.
- Varios roles por usuario.

## Capabilities

### New Capabilities

- `user-management`: alta, listado, cambio de rol, desactivación, reactivación y restablecimiento del acceso de los usuarios; reglas que protegen al último administrador y al propio administrador; enlace de un solo uso para fijar la contraseña.

### Modified Capabilities

- `authentication`: el rol pasa a ser un dato propio con tres valores; el JWT lleva el rol y el sello de seguridad; la API revoca en el acto los tokens de un usuario desactivado o con el rol cambiado; la autorización usa políticas con nombre; el aprovisionamiento del alumno rechaza correos de otros roles; se corrige el escenario que menciona el rol inexistente `User`.
- `admin-console`: el login redirige según el rol; la barra lateral depende del rol; el evaluador tiene su página de bienvenida; un `401` cierra la sesión; nueva página de gestión de usuarios.
- `exam-delivery`: el envío de una invitación rechaza un correo que pertenece a un administrador o a un evaluador.
- `deployment-ops`: guion aditivo que añade el rol, el sello de seguridad y los enlaces para fijar la contraseña sin perder datos.

## Impact

- **Dominio**: nuevo enumerado `UserRole`. `User` pierde `IsAdmin` y gana `Role` y `SecurityStamp`. Nueva entidad `PasswordSetupToken`.
- **Base de datos**: `Users.Role` y `Users.SecurityStamp` nuevas; `Users.IsAdmin` desaparece; nueva tabla `PasswordSetupTokens`. Cambian `scripts/create_database.sql` y un guion aditivo nuevo, `scripts/add_user_roles.sql`. La prueba de alineación entre modelo y guion lo exige.
- **Seguridad**: `TokenService` emite el rol y el sello. `Program.cs` añade la comprobación del sello en `OnTokenValidated` y registra las políticas. Esa comprobación añade una consulta a la base de datos por cada petición autenticada.
- **Aplicación**: nuevo servicio de gestión de usuarios. `ExamTokenService` deja de usar `isAdmin` y rechaza las invitaciones a correos de otros roles. `DbSeeder` siembra el administrador con `Role = Admin`.
- **API**: nuevo `UsersController`. `AuthController` devuelve el rol y gana los endpoints públicos del enlace para fijar la contraseña, con límite de ritmo. Todos los controladores pasan de `Roles = "..."` a políticas.
- **Correo**: `IEmailService` gana el mensaje con el enlace para fijar la contraseña.
- **Web**: `AuthStateService` guarda el rol en lugar de `IsAdmin`. Cambian `Login.razor`, `MainLayout.razor` y las páginas que comprueban `Auth.IsAdmin`. Nuevas páginas: `/admin/usuarios`, `/fijar-contrasena/{token}` y la bienvenida del evaluador. `ApiService` trata el `401` de forma global.
- **Despliegue**: los tokens emitidos antes del despliegue dejan de valer. Un candidato con una prueba abierta perdería el guardado. Hay que desplegar sin sesiones de examen en curso.
- **Pruebas**: pruebas de servicio para las tres reglas de protección, el enlace de un solo uso, la revocación por sello y el rechazo de invitaciones. Se actualizan las pruebas que usan `IsAdmin`.
- **Documentación**: `README.md` y `documentacion.md` describen los roles, la gestión de usuarios y la nota de despliegue.
