# admin-console Specification

## Purpose
TBD - created by archiving change admin-console. Update Purpose after archive.

## Requirements

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

### Requirement: Creación y generación de exámenes desde la interfaz
El sistema SHALL permitir al administrador crear una prueba manualmente o generarla automáticamente a partir de criterios de selección de preguntas, desde `/admin/pruebas`.

#### Scenario: Acceso a las dos vías de creación
- **GIVEN** un administrador en `/admin/pruebas`
- **WHEN** consulta la cabecera del listado
- **THEN** el sistema SHALL ofrecer un enlace a creación manual (`/admin/pruebas/new`) y otro a generación automática (`/admin/pruebas/generate`)

#### Scenario: Generación automática exitosa
- **GIVEN** un administrador en `/admin/pruebas/generate` con título, número de preguntas, tiempo límite y nota mínima de aprobación completados
- **WHEN** opcionalmente selecciona categorías y/o un nivel de dificultad, y confirma "Generar prueba"
- **THEN** el sistema llama a `POST /api/exams/generate` con esos criterios y, si la API devuelve la prueba creada, navega a `/admin/pruebas`

#### Scenario: Generación automática sin preguntas suficientes
- **GIVEN** un administrador en `/admin/pruebas/generate` con filtros de categoría y/o dificultad demasiado restrictivos
- **WHEN** confirma "Generar prueba" y la API no puede satisfacer el número de preguntas solicitado
- **THEN** el sistema MUST mostrar el mensaje "No hay suficientes preguntas con los filtros seleccionados. Prueba cambiando los filtros." sin navegar fuera de la página

#### Scenario: Validación de título obligatorio en generación automática
- **GIVEN** un administrador en `/admin/pruebas/generate`
- **WHEN** intenta generar la prueba sin haber introducido un título
- **THEN** el sistema MUST mostrar el mensaje "El título es obligatorio." sin llamar a la API

### Requirement: Envío de examen a candidatos desde el modal del listado
El sistema SHALL permitir enviar una prueba existente a uno o varios candidatos mediante un modal accesible desde el listado de pruebas, con modo individual y modo masivo.

#### Scenario: Envío individual exitoso
- **GIVEN** un administrador que abre el modal de envío de una prueba en modo "Individual"
- **WHEN** completa nombre, email y horas de expiración del candidato, y confirma "Enviar"
- **THEN** el sistema llama a `POST /api/exams/send` y, si la API devuelve un token, SHALL mostrar el mensaje de confirmación "Prueba enviada correctamente a {email}" dentro del propio modal

#### Scenario: Envío masivo mediante importación de CSV
- **GIVEN** un administrador en el modal de envío en modo "Masivo" con la pestaña "Pegar CSV" activa
- **WHEN** pega líneas con formato `Nombre,Email` y pulsa "Importar", y luego confirma el envío
- **THEN** el sistema SHALL poblar la lista de candidatos a partir de las líneas válidas, llamar a `POST /api/exams/send-bulk` con esa lista y la expiración indicada, y mostrar el recuento de envíos correctos y fallidos devuelto por la API

#### Scenario: Envío masivo sin candidatos añadidos
- **GIVEN** un administrador en el modal de envío en modo "Masivo" sin ningún candidato en la lista
- **WHEN** intenta confirmar el envío
- **THEN** el sistema MUST impedir la llamada a la API y mostrar el mensaje "Añade al menos un candidato antes de enviar."

#### Scenario: Cierre del modal tras un envío
- **GIVEN** un modal de envío que ya muestra un resultado (individual exitoso o resumen masivo)
- **WHEN** el administrador pulsa "Cerrar"
- **THEN** el sistema SHALL cerrar el modal y restablecer su estado para una próxima apertura

### Requirement: Consulta del listado global de resultados
El sistema SHALL ofrecer en `/admin/results` un listado de todas las evaluaciones completadas, con indicadores agregados y filtros combinables. Los indicadores agregados de aprobados, reprobados y nota media SHALL calcularse únicamente sobre los resultados ya corregidos; los resultados pendientes de corrección SHALL mostrarse identificados como tales, sin nota ni veredicto, y contabilizarse en un indicador propio.

#### Scenario: Carga inicial de resultados con KPIs
- **GIVEN** un administrador que navega a `/admin/results`
- **WHEN** la página termina de cargar los resultados vía `GET /api/results`
- **THEN** el sistema SHALL mostrar el total de evaluaciones, el número y porcentaje de aprobados, el número de reprobados y la nota media calculados sobre los resultados corregidos, más el número de resultados pendientes de corrección

#### Scenario: Fila de un resultado pendiente de corrección
- **GIVEN** un administrador en `/admin/results` con al menos un resultado pendiente
- **WHEN** la página muestra ese resultado
- **THEN** el sistema SHALL mostrarlo con la etiqueta «Pendiente de corrección» en lugar de «Aprobado» o «Reprobado», y sin barra de progreso de puntuación

#### Scenario: Filtrado combinado de resultados
- **GIVEN** un administrador en `/admin/results` con resultados cargados
- **WHEN** combina filtros de texto (candidato/email), prueba, resultado (aprobado/reprobado/pendiente), rango de nota y/o rango de fechas
- **THEN** el sistema SHALL aplicar todos los filtros activos simultáneamente sobre el listado en memoria y actualizar el contador de "Mostrando X de Y resultados"

#### Scenario: Los resultados pendientes quedan fuera del filtro por rango de nota
- **GIVEN** un administrador que filtra por un rango de nota
- **WHEN** existen resultados pendientes de corrección
- **THEN** el sistema SHALL excluirlos del resultado del filtro, ya que no tienen nota definitiva que comparar

#### Scenario: Acceso al detalle de un resultado
- **GIVEN** un administrador viendo el listado de resultados
- **WHEN** pulsa "Ver detalle" sobre una fila
- **THEN** el sistema SHALL navegar a la vista de detalle de ese resultado (`/admin/results/{id}`)

### Requirement: Cola de correcciones pendientes en la interfaz de administración
El sistema SHALL ofrecer en `/admin/results/pending` un listado de los resultados pendientes de corrección manual, ordenados del más antiguo al más reciente, mostrando por cada uno el candidato, la prueba, la fecha de envío, los días transcurridos y el número de respuestas abiertas por corregir. El acceso a la cola SHALL estar restringido a usuarios con rol `Admin`, igual que el resto de páginas de administración.

#### Scenario: Carga de la cola con resultados pendientes
- **GIVEN** un administrador que navega a `/admin/results/pending`
- **WHEN** la página termina de cargar la cola de correcciones
- **THEN** el sistema SHALL mostrar los resultados pendientes ordenados por antigüedad descendente de espera, cada uno con su recuento de respuestas por corregir y un acceso a la pantalla de corrección

#### Scenario: Cola vacía
- **GIVEN** un administrador en `/admin/results/pending`
- **WHEN** no existe ningún resultado pendiente de corrección
- **THEN** el sistema SHALL mostrar un estado vacío indicando que no hay correcciones pendientes, sin error

#### Scenario: Acceso visible desde el dashboard
- **GIVEN** un administrador en el dashboard con resultados pendientes de corrección
- **WHEN** la página carga las estadísticas
- **THEN** el sistema SHALL mostrar el número de correcciones pendientes con un acceso directo a `/admin/results/pending`

### Requirement: Pantalla de corrección de respuestas abiertas
El sistema SHALL ofrecer una pantalla de corrección que presente, para cada respuesta abierta pendiente del resultado, el enunciado de la pregunta, el texto entregado por el candidato, la respuesta de referencia (`SampleAnswer`) cuando exista, y un campo para otorgar puntos entre `0` y los puntos máximos de la pregunta, junto a un campo opcional de comentario. La pantalla SHALL impedir el envío mientras alguna respuesta carezca de puntuación o tenga un valor fuera de rango, y SHALL enviar la corrección de todas las respuestas en una única operación.

#### Scenario: Corrección completa de un resultado
- **GIVEN** un administrador en la pantalla de corrección de un resultado con dos respuestas abiertas pendientes
- **WHEN** otorga puntos válidos a ambas respuestas y confirma la corrección
- **THEN** el sistema SHALL enviar ambas puntuaciones en una única operación y, al recibir la confirmación, SHALL navegar de vuelta a la cola mostrando el resultado ya corregido

#### Scenario: Envío bloqueado con puntuación incompleta
- **GIVEN** un administrador en la pantalla de corrección con dos respuestas abiertas pendientes
- **WHEN** solo ha puntuado una de las dos
- **THEN** el sistema SHALL mantener deshabilitada la acción de confirmar la corrección e indicar qué respuestas faltan por puntuar

#### Scenario: Puntuación fuera de rango señalada en la interfaz
- **GIVEN** una respuesta abierta correspondiente a una pregunta de 3 puntos
- **WHEN** el administrador introduce un valor negativo o superior a 3
- **THEN** el sistema SHALL señalar el valor como inválido e impedir el envío de la corrección

#### Scenario: Resultado corregido por otro administrador entretanto
- **GIVEN** dos administradores con la misma pantalla de corrección abierta
- **WHEN** el segundo confirma la corrección después de que el primero ya la haya completado
- **THEN** el sistema SHALL mostrar un aviso de que el resultado ya fue corregido y SHALL refrescar la cola, sin aplicar una segunda corrección

### Requirement: Gestión manual del banco de preguntas desde la interfaz
El sistema SHALL permitir crear, editar y listar preguntas del banco desde la interfaz de administración, adaptando el formulario según el tipo de pregunta seleccionado. El alta de preguntas SHALL ser exclusivamente manual, sin ofrecer ningún acceso a generación por IA ni a bandeja de revisión de preguntas generadas.

#### Scenario: Listado filtrable de preguntas
- **GIVEN** un administrador en `/admin/questions`
- **WHEN** selecciona una categoría, un nivel de dificultad o un tipo en los filtros superiores
- **THEN** el sistema SHALL recargar el listado llamando a `GET /api/questions` con los parámetros de filtro correspondientes

#### Scenario: Creación de una pregunta de tipo test
- **GIVEN** un administrador en `/admin/questions/new` con el tipo "Test (4 opciones)" seleccionado
- **WHEN** completa el enunciado, la categoría, marca exactamente una opción como correcta entre las cuatro disponibles, y guarda
- **THEN** el sistema llama a `POST /api/questions` con las cuatro respuestas y navega de vuelta a `/admin/questions`

#### Scenario: Intento de guardar sin marcar respuesta correcta
- **GIVEN** un administrador creando o editando una pregunta de tipo test
- **WHEN** intenta guardar sin haber marcado ninguna opción como correcta
- **THEN** el sistema MUST mostrar el mensaje "Marca la respuesta correcta." y no llamar a la API

#### Scenario: Edición de una pregunta existente
- **GIVEN** un administrador que navega a `/admin/questions/{id}` de una pregunta existente
- **WHEN** el formulario carga los datos vía `GET /api/questions/{id}` y el administrador modifica campos y guarda
- **THEN** el sistema llama a `PUT /api/questions/{id}` con los datos actualizados y navega de vuelta a `/admin/questions`

#### Scenario: Baja de una pregunta desde el listado
- **GIVEN** un administrador en `/admin/questions` con al menos una pregunta listada
- **WHEN** pulsa el botón de eliminar sobre una fila
- **THEN** el sistema llama a `DELETE /api/questions/{id}` y, si la operación es exitosa, SHALL retirar esa fila del listado sin recargar la página completa

#### Scenario: El listado no ofrece generación por IA
- **GIVEN** un administrador en `/admin/questions`
- **WHEN** consulta la cabecera del listado
- **THEN** el sistema SHALL NOT ofrecer ningún botón "Generar con IA" ni ningún acceso a una bandeja de "Pendientes de revisión"

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

### Requirement: Cierre de la sesión cuando la API la rechaza
Cuando la API responde `401 Unauthorized` a una petición hecha con una sesión abierta, la interfaz SHALL cerrar la sesión local. Si el rol de la sesión era `Admin` o `Evaluador`, SHALL llevar a `/login` con el mensaje "Tu sesión ya no es válida. Inicia sesión de nuevo.". Si el rol era `Alumno`, SHALL mostrar un mensaje que pide volver a abrir el enlace de la invitación, porque el alumno no tiene contraseña. La petición de login SHALL quedar fuera de esta regla, porque su `401` significa credenciales incorrectas.

#### Scenario: Evaluador desactivado mientras usa la aplicación
- **GIVEN** un evaluador con la aplicación abierta
- **WHEN** un administrador lo desactiva y el evaluador hace cualquier acción que llama a la API
- **THEN** la interfaz cierra la sesión y lleva a `/login` con el mensaje de sesión no válida

#### Scenario: Login con credenciales incorrectas
- **WHEN** la API responde `401` a la petición de login
- **THEN** la interfaz muestra "Credenciales incorrectas." y no trata la respuesta como una sesión rechazada

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
