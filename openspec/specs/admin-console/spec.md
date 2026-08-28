# admin-console Specification

## Purpose
TBD - created by archiving change admin-console. Update Purpose after archive.
## Requirements
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
- **WHEN** navega directamente a `/admin`, `/admin/questions`, `/admin/pruebas` o `/admin/results`
- **THEN** el sistema MUST redirigir a `/login` antes de cargar los datos de la página

#### Scenario: Sidebar visible solo con sesión activa
- **GIVEN** el layout principal de la aplicación (`MainLayout`)
- **WHEN** `AuthStateService.IsAuthenticated` es verdadero
- **THEN** el sistema SHALL mostrar la barra lateral con enlaces a Dashboard, Preguntas, Pruebas y Resultados, el nombre del usuario conectado y el botón de cerrar sesión

#### Scenario: Cierre de sesión
- **GIVEN** un administrador autenticado en cualquier página de `/admin`
- **WHEN** pulsa "Cerrar sesión" en la barra lateral
- **THEN** el sistema SHALL limpiar el token en memoria y en `localStorage` (`AuthStateService.LogoutAsync`) y navegar a `/login`

### Requirement: Gestión del banco de preguntas desde la interfaz
El sistema SHALL permitir crear, editar y listar preguntas del banco desde la interfaz de administración, adaptando el formulario según el tipo de pregunta seleccionado, y SHALL ofrecer desde esa misma pantalla acceso a la generación de preguntas por IA y a la bandeja de revisión de preguntas generadas.

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

#### Scenario: Acceso a la generación de preguntas por IA desde el listado
- **GIVEN** un administrador en `/admin/questions`
- **WHEN** consulta la cabecera del listado
- **THEN** el sistema SHALL ofrecer un botón "Generar con IA" que navega a `/admin/questions/generate`

#### Scenario: Acceso a la bandeja de revisión desde el listado
- **GIVEN** un administrador en `/admin/questions` con al menos una pregunta en `QuestionReviewStatus = PendingReview`
- **WHEN** consulta la cabecera del listado
- **THEN** el sistema SHALL ofrecer un acceso a "Pendientes de revisión" que navega a `/admin/questions/review`, indicando la cantidad de preguntas pendientes

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

