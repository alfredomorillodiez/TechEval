## ADDED Requirements

### Requirement: Inicio de sesión de administrador
El sistema SHALL ofrecer una página de login (`/login`) donde el administrador introduce email y contraseña, y SHALL redirigir a `/admin` tanto tras un inicio de sesión correcto como al abrir la aplicación con una sesión ya válida almacenada.

#### Scenario: Login exitoso redirige al dashboard
- **GIVEN** un administrador no autenticado en `/login`
- **WHEN** introduce un email y contraseña válidos y confirma el formulario (botón o tecla Enter)
- **THEN** el sistema llama a `POST /api/auth/login`, guarda el token y el nombre recibidos mediante `AuthStateService.LoginAsync`, y navega a `/admin`

#### Scenario: Credenciales incorrectas
- **GIVEN** un administrador en `/login`
- **WHEN** envía un email o contraseña que la API rechaza
- **THEN** el sistema MUST mostrar el mensaje "Credenciales incorrectas." sin navegar fuera de `/login`

#### Scenario: Campos vacíos
- **GIVEN** un administrador en `/login`
- **WHEN** intenta enviar el formulario con el email o la contraseña vacíos
- **THEN** el sistema MUST mostrar el mensaje "Email y contraseña son obligatorios." sin llamar a la API

#### Scenario: Sesión ya iniciada al cargar el login
- **GIVEN** un `localStorage` con un token guardado de una sesión previa
- **WHEN** el administrador navega a `/login`
- **THEN** el sistema SHALL restaurar la sesión mediante `AuthStateService.InitializeAsync` y redirigir automáticamente a `/admin`

### Requirement: Acceso restringido a las páginas de administración
El sistema SHALL impedir el acceso a cualquier página bajo `/admin` cuando no exista una sesión autenticada válida, y SHALL ocultar la barra de navegación lateral en ausencia de sesión.

#### Scenario: Acceso sin sesión a una página protegida
- **GIVEN** un visitante sin token almacenado en `localStorage`
- **WHEN** navega directamente a `/admin`, `/admin/questions`, `/admin/exams` o `/admin/results`
- **THEN** el sistema MUST redirigir a `/login` antes de cargar los datos de la página

#### Scenario: Sidebar visible solo con sesión activa
- **GIVEN** el layout principal de la aplicación (`MainLayout`)
- **WHEN** `AuthStateService.IsAuthenticated` es verdadero
- **THEN** el sistema SHALL mostrar la barra lateral con enlaces a Dashboard, Preguntas, Exámenes y Resultados, el nombre del usuario conectado y el botón de cerrar sesión

#### Scenario: Cierre de sesión
- **GIVEN** un administrador autenticado en cualquier página de `/admin`
- **WHEN** pulsa "Cerrar sesión" en la barra lateral
- **THEN** el sistema SHALL limpiar el token en memoria y en `localStorage` (`AuthStateService.LogoutAsync`) y navegar a `/login`

### Requirement: Gestión del banco de preguntas desde la interfaz
El sistema SHALL permitir crear, editar y listar preguntas del banco desde la interfaz de administración, adaptando el formulario según el tipo de pregunta seleccionado.

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

### Requirement: Creación y generación de exámenes desde la interfaz
El sistema SHALL permitir al administrador crear un examen manualmente o generarlo automáticamente a partir de criterios de selección de preguntas, desde `/admin/exams`.

#### Scenario: Acceso a las dos vías de creación
- **GIVEN** un administrador en `/admin/exams`
- **WHEN** consulta la cabecera del listado
- **THEN** el sistema SHALL ofrecer un enlace a creación manual (`/admin/exams/new`) y otro a generación automática (`/admin/exams/generate`)

#### Scenario: Generación automática exitosa
- **GIVEN** un administrador en `/admin/exams/generate` con título, número de preguntas, tiempo límite y nota mínima de aprobación completados
- **WHEN** opcionalmente selecciona categorías y/o un nivel de dificultad, y confirma "Generar examen"
- **THEN** el sistema llama a `POST /api/exams/generate` con esos criterios y, si la API devuelve el examen creado, navega a `/admin/exams`

#### Scenario: Generación automática sin preguntas suficientes
- **GIVEN** un administrador en `/admin/exams/generate` con filtros de categoría y/o dificultad demasiado restrictivos
- **WHEN** confirma "Generar examen" y la API no puede satisfacer el número de preguntas solicitado
- **THEN** el sistema MUST mostrar el mensaje "No hay suficientes preguntas con los filtros seleccionados. Prueba cambiando los filtros." sin navegar fuera de la página

#### Scenario: Validación de título obligatorio en generación automática
- **GIVEN** un administrador en `/admin/exams/generate`
- **WHEN** intenta generar el examen sin haber introducido un título
- **THEN** el sistema MUST mostrar el mensaje "El título es obligatorio." sin llamar a la API

### Requirement: Envío de examen a candidatos desde el modal del listado
El sistema SHALL permitir enviar un examen existente a uno o varios candidatos mediante un modal accesible desde el listado de exámenes, con modo individual y modo masivo.

#### Scenario: Envío individual exitoso
- **GIVEN** un administrador que abre el modal de envío de un examen en modo "Individual"
- **WHEN** completa nombre, email y horas de expiración del candidato, y confirma "Enviar"
- **THEN** el sistema llama a `POST /api/exams/send` y, si la API devuelve un token, SHALL mostrar el mensaje de confirmación "Examen enviado correctamente a {email}" dentro del propio modal

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
El sistema SHALL ofrecer en `/admin/results` un listado de todas las evaluaciones completadas, con indicadores agregados y filtros combinables.

#### Scenario: Carga inicial de resultados con KPIs
- **GIVEN** un administrador que navega a `/admin/results`
- **WHEN** la página termina de cargar los resultados vía `GET /api/results`
- **THEN** el sistema SHALL mostrar el total de evaluaciones, el número y porcentaje de aprobados, el número de reprobados y la nota media calculados sobre el conjunto completo

#### Scenario: Filtrado combinado de resultados
- **GIVEN** un administrador en `/admin/results` con resultados cargados
- **WHEN** combina filtros de texto (candidato/email), examen, resultado (aprobado/reprobado), rango de nota y/o rango de fechas
- **THEN** el sistema SHALL aplicar todos los filtros activos simultáneamente sobre el listado en memoria y actualizar el contador de "Mostrando X de Y resultados"

#### Scenario: Acceso al detalle de un resultado
- **GIVEN** un administrador viendo el listado de resultados
- **WHEN** pulsa "Ver detalle" sobre una fila
- **THEN** el sistema SHALL navegar a la vista de detalle de ese resultado (`/admin/results/{id}`)
