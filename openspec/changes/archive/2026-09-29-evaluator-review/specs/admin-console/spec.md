## ADDED Requirements

### Requirement: Evaluadores en el detalle de la prueba
El detalle de una prueba (`/admin/pruebas/{id}`) SHALL mostrar sus evaluadores asignados y SHALL permitir al administrador añadir un evaluador activo, elegido de una lista, y quitar cualquiera de los asignados. Si la prueba tiene preguntas abiertas y ningún evaluador, la tarjeta SHALL avisar de que solo la corregirá el administrador.

#### Scenario: Añadir un evaluador desde el detalle
- **GIVEN** un administrador en el detalle de una prueba
- **WHEN** elige un evaluador de la lista y lo añade
- **THEN** la tarjeta muestra al evaluador entre los asignados

#### Scenario: Prueba con abiertas y sin evaluador
- **GIVEN** una prueba con preguntas abiertas y sin evaluadores
- **WHEN** el administrador abre su detalle
- **THEN** la tarjeta avisa de que solo el administrador corregirá sus resultados

### Requirement: Aviso de pruebas sin evaluador en el dashboard
El dashboard SHALL mostrar las pruebas activas que tienen preguntas abiertas y ningún evaluador asignado, con un acceso al detalle de cada una. Si no hay ninguna, SHALL NOT mostrar el aviso.

#### Scenario: Prueba sin evaluador
- **GIVEN** una prueba activa con una pregunta abierta y sin evaluadores
- **WHEN** el administrador abre el dashboard
- **THEN** el dashboard muestra esa prueba en el aviso, con un acceso a su detalle

### Requirement: Reservas en la cola y en la corrección del administrador
La cola de correcciones del administrador SHALL indicar, en cada resultado reservado, quién tiene la reserva y hasta cuándo, y SHALL ofrecer liberarla. La pantalla de corrección del administrador SHALL tomar y renovar la reserva igual que la del evaluador, y SHALL liberarla al cancelar. Si al abrir la corrección el resultado está reservado por otra persona, la pantalla SHALL decir quién lo tiene y hasta cuándo, y SHALL NOT mostrar el formulario.

#### Scenario: Liberar una reserva desde la cola
- **GIVEN** un administrador en `/admin/results/pending` con un resultado reservado por un evaluador
- **WHEN** pulsa «Liberar» en esa fila y confirma
- **THEN** la fila deja de mostrar la reserva

#### Scenario: Abrir un resultado reservado por un evaluador
- **WHEN** el administrador abre la corrección de un resultado reservado por un evaluador
- **THEN** la pantalla dice qué evaluador lo tiene y hasta cuándo, y no muestra el formulario

## MODIFIED Requirements

### Requirement: Acceso restringido a las páginas de administración
El sistema SHALL impedir el acceso a cualquier página bajo `/admin` cuando no exista una sesión autenticada válida con rol `Admin`, y SHALL mostrar en la barra de navegación solo las opciones del rol de la sesión. Sin sesión, SHALL ocultar la barra de navegación lateral.

#### Scenario: Acceso sin sesión a una página protegida
- **GIVEN** un visitante sin token almacenado en `localStorage`
- **WHEN** navega directamente a `/admin`, `/admin/questions`, `/admin/pruebas`, `/admin/results`, `/admin/usuarios`, `/evaluacion` o `/evaluacion/historial`
- **THEN** el sistema MUST redirigir a `/login` antes de cargar los datos de la página

#### Scenario: Evaluador en una página de administración
- **GIVEN** un evaluador con una sesión válida
- **WHEN** navega directamente a cualquier página bajo `/admin`
- **THEN** el sistema MUST redirigir a `/evaluacion` antes de cargar los datos de la página

#### Scenario: Alumno en una página de administración o de evaluación
- **GIVEN** un alumno con una sesión válida
- **WHEN** navega directamente a una página bajo `/admin` o bajo `/evaluacion`
- **THEN** el sistema MUST redirigir a `/portal` antes de cargar los datos de la página

#### Scenario: Sidebar visible solo con sesión activa
- **GIVEN** el layout principal de la aplicación (`MainLayout`) con una sesión de rol `Admin`
- **WHEN** se muestra cualquier página
- **THEN** el sistema SHALL mostrar la barra lateral con enlaces a Dashboard, Preguntas, Pruebas, Resultados, Correcciones y Usuarios, el nombre del usuario conectado y el botón de cerrar sesión

#### Scenario: Barra lateral del evaluador
- **GIVEN** el layout principal con una sesión de rol `Evaluador`
- **WHEN** se muestra cualquier página
- **THEN** el sistema SHALL mostrar la barra lateral solo con los enlaces a `/evaluacion` (Correcciones) y a `/evaluacion/historial` (Historial), el nombre del usuario conectado y el botón de cerrar sesión

#### Scenario: Cierre de sesión
- **GIVEN** un usuario autenticado con rol `Admin` o `Evaluador`
- **WHEN** pulsa "Cerrar sesión" en la barra lateral
- **THEN** el sistema SHALL limpiar el token y el rol en memoria y en `localStorage` (`AuthStateService.LogoutAsync`) y navegar a `/login`

## REMOVED Requirements

### Requirement: Bienvenida del evaluador
**Reason**: `/evaluacion` deja de ser una bienvenida sin datos y pasa a ser la cola de correcciones del evaluador.
**Migration**: El comportamiento de `/evaluacion` lo describe ahora el requisito «Interfaz del evaluador» de la capacidad `evaluator-review`.
