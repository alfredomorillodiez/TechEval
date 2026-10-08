## ADDED Requirements

### Requirement: Página de gestión de usuarios
El sistema SHALL ofrecer en `/admin/usuarios`, solo para el rol `Admin`, un listado de usuarios con su nombre, email, rol, estado y si el acceso está pendiente, con filtros por rol, estado y texto. Desde esa página, el administrador SHALL poder crear un administrador o un evaluador, cambiar el rol, desactivar, reactivar y restablecer el acceso. La página SHALL NOT ofrecer un campo de contraseña en ninguna de esas acciones. Las acciones que la API no permite para una fila SHALL NOT ofrecerse en esa fila: por ejemplo, en la fila del propio administrador no se ofrece cambiar el rol, desactivar ni restablecer el acceso.

#### Scenario: Alta de un evaluador desde la página
- **GIVEN** un administrador en `/admin/usuarios`
- **WHEN** crea un usuario con nombre, email y rol `Evaluador`
- **THEN** la página muestra el usuario nuevo en el listado, con el acceso pendiente, y confirma que el enlace se envió por correo

#### Scenario: Alta con fallo del correo
- **GIVEN** un administrador que crea un evaluador y el correo no sale
- **WHEN** la API confirma la creación e indica que el correo no se envió
- **THEN** la página muestra un aviso y ofrece restablecer el acceso para reenviar el enlace

#### Scenario: Acciones sobre la propia fila
- **GIVEN** un administrador en `/admin/usuarios`
- **WHEN** consulta su propia fila del listado
- **THEN** la página no ofrece cambiar el rol, desactivar ni restablecer el acceso

#### Scenario: Confirmación de una desactivación
- **GIVEN** un administrador que pulsa "Desactivar" en la fila de un evaluador
- **WHEN** la página pide confirmación y el administrador confirma
- **THEN** la página llama a la API y muestra la fila como inactiva

#### Scenario: Error de la API mostrado al administrador
- **WHEN** la API rechaza una acción con `409 Conflict`, por ejemplo porque dejaría cero administradores activos
- **THEN** la página muestra el mensaje de la API y no cambia la fila

### Requirement: Página para fijar la contraseña
El sistema SHALL ofrecer una página pública en `/fijar-contrasena/{token}`. Al cargar, la página SHALL comprobar el enlace con la API. Si el enlace es válido, SHALL mostrar el nombre y el email del usuario y un formulario con la contraseña y su repetición. Si el enlace no es válido, SHALL mostrar un mensaje genérico que pide un enlace nuevo al administrador. Tras fijar la contraseña, la página SHALL llevar al login.

#### Scenario: Contraseña fijada desde la página
- **GIVEN** un evaluador que abre un enlace válido
- **WHEN** escribe la misma contraseña válida en los dos campos y confirma
- **THEN** la página muestra que la contraseña quedó fijada y lleva a `/login`

#### Scenario: Las dos contraseñas no coinciden
- **GIVEN** un evaluador en la página con un enlace válido
- **WHEN** escribe dos contraseñas distintas
- **THEN** la página muestra el error y no llama a la API

#### Scenario: Enlace no válido
- **WHEN** alguien abre la página con un enlace caducado, usado o inexistente
- **THEN** la página muestra el mensaje genérico y no muestra el formulario

### Requirement: Bienvenida del evaluador
El sistema SHALL ofrecer en `/evaluacion` una página para el rol `Evaluador`. En este cambio, la página SHALL mostrar el nombre del evaluador y un aviso de que todavía no tiene correcciones disponibles, y SHALL NOT mostrar datos de pruebas, candidatos ni resultados.

#### Scenario: Evaluador tras el login
- **GIVEN** un evaluador que inicia sesión
- **WHEN** llega a `/evaluacion`
- **THEN** la página muestra su nombre y el aviso, sin ningún dato de pruebas, candidatos ni resultados

### Requirement: Cierre de la sesión cuando la API la rechaza
Cuando la API responde `401 Unauthorized` a una petición hecha con una sesión abierta, la interfaz SHALL cerrar la sesión local. Si el rol de la sesión era `Admin` o `Evaluador`, SHALL llevar a `/login` con el mensaje "Tu sesión ya no es válida. Inicia sesión de nuevo.". Si el rol era `Alumno`, SHALL mostrar un mensaje que pide volver a abrir el enlace de la invitación, porque el alumno no tiene contraseña. La petición de login SHALL quedar fuera de esta regla, porque su `401` significa credenciales incorrectas.

#### Scenario: Evaluador desactivado mientras usa la aplicación
- **GIVEN** un evaluador con la aplicación abierta
- **WHEN** un administrador lo desactiva y el evaluador hace cualquier acción que llama a la API
- **THEN** la interfaz cierra la sesión y lleva a `/login` con el mensaje de sesión no válida

#### Scenario: Login con credenciales incorrectas
- **WHEN** la API responde `401` a la petición de login
- **THEN** la interfaz muestra "Credenciales incorrectas." y no trata la respuesta como una sesión rechazada

## MODIFIED Requirements

### Requirement: Inicio de sesión de administrador
El sistema SHALL ofrecer una página de login (`/login`) donde un usuario introduce email y contraseña. Tras un inicio de sesión correcto, y al abrir la aplicación con una sesión ya válida almacenada, el sistema SHALL redirigir según el rol: `Admin` a `/admin`, `Evaluador` a `/evaluacion` y `Alumno` a `/portal`.

#### Scenario: Login exitoso redirige al dashboard
- **GIVEN** un administrador no autenticado en `/login`
- **WHEN** introduce un email y contraseña válidos y confirma el formulario (botón o tecla Enter)
- **THEN** el sistema llama a `POST /api/auth/login`, guarda el token, el nombre y el rol recibidos mediante `AuthStateService.LoginAsync`, y navega a `/admin`

#### Scenario: Login exitoso de un evaluador
- **GIVEN** un evaluador no autenticado en `/login`
- **WHEN** introduce un email y contraseña válidos y confirma el formulario
- **THEN** el sistema guarda el token, el nombre y el rol `Evaluador`, y navega a `/evaluacion`

#### Scenario: Login exitoso de un alumno con contraseña
- **GIVEN** un alumno con contraseña fijada, no autenticado en `/login`
- **WHEN** introduce un email y contraseña válidos y confirma el formulario
- **THEN** el sistema guarda el token, el nombre y el rol `Alumno`, y navega a `/portal`

#### Scenario: Credenciales incorrectas
- **GIVEN** un usuario en `/login`
- **WHEN** envía un email o contraseña que la API rechaza
- **THEN** el sistema MUST mostrar el mensaje "Credenciales incorrectas." sin navegar fuera de `/login`

#### Scenario: Campos vacíos
- **GIVEN** un usuario en `/login`
- **WHEN** intenta enviar el formulario con el email o la contraseña vacíos
- **THEN** el sistema MUST mostrar el mensaje "Email y contraseña son obligatorios." sin llamar a la API

#### Scenario: Sesión ya iniciada al cargar el login
- **GIVEN** un `localStorage` con un token y un rol guardados de una sesión previa
- **WHEN** el usuario navega a `/login`
- **THEN** el sistema SHALL restaurar la sesión mediante `AuthStateService.InitializeAsync` y redirigir automáticamente a la página de inicio de su rol

#### Scenario: Sesión guardada por la versión anterior
- **GIVEN** un `localStorage` con un token guardado por la versión anterior, sin rol
- **WHEN** el usuario abre la aplicación
- **THEN** el sistema SHALL tratar la sesión como cerrada y mostrar `/login`

### Requirement: Acceso restringido a las páginas de administración
El sistema SHALL impedir el acceso a cualquier página bajo `/admin` cuando no exista una sesión autenticada válida con rol `Admin`, y SHALL mostrar en la barra de navegación solo las opciones del rol de la sesión. Sin sesión, SHALL ocultar la barra de navegación lateral.

#### Scenario: Acceso sin sesión a una página protegida
- **GIVEN** un visitante sin token almacenado en `localStorage`
- **WHEN** navega directamente a `/admin`, `/admin/questions`, `/admin/pruebas`, `/admin/results`, `/admin/usuarios` o `/evaluacion`
- **THEN** el sistema MUST redirigir a `/login` antes de cargar los datos de la página

#### Scenario: Evaluador en una página de administración
- **GIVEN** un evaluador con una sesión válida
- **WHEN** navega directamente a cualquier página bajo `/admin`
- **THEN** el sistema MUST redirigir a `/evaluacion` antes de cargar los datos de la página

#### Scenario: Alumno en una página de administración o de evaluación
- **GIVEN** un alumno con una sesión válida
- **WHEN** navega directamente a una página bajo `/admin` o a `/evaluacion`
- **THEN** el sistema MUST redirigir a `/portal` antes de cargar los datos de la página

#### Scenario: Sidebar visible solo con sesión activa
- **GIVEN** el layout principal de la aplicación (`MainLayout`) con una sesión de rol `Admin`
- **WHEN** se muestra cualquier página
- **THEN** el sistema SHALL mostrar la barra lateral con enlaces a Dashboard, Preguntas, Pruebas, Resultados, Correcciones y Usuarios, el nombre del usuario conectado y el botón de cerrar sesión

#### Scenario: Barra lateral del evaluador
- **GIVEN** el layout principal con una sesión de rol `Evaluador`
- **WHEN** se muestra cualquier página
- **THEN** el sistema SHALL mostrar la barra lateral solo con el enlace a `/evaluacion`, el nombre del usuario conectado y el botón de cerrar sesión

#### Scenario: Cierre de sesión
- **GIVEN** un usuario autenticado con rol `Admin` o `Evaluador`
- **WHEN** pulsa "Cerrar sesión" en la barra lateral
- **THEN** el sistema SHALL limpiar el token y el rol en memoria y en `localStorage` (`AuthStateService.LogoutAsync`) y navegar a `/login`
